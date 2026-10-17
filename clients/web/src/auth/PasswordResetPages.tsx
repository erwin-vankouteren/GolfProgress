import { useId, useState, type FormEvent } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { Link, useSearchParams } from "react-router";
import { ApiProblem, unwrap } from "../api";
import { useApi } from "../app/ApiContext";
import { messages } from "../i18n/messages";
import { checkNewPassword, passwordLength } from "../profile/profileForm";
import { AuthFrame, FormError, SubmitButton } from "./AuthPages";
import "./AuthPages.css";

/**
 * Password reset by email (#86): asking for a link, and choosing a new password from it.
 * Asking always says the same thing, whether or not the address has an account.
 */

const emailShape = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function ForgotPasswordPage() {
  const intl = useIntl();
  const api = useApi();
  const id = useId();
  const [email, setEmail] = useState("");
  const [error, setError] = useState<string>();
  const [formError, setFormError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(undefined);
    const trimmed = email.trim();
    if (!emailShape.test(trimmed)) {
      setError(
        intl.formatMessage(trimmed === "" ? messages.required : messages.invalidEmail, {
          field: intl.formatMessage(messages.email),
        }),
      );
      document.getElementById(id)?.focus();
      return;
    }
    setBusy(true);
    try {
      unwrap(await api.POST("/auth/password-reset", { body: { email: trimmed } }));
      setSent(true);
    } catch (e) {
      setFormError(
        intl.formatMessage(
          e instanceof ApiProblem && e.status === 429
            ? messages.tooManyAttemptsLater
            : e instanceof ApiProblem
              ? messages.somethingWentWrong
              : messages.cantConnect,
        ),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthFrame>
      <h1>
        <FormattedMessage {...messages.resetPasswordTitle} />
      </h1>
      {sent ? (
        <>
          <p className="auth-intro" role="status">
            <FormattedMessage {...messages.resetLinkSent} values={{ email: email.trim() }} />
          </p>
          <Link to="/sign-in" className="button">
            <FormattedMessage {...messages.backToSignIn} />
          </Link>
        </>
      ) : (
        <>
          <p className="auth-intro">
            <FormattedMessage {...messages.resetPasswordIntro} />
          </p>
          <form onSubmit={submit} noValidate>
            <div className="auth-field">
              <label htmlFor={id}>
                <FormattedMessage {...messages.email} />
              </label>
              <input
                id={id}
                type="email"
                autoComplete="email"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value);
                  setError(undefined);
                }}
                aria-invalid={error ? true : undefined}
                aria-describedby={error ? `${id}-error` : undefined}
              />
              {error && (
                <p id={`${id}-error`} className="auth-field-error">
                  {error}
                </p>
              )}
            </div>
            <FormError message={formError} />
            <SubmitButton busy={busy} label={messages.sendResetLink} />
          </form>
          <Link to="/sign-in">
            <FormattedMessage {...messages.backToSignIn} />
          </Link>
        </>
      )}
    </AuthFrame>
  );
}

/** The page the email links to: /reset-password?token=…. */
export function ResetPasswordPage() {
  const intl = useIntl();
  const api = useApi();
  const id = useId();
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [formError, setFormError] = useState<string>();
  const [linkInvalid, setLinkInvalid] = useState(token === "");
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(undefined);
    const problem = checkNewPassword(password, "", null);
    if (problem) {
      setError(
        problem.kind === "tooShort"
          ? intl.formatMessage(messages.passwordTooShort, { min: problem.min })
          : problem.kind === "tooLong"
            ? intl.formatMessage(messages.atMostCharacters, { max: problem.max })
            : problem.kind === "onlySpaces"
              ? intl.formatMessage(messages.passwordOnlySpaces)
              : intl.formatMessage(messages.required, { field: intl.formatMessage(messages.newPassword) }),
      );
      document.getElementById(id)?.focus();
      return;
    }
    setBusy(true);
    try {
      unwrap(await api.POST("/auth/password-reset/confirm", { body: { token, newPassword: password } }));
      setDone(true);
    } catch (e) {
      if (e instanceof ApiProblem && e.status === 400 && e.messagesFor("token").length) setLinkInvalid(true);
      else if (e instanceof ApiProblem && e.status === 400 && e.messagesFor("newPassword")[0])
        setError(e.messagesFor("newPassword")[0]);
      else
        setFormError(intl.formatMessage(e instanceof ApiProblem ? messages.somethingWentWrong : messages.cantConnect));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthFrame>
      <h1>
        <FormattedMessage {...messages.chooseNewPassword} />
      </h1>
      {done ? (
        <>
          <p className="auth-intro" role="status">
            <FormattedMessage {...messages.passwordResetDone} />
          </p>
          <Link to="/sign-in" className="button button--primary">
            <FormattedMessage {...messages.signIn} />
          </Link>
        </>
      ) : linkInvalid ? (
        <>
          <p className="auth-intro" role="alert">
            <FormattedMessage {...messages.resetLinkInvalid} />
          </p>
          <Link to="/forgot-password" className="button button--primary">
            <FormattedMessage {...messages.askForNewLink} />
          </Link>
        </>
      ) : (
        <form onSubmit={submit} noValidate>
          <div className="auth-field">
            <label htmlFor={id}>
              <FormattedMessage {...messages.newPassword} />
            </label>
            <input
              id={id}
              type="password"
              autoComplete="new-password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                setError(undefined);
              }}
              aria-invalid={error ? true : undefined}
              aria-describedby={`${id}-hint${error ? ` ${id}-error` : ""}`}
            />
            <p id={`${id}-hint`} className="hint">
              <FormattedMessage {...messages.passwordTooShort} values={{ min: passwordLength.min }} />
            </p>
            {error && (
              <p id={`${id}-error`} className="auth-field-error">
                {error}
              </p>
            )}
          </div>
          <FormError message={formError} />
          <SubmitButton busy={busy} label={messages.setNewPassword} />
        </form>
      )}
    </AuthFrame>
  );
}
