import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState, type FormEvent, type ReactNode } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { ApiProblem, unwrap, type Schemas } from "../api";
import { queryKeys, useReflections } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import {
  emptyReflection,
  hasContent,
  reflectionTextMax,
  tooLong,
  toReflectionRequest,
  type Rating,
  type ReflectionText,
  type ReflectionValues,
} from "./reflectionForm";

/**
 * A round's reflections, and adding one inline (#99): how focus and mood were, whether
 * frustration carried over, what worked, what to repeat and one change for next time.
 */
export function ReflectionSection({ roundId, Heading }: { roundId: string; Heading: "h2" | "h3" }) {
  const intl = useIntl();
  const reflections = useReflections(roundId);
  const [adding, setAdding] = useState(false);
  const [justSaved, setJustSaved] = useState(false);
  const addButton = useRef<HTMLButtonElement>(null);

  const close = (saved: boolean) => {
    setAdding(false);
    setJustSaved(saved);
    // Back to where the user started, once the button is there again.
    requestAnimationFrame(() => addButton.current?.focus());
  };

  // Newest first.
  const list = [...(reflections.data ?? [])].reverse();

  return (
    <section className="card" aria-label={intl.formatMessage(messages.reflection)}>
      <Heading>
        <FormattedMessage {...messages.reflection} />
      </Heading>

      {reflections.isPending ? (
        <Skeleton blocks={1} />
      ) : reflections.isError ? (
        <LoadError message={messages.reflectionLoadError} onRetry={() => void reflections.refetch()} />
      ) : (
        <>
          {list.length === 0 && !adding && (
            <p>
              <FormattedMessage {...messages.noReflection} />
            </p>
          )}
          {list.map((reflection) => (
            <SavedReflection key={reflection.reflectionId} reflection={reflection} />
          ))}
        </>
      )}

      {justSaved && (
        <p className="hint" role="status">
          <FormattedMessage {...messages.reflectionSaved} />
        </p>
      )}

      {adding ? (
        <ReflectionForm roundId={roundId} onDone={close} />
      ) : (
        !reflections.isPending && (
          <div>
            <button
              ref={addButton}
              type="button"
              className="button"
              onClick={() => {
                setJustSaved(false);
                setAdding(true);
              }}
            >
              <FormattedMessage {...(list.length ? messages.addAnotherReflection : messages.addReflection)} />
            </button>
          </div>
        )
      )}
    </section>
  );
}

