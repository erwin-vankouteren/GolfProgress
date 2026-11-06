import { useId, type InputHTMLAttributes, type ReactNode } from "react";
import { FormattedMessage } from "react-intl";
import type { PlainMessage } from "../i18n/messages";

/** A titled card on the Profile page. */
export function Section({ title, children }: { title: PlainMessage; children: ReactNode }) {
  const id = useId();
  return (
    <section className="card profile-section" aria-labelledby={id}>
      <h2 id={id}>
        <FormattedMessage {...title} />
      </h2>
      {children}
    </section>
  );
}

/** A labelled input with its hint and error, described by both. */
export function TextField({
  id,
  label,
  hint,
  error,
  ...input
}: {
  id: string;
  label: PlainMessage;
  hint?: PlainMessage;
  error?: string;
} & InputHTMLAttributes<HTMLInputElement>) {
  const describedBy = [hint && `${id}-hint`, error && `${id}-error`].filter(Boolean).join(" ") || undefined;
  return (
    <div className="field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
      </label>
      <input id={id} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...input} />
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
