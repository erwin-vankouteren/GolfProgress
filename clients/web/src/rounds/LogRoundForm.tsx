import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useRef, useState, type FormEvent, type KeyboardEvent, type ReactNode } from "react";
import { FormattedMessage, useIntl, type IntlShape } from "react-intl";
import { ApiProblem, unwrap, type Schemas } from "../api";
import { queryKeys, useMe } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import {
  emptyRound,
  maxFor,
  statFieldsFor,
  step,
  toRequest,
  validateRound,
  type NumberField,
  type RoundError,
  type RoundField,
  type RoundFormValues,
  type StatField,
} from "./roundForm";
import "./rounds.css";

const statLabels: Record<StatField, PlainMessage> = {
  totalPutts: messages.putts,
  penalties: messages.penalties,
  fairwaysHit: messages.fairways,
  gir: messages.greens,
  threePutts: messages.threePutts,
  blowUpHoles: messages.blowUpHoles,
};

/**
 * The Log round form (#92), shown as a page on phones and in a dialog on desktop. It
 * shows the stats for the user's tracking mode, checks the API's rules as the user
 * types, and on Save logs the round, which the API scores in the same request (#80).
 */
export function LogRoundForm({
  headingLevel,
  titleId,
  onSaved,
  onCancel,
}: {
  headingLevel: 1 | 2;
  titleId?: string;
  onSaved: (roundId: string) => void;
  onCancel: () => void;
}) {
  const me = useMe();
  const intl = useIntl();
  const Heading = headingLevel === 1 ? "h1" : "h2";

  return (
    <div className="log-round">
      <div className="log-round-header">
        <Heading id={titleId}>
          <FormattedMessage {...messages.logRound} />
        </Heading>
        <button
          type="button"
          className="icon-button"
          aria-label={intl.formatMessage(messages.close)}
          onClick={onCancel}
        >
          <span aria-hidden="true">✕</span>
        </button>
      </div>

      {me.isPending ? (
        <Skeleton blocks={3} />
      ) : me.isError ? (
        <LoadError message={messages.preferencesLoadError} onRetry={() => void me.refetch()} />
      ) : (
        <RoundFields me={me.data} onSaved={onSaved} />
      )}
    </div>
  );
}