function SavedReflection({ reflection }: { reflection: Schemas["ReflectionResponse"] }) {
  const intl = useIntl();

  return (
    <div className="saved-reflection">
      <p className="card-detail">
        <FormattedMessage
          {...messages.reflectedOn}
          values={{ date: intl.formatDate(reflection.createdAt, { day: "numeric", month: "short", year: "numeric" }) }}
        />
      </p>
      <dl className="stat-list">
        {reflection.focusRating !== null && (
          <Entry label={messages.focus}>
            <FormattedMessage {...messages.outOfFive} values={{ value: reflection.focusRating }} />
          </Entry>
        )}
        {reflection.moodRating !== null && (
          <Entry label={messages.mood}>
            <FormattedMessage {...messages.outOfFive} values={{ value: reflection.moodRating }} />
          </Entry>
        )}
        {reflection.frustrationSpillover !== null && (
          <Entry label={messages.frustrationCarriedOver}>
            <FormattedMessage {...(reflection.frustrationSpillover ? messages.yes : messages.no)} />
          </Entry>
        )}
        {reflection.whatWorked && (
          <Entry label={messages.whatWorked} wide>
            {reflection.whatWorked}
          </Entry>
        )}
        {reflection.whatToRepeat && (
          <Entry label={messages.whatToRepeat} wide>
            {reflection.whatToRepeat}
          </Entry>
        )}
        {reflection.oneChangeNext && (
          <Entry label={messages.oneChangeNext} wide>
            {reflection.oneChangeNext}
          </Entry>
        )}
      </dl>
    </div>
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

const textLabels: Record<ReflectionText, PlainMessage> = {
  whatWorked: messages.whatWorked,
  whatToRepeat: messages.whatToRepeat,
  oneChangeNext: messages.oneChangeNext,
};

function ReflectionForm({ roundId, onDone }: { roundId: string; onDone: (saved: boolean) => void }) {
  const intl = useIntl();
  const api = useApi();
  const queryClient = useQueryClient();
  const online = useOnline();
  const [values, setValues] = useState<ReflectionValues>(emptyReflection);
  const [formError, setFormError] = useState<string | undefined>();
  const [apiErrors, setApiErrors] = useState<Partial<Record<ReflectionText, string>>>({});
  const form = useRef<HTMLFormElement>(null);
  const sending = useRef(false);

  // Opening the form takes the user to it.
  useEffect(() => {
    form.current?.querySelector<HTMLElement>("input, textarea")?.focus();
  }, []);

  const save = useMutation({
    mutationFn: async (body: Schemas["CreateReflectionRequest"]) =>
      unwrap(await api.POST("/rounds/{roundId}/reflections", { params: { path: { roundId } }, body })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.reflections(roundId) });
      onDone(true);
    },
    onError: (error) => {
      if (error instanceof ApiProblem && error.status === 400) {
        const fieldErrors: Partial<Record<ReflectionText, string>> = {};
        for (const field of ["whatWorked", "whatToRepeat", "oneChangeNext"] as const) {
          const message = error.messagesFor(field)[0];
          if (message) fieldErrors[field] = message;
        }
        setApiErrors(fieldErrors);
        if (Object.keys(fieldErrors).length) return;
      }
      setFormError(
        intl.formatMessage(error instanceof ApiProblem ? messages.reflectionSaveFailed : messages.cantConnect),
      );
    },
    onSettled: () => {
      sending.current = false;
    },
  });

  const set = <K extends keyof ReflectionValues>(field: K, value: ReflectionValues[K]) => {
    setValues((v) => ({ ...v, [field]: value }));
    setFormError(undefined);
    if (field in apiErrors) setApiErrors((e) => ({ ...e, [field]: undefined }));
  };

  const over = tooLong(values);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (sending.current || !online) return;
    if (!hasContent(values)) {
      setFormError(intl.formatMessage(messages.reflectionNeedsSomething));
      return;
    }
    if (over.length) return;

    sending.current = true;
    save.mutate(toReflectionRequest(values));
  };

  return (
    <form ref={form} className="reflection-form" onSubmit={submit} noValidate>
      <p className="hint">
        <FormattedMessage {...messages.reflectionHabitsHint} />
      </p>

      <RatingInput label={messages.focus} value={values.focusRating} onChange={(v) => set("focusRating", v)} />
      <RatingInput label={messages.mood} value={values.moodRating} onChange={(v) => set("moodRating", v)} />

      <fieldset className="choice-group">
        <legend>
          <FormattedMessage {...messages.frustrationQuestion} />
        </legend>
        {[true, false].map((answer) => (
          <label key={String(answer)} className="choice">
            <input
              type="radio"
              name="frustrationSpillover"
              checked={values.frustrationSpillover === answer}
              onChange={() => set("frustrationSpillover", answer)}
            />
            <FormattedMessage {...(answer ? messages.yes : messages.no)} />
          </label>
        ))}
      </fieldset>

      {(["whatWorked", "whatToRepeat", "oneChangeNext"] as const).map((field) => (
        <ReflectionTextField
          key={field}
          label={textLabels[field]}
          value={values[field]}
          error={
            apiErrors[field] ??
            (over.includes(field)
              ? intl.formatMessage(messages.atMostCharacters, { max: reflectionTextMax })
              : undefined)
          }
          onChange={(v) => set(field, v)}
        />
      ))}

      {formError && (
        <p className="form-error" role="alert">
          {formError}
        </p>
      )}
      {!online && (
        <p className="hint" role="status">
          <FormattedMessage {...messages.offlineCantSaveReflection} />
        </p>
      )}

      <div className="reflection-actions">
        <button
          type="submit"
          className="button button--primary"
          disabled={save.isPending || !online}
          aria-busy={save.isPending}
        >
          <FormattedMessage {...(save.isPending ? messages.saving : messages.saveReflection)} />
        </button>
        <button type="button" className="button" onClick={() => onDone(false)} disabled={save.isPending}>
          <FormattedMessage {...messages.cancel} />
        </button>
      </div>
    </form>
  );
}

/**
 * A 1–5 rating as five radio buttons, each read as "4 of 5". Optional, so Clear takes it
 * back to unanswered: a radio group can't be unset otherwise.
 */
function RatingInput({
  label,
  value,
  onChange,
}: {
  label: PlainMessage;
  value: Rating | null;
  onChange: (value: Rating | null) => void;
}) {
  const intl = useIntl();
  const name = useId();

  return (
    <fieldset className="rating">
      <legend>
        <FormattedMessage {...label} />
      </legend>
      <div className="rating-options">
        {([1, 2, 3, 4, 5] as const).map((rating) => (
          <label key={rating} className="rating-option">
            <input
              type="radio"
              name={name}
              value={rating}
              checked={value === rating}
              onChange={() => onChange(rating)}
            />
            <span aria-hidden="true">{rating}</span>
            <span className="visually-hidden">
              <FormattedMessage {...messages.outOfFive} values={{ value: rating }} />
            </span>
          </label>
        ))}
        {value !== null && (
          <button
            type="button"
            className="button rating-clear"
            aria-label={intl.formatMessage(messages.clearNamedRating, { field: intl.formatMessage(label) })}
            onClick={() => onChange(null)}
          >
            <FormattedMessage {...messages.clearRating} />
          </button>
        )}
      </div>
    </fieldset>
  );
}

function ReflectionTextField({
  label,
  value,
  error,
  onChange,
}: {
  label: PlainMessage;
  value: string;
  error?: string;
  onChange: (value: string) => void;
}) {
  const id = useId();

  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
      </label>
      <textarea
        id={id}
        rows={2}
        value={value}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
        onChange={(e) => onChange(e.target.value)}
      />
      {error && (
        <p id={`${id}-error`} className="field-error">
          {error}
        </p>
      )}
    </div>
  );
}
