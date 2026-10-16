import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from "react";
import { FormattedMessage, useIntl, type IntlShape } from "react-intl";
import { Link, useNavigate, useParams } from "react-router";
import { ApiProblem, knownSessionTypes, sessionTypeOf, unwrap, type Schemas, type SessionType } from "../api";
import { queryKeys, usePracticeList, usePracticeSession } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { ConfirmDialog } from "../common/ConfirmDialog";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { formatPlayedDate } from "../rounds/score";
import {
  addTag,
  emptyPractice,
  maxTagLength,
  maxTags,
  toPracticeRequest,
  validatePractice,
  type KnownSessionType,
  type PracticeError,
  type PracticeField,
  type PracticeFormValues,
} from "./practiceForm";
import "../rounds/rounds.css";
import "./practice.css";

/** The Practice section (#103): the session list, Log practice and a session's detail. */

const sessionTypeLabels: Record<SessionType, PlainMessage> = {
  Range: messages.sessionRange,
  Putting: messages.sessionPutting,
  ShortGame: messages.sessionShortGame,
  Simulator: messages.sessionSimulator,
  Lesson: messages.sessionLesson,
  Other: messages.sessionOther,
  // A type added to the API after this build (#134).
  unknown: messages.sessionUnknown,
};

/** The session type's name; one this build doesn't know reads as "Practice". */
export const sessionTypeLabel = (value: string) => sessionTypeLabels[sessionTypeOf(value)];

// ---------------------------------------------------------------- list

