import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { FormattedMessage, FormattedNumber, useIntl, type IntlShape } from "react-intl";
import { Link, useNavigate, useParams } from "react-router";
import { ApiProblem, knownSessionTypes, unwrap } from "../api";
import { queryKeys, useGoalOutcome, usePracticeGoal, usePracticeGoals, useStudentOverview } from "../api/queries";
import { scopedKey, useSectionPaths, useStudentScope } from "../api/studentScope";
import { useApi } from "../app/ApiContext";
import { ConfirmDialog } from "../common/ConfirmDialog";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { sessionTypeLabel } from "../practice/PracticePages";
import { goalLabels } from "../profile/PreferenceFields";
import { ConfidenceBadge, formatPlayedDate } from "../rounds/score";
import {
  emptyGoal,
  focusAreas,
  goalToForm,
  goalsToShow,
  isOpen,
  maxDetails,
  maxTargetSessions,
  maxTitle,
  progressOf,
  toGoalRequest,
  validateGoal,
  type GoalError,
  type GoalField,
  type GoalFormValues,
  type GoalOutcome,
  type PracticeGoal,
} from "./goals";
import "./goals.css";

// ---------------------------------------------------------------- on Practice

/**
 * Goals on Practice, above the sessions (#230, #227): the golfer's goals from their coach,
 * with progress; in a student's screens, the goals this coach set, and Set a practice goal.
 * The golfer's section only shows when there are goals.
 */
export function GoalsSection() {
  const goals = usePracticeGoals();
  const coaching = useStudentScope() !== undefined;
  const paths = useSectionPaths();

  if (goals.isPending) return coaching ? <Skeleton blocks={1} /> : null;
  if (goals.isError) {
    // A golfer without goals has nothing to miss; a coach is here to see them.
    return coaching ? <LoadError message={messages.goalsLoadError} onRetry={() => void goals.refetch()} /> : null;
  }

  // Defensive: Practice mustn't fail over an unexpected answer about goals.
  const shown = Array.isArray(goals.data) ? goalsToShow(goals.data) : [];
  if (!coaching && shown.length === 0) return null;

  return (
    <section className="card goals" aria-labelledby="practice-goals">
      <div className="section-header">
        <h2 id="practice-goals">
          <FormattedMessage {...(coaching ? messages.yourGoalsForStudent : messages.goalsFromYourCoach)} />
        </h2>
        {coaching && (
          <Link to={`${paths.practice}/goals/new`} className="button">
            <FormattedMessage {...messages.setPracticeGoal} />
          </Link>
        )}
      </div>
      {shown.length === 0 ? (
        <p className="hint">
          <FormattedMessage {...messages.noGoalsYet} />
        </p>
      ) : (
        <GoalList goals={shown} />
      )}
    </section>
  );
}

/** Goals with their progress, each linking to its page. */
export function GoalList({ goals }: { goals: PracticeGoal[] }) {
  const paths = useSectionPaths();
  return (
    <ul className="goal-list">
      {goals.map((goal) => (
        <li key={goal.practiceGoalId}>
          <Link to={`${paths.practice}/goals/${goal.practiceGoalId}`} className="goal-row">
            <span className="goal-row-title">{goal.title}</span>
            <GoalProgress goal={goal} />
          </Link>
        </li>
      ))}
    </ul>
  );
}

/** "3 of 6 sessions", with a bar that repeats it for sighted users; ended goals say so. */
function GoalProgress({ goal }: { goal: PracticeGoal }) {
  const { linked, target, reached } = progressOf(goal);
  return (
    <span className="goal-progress">
      <span>
        <FormattedMessage {...messages.goalProgress} values={{ linked, target }} />
        {goal.status === "Ended" && (
          <>
            {" · "}
            <FormattedMessage {...messages.goalEnded} />
          </>
        )}
        {reached && goal.status === "Active" && (
          <>
            {" · "}
            <FormattedMessage {...messages.goalReached} />
          </>
        )}
      </span>
      <span className="goal-bar" aria-hidden="true">
        <span className="goal-bar-fill" style={{ width: `${Math.min(linked / target, 1) * 100}%` }} />
      </span>
    </span>
  );
}

