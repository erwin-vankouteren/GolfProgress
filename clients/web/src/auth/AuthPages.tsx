import { useId, useState, type FormEvent, type ReactNode } from "react";
import { FormattedMessage, useIntl, type IntlShape } from "react-intl";
import { Link, useLocation, useNavigate } from "react-router";
import { ApiProblem } from "../api";
import { messages, type PlainMessage } from "../i18n/messages";
import { useSession } from "../session/SessionContext";
import "./AuthPages.css";

/** Welcome, Sign in and Create account (docs/clients/wireframes.md → "Authentication", #69). */

export function WelcomePage() {
  return (
    <AuthFrame>
      <h1>
        <FormattedMessage {...messages.welcomeTitle} />
      </h1>
      <p className="auth-intro">
        <FormattedMessage {...messages.welcomeBody} />
      </p>
      <div className="auth-actions">
        <AuthLink to="/create-account" className="button button--primary">
          <FormattedMessage {...messages.createAccount} />
        </AuthLink>
        <AuthLink to="/sign-in" className="button">
          <FormattedMessage {...messages.signIn} />
        </AuthLink>
      </div>
    </AuthFrame>
  );
}

export function SignInPage() {
  const intl = useIntl();
  const session = useSession();
  const form = useAuthForm(["email", "password"]);

  const submit = form.handleSubmit(async ({ email, password }) => {
    await session.signIn(email!, password!);
  }, intl.formatMessage(messages.wrongCredentials));

  return (
    <AuthFrame>
      <h1>
        <FormattedMessage {...messages.signIn} />
      </h1>
      <form onSubmit={submit} noValidate>
        <Field form={form} name="email" type="email" autoComplete="email" label={messages.email} />
        <Field form={form} name="password" type="password" autoComplete="current-password" label={messages.password} />
        <FormError message={form.formError} />
        <SubmitButton busy={form.busy} label={messages.signIn} />
      </form>
      <AuthLink to="/create-account">
        <FormattedMessage {...messages.needAccount} />
      </AuthLink>
    </AuthFrame>
  );
}

export function CreateAccountPage() {
  const intl = useIntl();
  const session = useSession();
  const form = useAuthForm(["displayName", "email", "password"]);

  const submit = form.handleSubmit(async ({ displayName, email, password }) => {
    await session.register(email!, displayName!, password!);
  }, intl.formatMessage(messages.emailTaken));

  return (
    <AuthFrame>
      <h1>
        <FormattedMessage {...messages.createAccount} />
      </h1>
      <form onSubmit={submit} noValidate>
        <Field form={form} name="displayName" type="text" autoComplete="name" label={messages.name} />
        <Field form={form} name="email" type="email" autoComplete="email" label={messages.email} />
        <Field form={form} name="password" type="password" autoComplete="new-password" label={messages.password} />
        <FormError message={form.formError} />
        <SubmitButton busy={form.busy} label={messages.createAccount} />
      </form>
      <AuthLink to="/sign-in">
        <FormattedMessage {...messages.haveAccount} />
      </AuthLink>
    </AuthFrame>
  );
}

// ---------------------------------------------------------------- form plumbing

type FieldName = "displayName" | "email" | "password";
type Values = Partial<Record<FieldName, string>>;

const fieldLabels = { displayName: messages.name, email: messages.email, password: messages.password };

/**
 * State and submission for a sign-in form: checks required fields and email shape
 * locally, then shows the API's per-field messages and its 401/409/429 as a form error.
 * `conflictMessage` is what a 401 (sign in) or 409 (create account) means here.
 */