function RoundFields({ me, onSaved }: { me: Schemas["UserResponse"]; onSaved: (roundId: string) => void }) {
  const intl = useIntl();
  const api = useApi();
  const queryClient = useQueryClient();
  const online = useOnline();
  const stats = statFieldsFor(me.trackingMode);

  const [values, setValues] = useState<RoundFormValues>(() => emptyRound());
  const [touched, setTouched] = useState<Partial<Record<RoundField, boolean>>>({});
  const [submitted, setSubmitted] = useState(false);
  const [apiErrors, setApiErrors] = useState<Partial<Record<RoundField, string>>>({});
  const [formError, setFormError] = useState<string | undefined>();
  const formId = useId();
  /** Each field's input id, so Save can move focus to the first one to fix. */
  const idFor = (field: RoundField) => `${formId}-${field}`;
  // Set synchronously, so a second Enter before React re-renders can't send it twice.
  const sending = useRef(false);

  const save = useMutation({
    mutationFn: async (body: Schemas["CreateRoundRequest"]) => unwrap(await api.POST("/me/rounds", { body })),
    onSuccess: (round) => {
      queryClient.setQueryData(queryKeys.round(round.roundId), round);
      void queryClient.invalidateQueries({ queryKey: queryKeys.dashboard });
      void queryClient.invalidateQueries({ queryKey: queryKeys.rounds });
      onSaved(round.roundId);
    },
    onError: (error) => {
      const { field, form } = describeSaveError(error, intl);
      setApiErrors(field);
      setFormError(form);
    },
    onSettled: () => {
      sending.current = false;
    },
  });

  const localErrors = validateRound(values, stats);
  const errorFor = (field: RoundField): string | undefined =>
    apiErrors[field] ?? (touched[field] || submitted ? errorMessage(localErrors[field], intl) : undefined);

  const update = (field: RoundField, value: RoundFormValues[RoundField]) => {
    setValues((v) => ({ ...v, [field]: value }));
    setTouched((t) => ({ ...t, [field]: true }));
    setApiErrors((e) => ({ ...e, [field]: undefined }));
  };

  const onStep = (field: NumberField, delta: 1 | -1) => update(field, step(field, values, delta, me.typical18Score));

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (sending.current || !online) return;

    setSubmitted(true);
    setFormError(undefined);

    const firstInvalid = (Object.keys(localErrors) as RoundField[])[0];
    if (firstInvalid) {
      document.getElementById(idFor(firstInvalid))?.focus();
      return;
    }

    sending.current = true;
    save.mutate(toRequest(values, stats));
  };

  return (
    <form className="log-round-form" onSubmit={submit} noValidate>
      <TextField
        label={messages.date}
        type="date"
        value={values.playedAt}
        error={errorFor("playedAt")}
        id={idFor("playedAt")}
        onChange={(v) => update("playedAt", v)}
      />

      <fieldset className="holes">
        <legend>
          <FormattedMessage {...messages.holes} />
        </legend>
        {([9, 18] as const).map((holes) => (
          <label key={holes} className="holes-option">
            <input
              type="radio"
              name="holes"
              value={holes}
              checked={values.holes === holes}
              onChange={() => update("holes", holes)}
            />
            <FormattedMessage {...messages.holesOption} values={{ count: holes }} />
          </label>
        ))}
      </fieldset>

      <TextField
        label={messages.course}
        value={values.courseName}
        autoComplete="off"
        error={errorFor("courseName")}
        id={idFor("courseName")}
        onChange={(v) => update("courseName", v)}
      />
      <TextField
        label={messages.tee}
        value={values.teeName}
        autoComplete="off"
        error={errorFor("teeName")}
        id={idFor("teeName")}
        onChange={(v) => update("teeName", v)}
      />

      <Stepper
        label={messages.strokes}
        value={values.totalStrokes}
        error={errorFor("totalStrokes")}
        id={idFor("totalStrokes")}
        onChange={(v) => update("totalStrokes", v)}
        onStep={(d) => onStep("totalStrokes", d)}
      />

      {stats.map((field) => (
        <Stepper
          key={field}
          label={statLabels[field]}
          value={values[field]}
          max={maxFor(field, values)}
          error={errorFor(field)}
          id={idFor(field)}
          onChange={(v) => update(field, v)}
          onStep={(d) => onStep(field, d)}
        />
      ))}

      {me.trackingMode !== "Detailed" && (
        <p className="hint">
          <FormattedMessage {...messages.moreStatsHint} />
        </p>
      )}

      <TextField
        label={messages.notes}
        value={values.notes}
        multiline
        error={errorFor("notes")}
        id={idFor("notes")}
        onChange={(v) => update("notes", v)}
      />

      <div className="log-round-footer">
        {formError && (
          <p className="form-error" role="alert">
            {formError}
          </p>
        )}
        {!online && (
          <p className="hint" role="status">
            <FormattedMessage {...messages.offlineCantSave} />
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
  );
}

// ---------------------------------------------------------------- fields

function FieldShell({
  id,
  label,
  error,
  hint,
  children,
}: {
  id: string;
  label: PlainMessage;
  error?: string;
  hint?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
      </label>
      {children}
      {hint}
      {error && (
        <p id={`${id}-error`} className="field-error">
          {error}
        </p>
      )}
    </div>
  );
}

function TextField({
  label,
  value,
  error,
  type = "text",
  autoComplete,
  multiline = false,
  id,
  onChange,
}: {
  label: PlainMessage;
  value: string;
  error?: string;
  type?: string;
  autoComplete?: string;
  multiline?: boolean;
  id: string;
  onChange: (value: string) => void;
}) {
  const shared = {
    id,
    value,
    "aria-invalid": error ? true : undefined,
    "aria-describedby": error ? `${id}-error` : undefined,
  };

  return (
    <FieldShell id={id} label={label} error={error}>
      {multiline ? (
        <textarea {...shared} rows={3} onChange={(e) => onChange(e.target.value)} />
      ) : (
        <input
          {...shared}

          type={type}
          autoComplete={autoComplete}
          onChange={(e) => onChange(e.target.value)}
        />
      )}
    </FieldShell>
  );
}

/**
 * A whole number with − and + either side, sized for a thumb on the course. The input
 * brings up the numeric keypad and takes the arrow keys, so the buttons stay out of the
 * tab order: keyboard users type or use the arrows, touch users tap.
 */
function Stepper({
  label,
  value,
  max,
  error,
  id,
  onChange,
  onStep,
}: {
  label: PlainMessage;
  value: string;
  max?: number;
  error?: string;
  id: string;
  onChange: (value: string) => void;
  onStep: (delta: 1 | -1) => void;
}) {
  const intl = useIntl();
  const name = intl.formatMessage(label);
  const hintId = `${id}-max`;

  const onKeyDown = (event: KeyboardEvent) => {
    if (event.key === "ArrowUp" || event.key === "ArrowDown") {
      event.preventDefault();
      onStep(event.key === "ArrowUp" ? 1 : -1);
    }
  };

  const describedBy = [error ? `${id}-error` : undefined, max !== undefined ? hintId : undefined]
    .filter(Boolean)
    .join(" ");

  return (
    <FieldShell id={id} label={label} error={error}>
      <div className="stepper">
        <button
          type="button"
          tabIndex={-1}
          className="stepper-button"
          aria-label={intl.formatMessage(messages.decrease, { field: name })}
          onClick={() => onStep(-1)}
        >
          <span aria-hidden="true">−</span>
        </button>
        <input
          id={id}

          type="text"
          inputMode="numeric"
          pattern="[0-9]*"
          autoComplete="off"
          value={value}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy || undefined}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={onKeyDown}
        />
        <button
          type="button"
          tabIndex={-1}
          className="stepper-button"
          aria-label={intl.formatMessage(messages.increase, { field: name })}
          onClick={() => onStep(1)}
        >
          <span aria-hidden="true">+</span>
        </button>
        {max !== undefined && (
          <span id={hintId} className="stepper-max">
            <FormattedMessage {...messages.outOf} values={{ max }} />
          </span>
        )}
      </div>
    </FieldShell>
  );
}