// ---------------------------------------------------------------- a goal

const statusLabels: Record<PracticeGoal["status"], PlainMessage> = {
  Active: messages.goalActive,
  Ended: messages.goalEnded,
  Done: messages.goalDone,
  Dismissed: messages.goalDismissed,
};

/**
 * A goal (#230, #227): what it is, progress, the linked sessions and "Did it work?" (#235).
 * The golfer marks it done or dismisses it; the coach who set it changes or removes it.
 */
export function GoalPage() {
  const { goalId = "" } = useParams();
  const goal = usePracticeGoal(goalId);
  const coaching = useStudentScope() !== undefined;
  const paths = useSectionPaths();
  const intl = useIntl();

  const back = (
    <Link to={paths.practice} className="back-link">
      <span aria-hidden="true">‹ </span>
      <FormattedMessage {...messages.backToPractice} />
    </Link>
  );

  if (goal.isPending) {
    return (
      <section className="page">
        {back}
        <Skeleton blocks={3} />
      </section>
    );
  }
  if (goal.isError) {
    return (
      <section className="page">
        {back}
        {goal.error instanceof ApiProblem && goal.error.status === 404 ? (
          <p>
            <FormattedMessage {...messages.goalGone} />
          </p>
        ) : (
          <LoadError message={messages.goalLoadError} onRetry={() => void goal.refetch()} />
        )}
      </section>
    );
  }

  const g = goal.data.goal;
  return (
    <section className="page goal-page">
      {back}
      <h1>{g.title}</h1>
      <p className="round-summary">
        {!coaching && (
          <>
            {g.coachName ? (
              <FormattedMessage {...messages.goalFrom} values={{ coach: g.coachName }} />
            ) : (
              <FormattedMessage {...messages.goalFromFormerCoach} />
            )}
            {" · "}
          </>
        )}
        <FormattedMessage
          {...messages.goalPeriod}
          values={{ from: formatPlayedDate(intl, g.startsOn), to: formatPlayedDate(intl, g.endsOn) }}
        />
        {" · "}
        <FormattedMessage {...statusLabels[g.status]} />
      </p>

      <section className="card" aria-labelledby="goal-what">
        <h2 id="goal-what">
          <FormattedMessage {...messages.goalWhat} />
        </h2>
        {g.details && <p className="feedback-text">{g.details}</p>}
        <dl className="stat-list">
          <Entry label={messages.goalTarget}>
            <GoalProgress goal={g} />
          </Entry>
          {g.focusArea && (
            <Entry label={messages.focusArea}>
              <FormattedMessage {...goalLabels[g.focusArea]} />
            </Entry>
          )}
          {g.sessionType && (
            <Entry label={messages.sessionType}>
              <FormattedMessage {...sessionTypeLabel(g.sessionType)} />
            </Entry>
          )}
        </dl>
      </section>

      <OutcomeCard goalId={g.practiceGoalId} />

      <section className="card" aria-labelledby="goal-sessions">
        <h2 id="goal-sessions">
          <FormattedMessage {...messages.goalSessions} />
        </h2>
        {goal.data.sessions.length === 0 ? (
          <p className="hint">
            <FormattedMessage {...(coaching ? messages.goalNoSessionsCoach : messages.goalNoSessions)} />
          </p>
        ) : (
          <ul className="goal-sessions">
            {goal.data.sessions.map((session) => (
              <li key={session.practiceSessionId}>
                <Link to={`${paths.practice}/${session.practiceSessionId}`}>
                  {formatPlayedDate(intl, session.practicedAt)} ·{" "}
                  <FormattedMessage {...sessionTypeLabel(session.sessionType)} />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      {coaching ? <CoachGoalActions goal={g} /> : isOpen(g) && <GolferGoalActions goal={g} />}
    </section>
  );
}

function Entry({ label, children }: { label: PlainMessage; children: ReactNode }) {
  return (
    <div>
      <dt>
        <FormattedMessage {...label} />
      </dt>
      <dd>{children}</dd>
    </div>
  );
}

/** Mark done, or dismiss after asking: the coach sees it either way. */
function GolferGoalActions({ goal }: { goal: PracticeGoal }) {
  const api = useApi();
  const intl = useIntl();
  const online = useOnline();
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);
  const change = useMutation({
    mutationFn: async (status: "Done" | "Dismissed") =>
      unwrap(
        await api.PATCH("/me/practice-goals/{id}", { params: { path: { id: goal.practiceGoalId } }, body: { status } }),
      ),
    onSuccess: async () => {
      setConfirming(false);
      // Also Home: the goal's stat may have been the next focus.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.practiceGoals }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard }),
      ]);
    },
  });
  const failed = change.isError
    ? intl.formatMessage(change.error instanceof ApiProblem ? messages.goalChangeFailed : messages.cantConnect)
    : undefined;

  return (
    <div className="goal-actions">
      {failed && !confirming && (
        <p className="form-error" role="alert">
          {failed}
        </p>
      )}
      <button
        type="button"
        className="button button--primary"
        disabled={!online || change.isPending}
        onClick={() => change.mutate("Done")}
      >
        <FormattedMessage {...messages.markGoalDone} />
      </button>
      <button
        type="button"
        className="button"
        disabled={!online || change.isPending}
        onClick={() => {
          change.reset();
          setConfirming(true);
        }}
      >
        <FormattedMessage {...messages.dismissGoal} />
      </button>
      {confirming && (
        <ConfirmDialog
          title={messages.dismissGoalTitle}
          body={messages.dismissGoalBody}
          confirm={messages.dismissGoal}
          busyLabel={messages.working}
          busy={change.isPending}
          error={failed}
          onConfirm={() => change.mutate("Dismissed")}
          onCancel={() => setConfirming(false)}
        />
      )}
    </div>
  );
}

/** Change, or remove after asking: refused once sessions are linked, when ending it sooner is the way. */
function CoachGoalActions({ goal }: { goal: PracticeGoal }) {
  const api = useApi();
  const intl = useIntl();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const golferId = useStudentScope()!;
  const paths = useSectionPaths();
  const [confirming, setConfirming] = useState(false);
  const remove = useMutation({
    mutationFn: async () =>
      unwrap(
        await api.DELETE("/coach/students/{golferId}/practice-goals/{id}", {
          params: { path: { golferId, id: goal.practiceGoalId } },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.students });
      navigate(paths.practice, { replace: true });
    },
  });
  const error = remove.isError
    ? intl.formatMessage(
        remove.error instanceof ApiProblem
          ? remove.error.status === 409
            ? messages.goalHasSessions
            : messages.goalChangeFailed
          : messages.cantConnect,
      )
    : undefined;

  if (goal.status === "Done" || goal.status === "Dismissed") return null;
  return (
    <div className="goal-actions">
      <Link to={`${paths.practice}/goals/${goal.practiceGoalId}/edit`} className="button button--primary">
        <FormattedMessage {...messages.changeGoal} />
      </Link>
      <button
        type="button"
        className="button"
        disabled={!online}
        onClick={() => {
          remove.reset();
          setConfirming(true);
        }}
      >
        <FormattedMessage {...messages.removeGoal} />
      </button>
      {confirming && (
        <ConfirmDialog
          title={messages.removeGoalTitle}
          body={messages.removeGoalBody}
          confirm={messages.removeGoal}
          busyLabel={messages.working}
          busy={remove.isPending}
          error={error}
          onConfirm={() => remove.mutate()}
          onCancel={() => setConfirming(false)}
        />
      )}
    </div>
  );
}

// ---------------------------------------------------------------- did it work?

const statLabels: Record<NonNullable<GoalOutcome["stat"]>, PlainMessage> = {
  PuttsPer18: messages.statPuttsPer18,
  PenaltiesPer18: messages.statPenaltiesPer18,
  FairwaysPer18: messages.statFairwaysPer18,
  ScoreSpread: messages.statScoreSpread,
};

/** The verdict in plain words: never "failed", and "can't tell yet" with too few rounds. */
export function verdictText(outcome: GoalOutcome, intl: IntlShape): string {
  const stat = outcome.stat ? intl.formatMessage(statLabels[outcome.stat]) : "";
  switch (outcome.verdict) {
    case "Improved":
      return intl.formatMessage(messages.verdictImproved, { stat });
    case "Worse":
      return intl.formatMessage(messages.verdictWorse, { stat });
    case "Same":
      return intl.formatMessage(messages.verdictSame, { stat });
    case "CantTellYet":
      return intl.formatMessage(messages.verdictCantTell);
    case "NotMeasured":
      return intl.formatMessage(messages.verdictNotMeasured);
  }
}

/** "Did it work?" (#235): the stat before and during, the change in words, and the confidence. */
function OutcomeCard({ goalId }: { goalId: string }) {
  const outcome = useGoalOutcome(goalId);
  const intl = useIntl();

  return (
    <section className="card" aria-labelledby="goal-outcome">
      <h2 id="goal-outcome">
        <FormattedMessage {...messages.didItWork} />
      </h2>
      {outcome.isPending ? (
        <Skeleton blocks={1} />
      ) : outcome.isError ? (
        <LoadError message={messages.outcomeLoadError} onRetry={() => void outcome.refetch()} />
      ) : (
        <>
          <p>{verdictText(outcome.data, intl)}</p>
          {outcome.data.stat && (
            <>
              <dl className="stat-list">
                <Entry label={messages.outcomeBefore}>
                  <OutcomeValue value={outcome.data.before} rounds={outcome.data.roundsBefore} />
                </Entry>
                <Entry label={messages.outcomeDuring}>
                  <OutcomeValue value={outcome.data.during} rounds={outcome.data.roundsDuring} />
                </Entry>
              </dl>
              <p className="hint">
                <FormattedMessage {...statLabels[outcome.data.stat]} />
                {" · "}
                <FormattedMessage
                  {...messages.outcomeWindow}
                  values={{
                    from: formatPlayedDate(intl, outcome.data.duringFrom),
                    to: formatPlayedDate(intl, outcome.data.duringTo),
                  }}
                />
              </p>
              <ConfidenceBadge level={outcome.data.confidence} explain />
            </>
          )}
        </>
      )}
    </section>
  );
}

function OutcomeValue({ value, rounds }: { value: number | null; rounds: number }) {
  return (
    <>
      {value === null ? "–" : <FormattedNumber value={value} maximumFractionDigits={1} />}{" "}
      <span className="hint">
        <FormattedMessage {...messages.outcomeRounds} values={{ count: rounds }} />
      </span>
    </>
  );
}

// ---------------------------------------------------------------- the coach's form

const fieldOrder: GoalField[] = [
  "title",
  "details",
  "focusArea",
  "sessionType",
  "targetSessions",
  "startsOn",
  "endsOn",
];

function goalErrorText(error: GoalError | undefined, intl: IntlShape): string | undefined {
  if (!error) return undefined;
  switch (error.code) {
    case "required":
      return intl.formatMessage(messages.goalTitleRequired);
    case "tooLong":
      return intl.formatMessage(messages.atMostCharacters, { max: error.max });
    case "targetRange":
      return intl.formatMessage(messages.goalTargetRange, { max: error.max });
    case "dateRequired":
      return intl.formatMessage(messages.dateRequired);
    case "endBeforeStart":
      return intl.formatMessage(messages.goalEndBeforeStart);
    case "tooLongPeriod":
      return intl.formatMessage(messages.goalTooLong, { weeks: error.weeks });
    case "endInPast":
      return intl.formatMessage(messages.goalEndInPast);
  }
}

/** Set a practice goal (#227), or change one: at students/:id/practice/goals/new and …/:goalId/edit. */
export function GoalFormPage() {
  const { goalId } = useParams();
  const existing = usePracticeGoal(goalId ?? "");
  if (!goalId) return <GoalForm />;
  if (existing.isPending) return <Skeleton blocks={3} />;
  if (existing.isError) return <LoadError message={messages.goalLoadError} onRetry={() => void existing.refetch()} />;
  return <GoalForm goal={existing.data.goal} />;
}

function GoalForm({ goal }: { goal?: PracticeGoal }) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const golferId = useStudentScope()!;
  const paths = useSectionPaths();
  const student = useStudentOverview(golferId);
  const formId = useId();
  const idFor = (field: GoalField) => `${formId}-${field}`;
  const [values, setValues] = useState<GoalFormValues>(() => (goal ? goalToForm(goal) : emptyGoal()));
  const [submitted, setSubmitted] = useState(false);
  const [apiErrors, setApiErrors] = useState<Partial<Record<GoalField, string>>>({});
  const [formError, setFormError] = useState<string | undefined>();
  const sending = useRef(false);

  const save = useMutation({
    mutationFn: async () => {
      const body = toGoalRequest(values);
      return goal
        ? unwrap(
            await api.PUT("/coach/students/{golferId}/practice-goals/{id}", {
              params: { path: { golferId, id: goal.practiceGoalId } },
              body,
            }),
          )
        : unwrap(await api.POST("/coach/students/{golferId}/practice-goals", { params: { path: { golferId } }, body }));
    },
    onSuccess: async (saved) => {
      // The student's goals, overview and Home all show it.
      await queryClient.invalidateQueries({ queryKey: scopedKey(golferId, []) });
      navigate(`${paths.practice}/goals/${saved.practiceGoalId}`, { replace: true });
    },
    onError: (error) => {
      if (error instanceof ApiProblem && error.status === 400) {
        const fields: Partial<Record<GoalField, string>> = {};
        for (const field of fieldOrder) {
          const message = error.messagesFor(field)[0];
          if (message) fields[field] = message;
        }
        setApiErrors(fields);
        if (Object.keys(fields).length) return;
      }
      setFormError(intl.formatMessage(error instanceof ApiProblem ? messages.goalSaveFailed : messages.cantConnect));
    },
    onSettled: () => {
      sending.current = false;
    },
  });

  const localErrors = validateGoal(values);
  const errorFor = (field: GoalField) =>
    apiErrors[field] ?? (submitted ? goalErrorText(localErrors[field], intl) : undefined);
  const update = <K extends GoalField>(field: K, value: GoalFormValues[K]) => {
    setValues((v) => ({ ...v, [field]: value }));
    setApiErrors((e) => ({ ...e, [field]: undefined }));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (sending.current || !online) return;
    setSubmitted(true);
    setFormError(undefined);
    const firstInvalid = fieldOrder.find((field) => localErrors[field]);
    if (firstInvalid) {
      document.getElementById(idFor(firstInvalid))?.focus();
      return;
    }
    sending.current = true;
    save.mutate();
  };

  return (
    <section className="page log-round-page">
      <Link to={goal ? `${paths.practice}/goals/${goal.practiceGoalId}` : paths.practice} className="back-link">
        <span aria-hidden="true">‹ </span>
        <FormattedMessage {...messages.cancel} />
      </Link>
      <h1>
        <FormattedMessage {...(goal ? messages.changeGoal : messages.setPracticeGoal)} />
      </h1>
      {student.data && (
        <p className="round-summary">
          <FormattedMessage {...messages.goalForStudent} values={{ name: student.data.displayName }} />
        </p>
      )}
      <form className="log-round-form" onSubmit={submit} noValidate>
        <FormField id={idFor("title")} label={messages.goalTitle} error={errorFor("title")}>
          {(props) => (
            <input
              {...props}
              type="text"
              maxLength={maxTitle}
              value={values.title}
              onChange={(e) => update("title", e.target.value)}
            />
          )}
        </FormField>
        <FormField id={idFor("details")} label={messages.goalDetails} error={errorFor("details")} optional>
          {(props) => (
            <textarea
              {...props}
              rows={3}
              maxLength={maxDetails}
              value={values.details}
              onChange={(e) => update("details", e.target.value)}
            />
          )}
        </FormField>
        <FormField
          id={idFor("focusArea")}
          label={messages.focusArea}
          error={errorFor("focusArea")}
          hint={messages.goalFocusAreaHint}
          optional
        >
          {(props) => (
            <select
              {...props}
              value={values.focusArea}
              onChange={(e) => update("focusArea", e.target.value as GoalFormValues["focusArea"])}
            >
              <option value="">{intl.formatMessage(messages.noFocusArea)}</option>
              {focusAreas.map((area) => (
                <option key={area} value={area}>
                  {intl.formatMessage(goalLabels[area])}
                </option>
              ))}
            </select>
          )}
        </FormField>
        <FormField id={idFor("sessionType")} label={messages.sessionType} error={errorFor("sessionType")} optional>
          {(props) => (
            <select
              {...props}
              value={values.sessionType}
              onChange={(e) => update("sessionType", e.target.value as GoalFormValues["sessionType"])}
            >
              <option value="">{intl.formatMessage(messages.anySessionType)}</option>
              {knownSessionTypes.map((type) => (
                <option key={type} value={type}>
                  {intl.formatMessage(sessionTypeLabel(type))}
                </option>
              ))}
            </select>
          )}
        </FormField>
        <FormField id={idFor("targetSessions")} label={messages.goalTarget} error={errorFor("targetSessions")}>
          {(props) => (
            <input
              {...props}
              type="number"
              min={1}
              max={maxTargetSessions}
              inputMode="numeric"
              value={values.targetSessions}
              onChange={(e) => update("targetSessions", e.target.value)}
            />
          )}
        </FormField>
        <FormField id={idFor("startsOn")} label={messages.goalStarts} error={errorFor("startsOn")}>
          {(props) => (
            <input
              {...props}
              type="date"
              value={values.startsOn}
              onChange={(e) => update("startsOn", e.target.value)}
            />
          )}
        </FormField>
        <FormField
          id={idFor("endsOn")}
          label={messages.goalEnds}
          error={errorFor("endsOn")}
          hint={messages.goalEndsHint}
        >
          {(props) => (
            <input {...props} type="date" value={values.endsOn} onChange={(e) => update("endsOn", e.target.value)} />
          )}
        </FormField>

        <div className="log-round-footer">
          {formError && (
            <p className="form-error" role="alert">
              {formError}
            </p>
          )}
          {!online && (
            <p className="hint" role="status">
              <FormattedMessage {...messages.offlineCantSaveGoal} />
            </p>
          )}
          <button
            type="submit"
            className="button button--primary"
            disabled={save.isPending || !online}
            aria-busy={save.isPending}
          >
            <FormattedMessage {...(save.isPending ? messages.saving : messages.save)} />
          </button>
        </div>
      </form>
    </section>
  );
}

function FormField({
  id,
  label,
  error,
  hint,
  optional = false,
  children,
}: {
  id: string;
  label: PlainMessage;
  error?: string;
  hint?: PlainMessage;
  optional?: boolean;
  children: (props: { id: string; "aria-invalid"?: true; "aria-describedby"?: string }) => ReactNode;
}) {
  const describedBy = [hint ? `${id}-hint` : undefined, error ? `${id}-error` : undefined].filter(Boolean).join(" ");
  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
        {optional && (
          <span className="hint">
            {" "}
            <FormattedMessage {...messages.optional} />
          </span>
        )}
      </label>
      {children({
        id,
        "aria-invalid": error ? true : undefined,
        "aria-describedby": describedBy || undefined,
      })}
      {hint && (
        <p id={`${id}-hint`} className="hint">
          <FormattedMessage {...hint} />
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className="field-error">
          {error}
        </p>
      )}
    </div>
  );
}