function useAuthForm(fields: FieldName[]) {
  const intl = useIntl();
  const navigate = useNavigate();
  const location = useLocation();
  const [values, setValues] = useState<Values>({});
  const [fieldErrors, setFieldErrors] = useState<Values>({});
  const [formError, setFormError] = useState<string | undefined>();
  const [busy, setBusy] = useState(false);

  const handleSubmit =
    (action: (values: Values) => Promise<void>, conflictMessage: string) => async (event: FormEvent) => {
      event.preventDefault();
      setFormError(undefined);

      const errors = validate(fields, values, intl);
      setFieldErrors(errors);
      if (Object.keys(errors).length > 0) return;

      setBusy(true);
      try {
        await action(trimmed(values));
        // Back to where a signed-out user was heading, or Home.
        const from = (location.state as { from?: string } | null)?.from;
        navigate(from && from !== "/welcome" ? from : "/", { replace: true });
      } catch (error) {
        const { field, form } = describe(error, fields, conflictMessage, intl);
        setFieldErrors(field);
        setFormError(form);
      } finally {
        setBusy(false);
      }
    };

  return {
    values,
    fieldErrors,
    formError,
    busy,
    setValue: (name: FieldName, value: string) => setValues((v) => ({ ...v, [name]: value })),
    handleSubmit,
  };
}

type AuthForm = ReturnType<typeof useAuthForm>;

function validate(fields: FieldName[], values: Values, intl: IntlShape): Values {
  const errors: Values = {};
  for (const name of fields) {
    const value = values[name]?.trim() ?? "";
    if (!value) {
      errors[name] = intl.formatMessage(messages.required, { field: intl.formatMessage(fieldLabels[name]) });
    } else if (name === "email" && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
      errors[name] = intl.formatMessage(messages.invalidEmail);
    }
  }
  return errors;
}

function trimmed(values: Values): Values {
  // Passwords are sent exactly as typed.
  return { ...values, email: values.email?.trim(), displayName: values.displayName?.trim() };
}

function describe(
  error: unknown,
  fields: FieldName[],
  conflictMessage: string,
  intl: IntlShape,
): { field: Values; form?: string } {
  if (!(error instanceof ApiProblem)) return { field: {}, form: intl.formatMessage(messages.cantConnect) };

  if (error.status === 400) {
    const field: Values = {};
    for (const name of fields) {
      const message = error.messagesFor(name)[0];
      if (message) field[name] = message;
    }
    return Object.keys(field).length ? { field } : { field, form: intl.formatMessage(messages.somethingWentWrong) };
  }
  if (error.status === 401 || error.status === 409) return { field: {}, form: conflictMessage };
  if (error.status === 429) {
    return {
      field: {},
      form:
        error.retryAfterSeconds !== undefined
          ? intl.formatMessage(messages.tooManyAttempts, { seconds: error.retryAfterSeconds })
          : intl.formatMessage(messages.tooManyAttemptsLater),
    };
  }
  return { field: {}, form: intl.formatMessage(messages.somethingWentWrong) };
}

// ---------------------------------------------------------------- pieces

function AuthFrame({ children }: { children: ReactNode }) {
  return (
    <main className="auth-frame" id="main">
      <div className="auth-card">{children}</div>
    </main>
  );
}

function Field({
  form,
  name,
  type,
  autoComplete,
  label,
}: {
  form: AuthForm;
  name: FieldName;
  type: string;
  autoComplete: string;
  label: PlainMessage;
}) {
  const id = useId();
  const error = form.fieldErrors[name];

  return (
    <div className="auth-field">
      <label htmlFor={id}>
        <FormattedMessage {...label} />
      </label>
      <input
        id={id}
        name={name}
        type={type}
        autoComplete={autoComplete}
        value={form.values[name] ?? ""}
        onChange={(e) => form.setValue(name, e.target.value)}
        aria-invalid={error ? true : undefined}
        aria-describedby={error ? `${id}-error` : undefined}
      />
      {error && (
        <p id={`${id}-error`} className="auth-field-error">
          {error}
        </p>
      )}
    </div>
  );
}

function FormError({ message }: { message?: string }) {
  // role="alert" so screen readers announce it when it appears.
  return message ? (
    <p className="auth-form-error" role="alert">
      {message}
    </p>
  ) : null;
}

function SubmitButton({ busy, label }: { busy: boolean; label: PlainMessage }) {
  return (
    <button type="submit" className="button button--primary auth-submit" disabled={busy} aria-busy={busy}>
      <FormattedMessage {...(busy ? messages.working : label)} />
    </button>
  );
}

/** A link between the sign-in screens that keeps where the user was heading. */
function AuthLink({ to, className, children }: { to: string; className?: string; children: ReactNode }) {
  return (
    <Link to={to} state={useLocation().state} className={className}>
      {children}
    </Link>
  );
}