export function PracticePage() {
  const list = usePracticeList();
  const intl = useIntl();
  const sentinel = useRef<HTMLDivElement>(null);
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = list;

  useEffect(() => {
    const element = sentinel.current;
    if (!element || !hasNextPage || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting) && !isFetchingNextPage) void fetchNextPage();
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const logButton = (
    <Link to="/practice/new" className="button button--primary">
      <FormattedMessage {...messages.logPractice} />
    </Link>
  );

  const sessions = list.data?.pages.flatMap((page) => page.sessions) ?? [];

  return (
    <section className="page practice">
      <div className="section-header">
        <h1>
          <FormattedMessage {...messages.practice} />
        </h1>
        {sessions.length > 0 && logButton}
      </div>

      {list.isPending ? (
        <Skeleton blocks={3} className="round-list-skeleton" />
      ) : list.isError ? (
        <LoadError message={messages.practiceLoadError} onRetry={() => void list.refetch()} />
      ) : sessions.length === 0 ? (
        <div className="empty-state">
          <p>
            <FormattedMessage {...messages.practiceEmpty} />
          </p>
          {logButton}
        </div>
      ) : (
        <>
          <p className="hint">
            <FormattedMessage {...messages.practiceHabitsHint} />
          </p>
          <ul className="round-list">
            {sessions.map((session) => (
              <li key={session.practiceSessionId}>
                <Link to={`/practice/${session.practiceSessionId}`} className="round-row practice-row">
                  <span className="round-row-main">
                    <span className="round-row-date">{formatPlayedDate(intl, session.practicedAt)}</span>
                    <span className="round-row-course">
                      <FormattedMessage {...sessionTypeLabel(session.sessionType)} />
                    </span>
                  </span>
                  {session.durationMinutes !== null && (
                    <span className="round-row-figure">
                      <FormattedMessage {...messages.durationMinutes} values={{ minutes: session.durationMinutes }} />
                    </span>
                  )}
                </Link>
              </li>
            ))}
          </ul>
          {hasNextPage && (
            <div ref={sentinel} className="round-list-more">
              <button
                type="button"
                className="button"
                disabled={isFetchingNextPage}
                onClick={() => void fetchNextPage()}
              >
                <FormattedMessage {...(isFetchingNextPage ? messages.loading : messages.showMorePractice)} />
              </button>
            </div>
          )}
        </>
      )}
    </section>
  );
}

// ---------------------------------------------------------------- log

export function LogPracticePage() {
  const intl = useIntl();
  const api = useApi();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const online = useOnline();
  const formId = useId();
  const idFor = (field: PracticeField) => `${formId}-${field}`;

  const [values, setValues] = useState<PracticeFormValues>(() => emptyPractice());
  const [tagInput, setTagInput] = useState("");
  const [tagError, setTagError] = useState<string | undefined>();
  const [submitted, setSubmitted] = useState(false);
  const [touched, setTouched] = useState<Partial<Record<PracticeField, boolean>>>({});
  const [apiErrors, setApiErrors] = useState<Partial<Record<PracticeField, string>>>({});
  const [formError, setFormError] = useState<string | undefined>();
  const sending = useRef(false);

  const save = useMutation({
    mutationFn: async (body: Schemas["CreatePracticeSessionRequest"]) =>
      unwrap(await api.POST("/me/practice-sessions", { body })),
    onSuccess: (session) => {
      queryClient.setQueryData(queryKeys.practiceSession(session.practiceSessionId), session);
      void queryClient.invalidateQueries({ queryKey: queryKeys.practiceList });
      navigate(`/practice/${session.practiceSessionId}`, { replace: true });
    },
    onError: (error) => {
      if (error instanceof ApiProblem && error.status === 400) {
        const fields: Partial<Record<PracticeField, string>> = {};
        for (const field of practiceFields) {
          const message = error.messagesFor(field)[0];
          if (message) fields[field] = message;
        }
        setApiErrors(fields);
        if (Object.keys(fields).length) return;
      }
      setFormError(
        intl.formatMessage(error instanceof ApiProblem ? messages.practiceSaveFailed : messages.cantConnect),
      );
    },
    onSettled: () => {
      sending.current = false;
    },
  });

  const localErrors = validatePractice(values);
  const errorFor = (field: PracticeField) =>
    apiErrors[field] ?? (touched[field] || submitted ? practiceErrorText(localErrors[field], intl) : undefined);

  const update = <K extends keyof PracticeFormValues>(field: K, value: PracticeFormValues[K]) => {
    setValues((v) => ({ ...v, [field]: value }));
    setTouched((t) => ({ ...t, [field]: true }));
    setApiErrors((e) => ({ ...e, [field]: undefined }));
  };

  /** Turns what's in the tag input into a chip. False if it couldn't. */
  const commitTag = (): string[] | false => {
    if (tagInput.trim() === "") return values.tags;
    const result = addTag(values.tags, tagInput);
    if (!result.ok) {
      setTagError(
        result.reason === "tooLong"
          ? intl.formatMessage(messages.tagTooLong, { max: maxTagLength })
          : result.reason === "tooMany"
            ? intl.formatMessage(messages.tooManyTags, { max: maxTags })
            : result.reason === "duplicate"
              ? intl.formatMessage(messages.tagDuplicate)
              : undefined,
      );
      return false;
    }
    update("tags", result.tags);
    setTagInput("");
    setTagError(undefined);
    return result.tags;
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (sending.current || !online) return;
    setSubmitted(true);
    setFormError(undefined);

    // A tag still being typed counts, as if Enter had been pressed.
    const tags = commitTag();
    if (tags === false) {
      document.getElementById(idFor("tags"))?.focus();
      return;
    }

    const withTags = { ...values, tags };
    const firstInvalid = (Object.keys(validatePractice(withTags)) as PracticeField[])[0];
    if (firstInvalid) {
      document.getElementById(idFor(firstInvalid))?.focus();
      return;
    }

    sending.current = true;
    save.mutate(toPracticeRequest(withTags));
  };

  return (
    <section className="page log-round-page">
      <div className="log-round-header">
        <h1>
          <FormattedMessage {...messages.logPractice} />
        </h1>
        <button
          type="button"
          className="icon-button"
          aria-label={intl.formatMessage(messages.close)}
          onClick={() =>
            (window.history.state as { idx?: number } | null)?.idx ? navigate(-1) : navigate("/practice")
          }
        >
          <span aria-hidden="true">✕</span>
        </button>
      </div>

      <form className="log-round-form" onSubmit={submit} noValidate>
        <Field id={idFor("practicedAt")} label={messages.date} error={errorFor("practicedAt")}>
          {(props) => (
            <input
              {...props}
              type="date"
              value={values.practicedAt}
              onChange={(e) => update("practicedAt", e.target.value)}
            />
          )}
        </Field>

        <Field id={idFor("sessionType")} label={messages.sessionType} error={errorFor("sessionType")}>
          {(props) => (
            <select
              {...props}
              value={values.sessionType}
              onChange={(e) => update("sessionType", e.target.value as KnownSessionType)}
            >
              {knownSessionTypes.map((type) => (
                <option key={type} value={type}>
                  {intl.formatMessage(sessionTypeLabels[type])}
                </option>
              ))}
            </select>
          )}
        </Field>

        <Field
          id={idFor("durationMinutes")}
          label={messages.duration}
          error={errorFor("durationMinutes")}
          suffix={<FormattedMessage {...messages.minutesUnit} />}
        >
          {(props) => (
            <input
              {...props}
              className="duration-input"
              type="text"
              inputMode="numeric"
              pattern="[0-9]*"
              autoComplete="off"
              value={values.durationMinutes}
              onChange={(e) => update("durationMinutes", e.target.value)}
            />
          )}
        </Field>

        <Field id={idFor("venueName")} label={messages.venue} error={errorFor("venueName")}>
          {(props) => (
            <input
              {...props}
              type="text"
              value={values.venueName}
              onChange={(e) => update("venueName", e.target.value)}
            />
          )}
        </Field>

        <Field id={idFor("primaryGoal")} label={messages.goal} error={errorFor("primaryGoal")}>
          {(props) => (
            <input
              {...props}
              type="text"
              value={values.primaryGoal}
              onChange={(e) => update("primaryGoal", e.target.value)}
            />
          )}
        </Field>

        <TagInput
          id={idFor("tags")}
          tags={values.tags}
          input={tagInput}
          error={tagError ?? errorFor("tags")}
          onInput={(text) => {
            setTagInput(text);
            setTagError(undefined);
          }}
          onCommit={commitTag}
          onRemove={(tag) =>
            update(
              "tags",
              values.tags.filter((t) => t !== tag),
            )
          }
        />

        <Field id={idFor("notes")} label={messages.notes} error={errorFor("notes")}>
          {(props) => (
            <textarea {...props} rows={3} value={values.notes} onChange={(e) => update("notes", e.target.value)} />
          )}
        </Field>

        <div className="log-round-footer">
          {formError && (
            <p className="form-error" role="alert">
              {formError}
            </p>
          )}
          {!online && (
            <p className="hint" role="status">
              <FormattedMessage {...messages.offlineCantSavePractice} />
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

const practiceFields: PracticeField[] = [
  "practicedAt",
  "sessionType",
  "durationMinutes",
  "venueName",
  "primaryGoal",
  "tags",
  "notes",
];

function practiceErrorText(error: PracticeError | undefined, intl: IntlShape): string | undefined {
  if (!error) return undefined;
  switch (error.code) {
    case "dateRequired":
      return intl.formatMessage(messages.dateRequired);
    case "wholeNumber":
      return intl.formatMessage(messages.wholeNumber);
    case "durationRange":
      return intl.formatMessage(messages.durationRange, { max: error.max });
    case "tooLong":
      return intl.formatMessage(messages.atMostCharacters, { max: error.max });
    case "tooManyTags":
      return intl.formatMessage(messages.tooManyTags, { max: error.max });
  }
}

type ControlProps = {
  id: string;
  "aria-invalid"?: true;
  "aria-describedby"?: string;
};

/** A label, its control, and the control's error, wired together for screen readers. */
function Field({
  id,
  label,
  error,
  suffix,
  children,
}: {
  id: string;
  label: PlainMessage;
  error?: string;
  suffix?: ReactNode;
  children: (props: ControlProps) => ReactNode;
}) {
  const props: ControlProps = {
    id,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": error ? `${id}-error` : undefined,
  };
  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
      </label>
      {suffix ? (
        <div className="field-with-suffix">
          {children(props)}
          <span className="hint">{suffix}</span>
        </div>
      ) : (
        children(props)
      )}
      {error && (
        <p id={`${id}-error`} className="field-error">
          {error}
        </p>
      )}
    </div>
  );
}

/**
 * Tags as chips: type one and press Enter or comma to add it; each chip has a labelled
 * remove button. Backspace in the empty input doesn't delete chips, so none goes by
 * accident.
 */
function TagInput({
  id,
  tags,
  input,
  error,
  onInput,
  onCommit,
  onRemove,
}: {
  id: string;
  tags: string[];
  input: string;
  error?: string;
  onInput: (text: string) => void;
  onCommit: () => void;
  onRemove: (tag: string) => void;
}) {
  const intl = useIntl();
  const hintId = `${id}-hint`;

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter" || event.key === ",") {
      event.preventDefault();
      onCommit();
    }
  };

  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...messages.tags} />
      </label>
      {tags.length > 0 && (
        <ul className="tag-chips">
          {tags.map((tag) => (
            <li key={tag} className="tag-chip">
              <span>{tag}</span>
              <button
                type="button"
                className="tag-remove"
                aria-label={intl.formatMessage(messages.removeTag, { tag })}
                onClick={() => {
                  onRemove(tag);
                  document.getElementById(id)?.focus();
                }}
              >
                <span aria-hidden="true">✕</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="tag-entry">
        <input
          id={id}
          type="text"
          autoComplete="off"
          value={input}
          aria-invalid={error ? true : undefined}
          aria-describedby={[hintId, error ? `${id}-error` : undefined].filter(Boolean).join(" ")}
          onChange={(e) => onInput(e.target.value)}
          onKeyDown={onKeyDown}
        />
        <button type="button" className="button" onClick={onCommit}>
          <FormattedMessage {...messages.addTag} />
        </button>
      </div>
      <p id={hintId} className="hint">
        <FormattedMessage {...messages.tagsHint} />
      </p>
      {error && (
        <p id={`${id}-error`} className="field-error" role="alert">
          {error}
        </p>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- detail

export function PracticeDetailPage() {
  const { sessionId } = useParams<{ sessionId: string }>();
  const session = usePracticeSession(sessionId!);
  const intl = useIntl();
  const api = useApi();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const online = useOnline();
  const [confirming, setConfirming] = useState(false);

  const remove = useMutation({
    mutationFn: async (id: string) =>
      unwrap(await api.DELETE("/me/practice-sessions/{id}", { params: { path: { id } } })),
    onSuccess: (_, id) => {
      queryClient.removeQueries({ queryKey: queryKeys.practiceSession(id) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.practiceList });
      navigate("/practice", { replace: true });
    },
  });

  const back = (
    <Link to="/practice" className="back-link">
      <span aria-hidden="true">‹ </span>
      <FormattedMessage {...messages.backToPractice} />
    </Link>
  );

  if (session.isPending) {
    return (
      <section className="page">
        {back}
        <Skeleton blocks={2} />
      </section>
    );
  }

  if (session.isError) {
    return (
      <section className="page">
        {back}
        {session.error instanceof ApiProblem && session.error.status === 404 ? (
          <div className="empty-state" role="status">
            <p>
              <FormattedMessage {...messages.practiceNoLongerExists} />
            </p>
          </div>
        ) : (
          <LoadError message={messages.practiceSessionLoadError} onRetry={() => void session.refetch()} />
        )}
      </section>
    );
  }

  const s = session.data;

  return (
    <section className="page practice-detail">
      {back}
      <h1>
        <FormattedMessage {...sessionTypeLabel(s.sessionType)} />
      </h1>
      <p className="round-summary">{formatPlayedDate(intl, s.practicedAt)}</p>

      <section className="card">
        <dl className="stat-list">
          {s.durationMinutes !== null && (
            <Entry label={messages.duration}>
              <FormattedMessage {...messages.durationMinutes} values={{ minutes: s.durationMinutes }} />
            </Entry>
          )}
          {s.venueName && <Entry label={messages.venue}>{s.venueName}</Entry>}
          {s.primaryGoal && (
            <Entry label={messages.goal} wide>
              {s.primaryGoal}
            </Entry>
          )}
          {s.tags.length > 0 && (
            <Entry label={messages.tags} wide>
              <ul className="tag-chips">
                {s.tags.map((tag) => (
                  <li key={tag} className="tag-chip">
                    {tag}
                  </li>
                ))}
              </ul>
            </Entry>
          )}
          {s.notes && (
            <Entry label={messages.notes} wide>
              {s.notes}
            </Entry>
          )}
        </dl>
      </section>

      <button
        type="button"
        className="button button--danger-outline"
        disabled={!online}
        onClick={() => setConfirming(true)}
      >
        <FormattedMessage {...messages.deletePractice} />
      </button>

      {confirming && (
        <ConfirmDialog
          title={messages.deletePracticeTitle}
          body={messages.deletePracticeBody}
          confirm={messages.delete}
          busyLabel={messages.deleting}
          busy={remove.isPending}
          error={remove.isError ? intl.formatMessage(messages.deletePracticeFailed) : undefined}
          onConfirm={() => remove.mutate(s.practiceSessionId)}
          onCancel={() => setConfirming(false)}
        />
      )}
    </section>
  );
}

function Entry({ label, wide = false, children }: { label: PlainMessage; wide?: boolean; children: ReactNode }) {
  return (
    <div className={wide ? "stat-list-wide" : undefined}>
      <dt>
        <FormattedMessage {...label} />
      </dt>
      <dd>{children}</dd>
    </div>
  );
}