// ---------------------------------------------------------------- messages

function errorMessage(error: RoundError | undefined, intl: IntlShape): string | undefined {
  if (!error) return undefined;
  switch (error.code) {
    case "dateRequired":
      return intl.formatMessage(messages.dateRequired);
    case "strokesRequired":
      return intl.formatMessage(messages.strokesRequired);
    case "wholeNumber":
      return intl.formatMessage(messages.wholeNumber);
    case "strokesPositive":
      return intl.formatMessage(messages.strokesPositive);
    case "notNegative":
      return intl.formatMessage(messages.notNegative);
    case "atMostHoles":
      return intl.formatMessage(messages.atMostHoles, { max: error.max });
    case "atMostStrokes":
      return intl.formatMessage(messages.atMostStrokes, { max: error.max });
  }
}

const formFields: RoundField[] = [
  "playedAt",
  "holes",
  "courseName",
  "teeName",
  "totalStrokes",
  "totalPutts",
  "penalties",
  "fairwaysHit",
  "gir",
  "threePutts",
  "blowUpHoles",
  "notes",
];

/** The API's 400 messages go next to their fields; anything else is one message for the form. */
function describeSaveError(
  error: unknown,
  intl: IntlShape,
): { field: Partial<Record<RoundField, string>>; form?: string } {
  if (!(error instanceof ApiProblem)) return { field: {}, form: intl.formatMessage(messages.cantConnect) };

  if (error.status === 400) {
    const field: Partial<Record<RoundField, string>> = {};
    for (const name of formFields) {
      const message = error.messagesFor(name)[0];
      if (message) field[name] = message;
    }
    if (Object.keys(field).length) return { field };
  }
  return { field: {}, form: intl.formatMessage(messages.roundSaveFailed) };
}
