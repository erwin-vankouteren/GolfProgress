import { useMutation } from "@tanstack/react-query";
import { useId, useState, type FormEvent, type InputHTMLAttributes, type ReactNode } from "react";
import { FormattedMessage, useIntl, type IntlShape } from "react-intl";
import { useNavigate } from "react-router";
import { ApiProblem, unwrap } from "../api";
import { useMe } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { ConfirmDialog } from "../common/ConfirmDialog";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { useSession } from "../session/SessionContext";
import { GoalFocusField, SaveStatus, TrackingModeField, UnitsField, useUpdateMe } from "./PreferenceFields";
import {
  checkNewPassword,
  preferencesOf,
  profileValuesOf,
  toProfileRequest,
  validateProfile,
  type Me,
  type PasswordError,
  type Preferences,
  type ProfileError,
  type ProfileField,
  type ProfileValues,
} from "./profileForm";
import "../rounds/rounds.css";
import "./profile.css";

/**
 * Profile (#111, docs/clients/ux-design.md → "Profile"): your details, preferences,
 * the account (password, sign out, delete) and About, as sections of one page.
 */
export function ProfilePage() {
  const me = useMe();

  return (
    <section className="page profile">
      <h1>
        <FormattedMessage {...messages.profile} />
      </h1>
      {me.isPending ? (
        <Skeleton blocks={3} className="profile-skeleton" />
      ) : me.isError ? (
        <LoadError message={messages.profileLoadError} onRetry={() => void me.refetch()} />
      ) : (
        <>
          <DetailsSection me={me.data} />
          <PreferencesSection me={me.data} />
        </>
      )}
      {/* These need nothing from GET /me, so signing out or deleting the account works
          even when the profile can't load. */}
      <AccountSection email={me.data?.email ?? null} />
      <AboutSection />
    </section>
  );
}

function Section({ title, children }: { title: PlainMessage; children: ReactNode }) {
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
function TextField({
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

const saveFailed = (error: unknown, intl: IntlShape) =>
  intl.formatMessage(error instanceof ApiProblem ? messages.profileSaveFailed : messages.cantConnect);

function OfflineHint() {
  return useOnline() ? null : (
    <p className="hint">
      <FormattedMessage {...messages.offlineCantSaveProfile} />
    </p>
  );
}

// ---------------------------------------------------------------- details

const profileFieldLabels: Record<ProfileField, PlainMessage> = {
  displayName: messages.name,
  handicapIndex: messages.handicapIndex,
  typical18Score: messages.typical18Score,
};

function profileErrorText(error: ProfileError | undefined, field: ProfileField, intl: IntlShape) {
  if (!error) return undefined;
  switch (error.kind) {
    case "required":
      return intl.formatMessage(messages.required, { field: intl.formatMessage(profileFieldLabels[field]) });
    case "tooLong":
      return intl.formatMessage(messages.atMostCharacters, { max: error.max });
    case "notANumber":
      return intl.formatMessage(messages.enterANumber);
    case "notWhole":
      return intl.formatMessage(messages.wholeNumber);
    case "outOfRange":
      return intl.formatMessage(messages.numberRange, { min: error.min, max: error.max });
  }
}

function DetailsSection({ me }: { me: Me }) {
  const intl = useIntl();
  const online = useOnline();
  const id = useId();
  const update = useUpdateMe();
  const [values, setValues] = useState<ProfileValues>(() => profileValuesOf(me));
  const [submitted, setSubmitted] = useState(false);
  const [apiErrors, setApiErrors] = useState<Partial<Record<ProfileField, string>>>({});
  const [formError, setFormError] = useState<string>();

  const local = validateProfile(values);
  const errorFor = (field: ProfileField) =>
    apiErrors[field] ?? (submitted ? profileErrorText(local[field], field, intl) : undefined);

  const change = (field: ProfileField, value: string) => {
    setValues((v) => ({ ...v, [field]: value }));
    setApiErrors((e) => ({ ...e, [field]: undefined }));
    update.reset();
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (update.isPending || !online) return;
    setSubmitted(true);
    setFormError(undefined);
    const firstInvalid = (Object.keys(local) as ProfileField[])[0];
    if (firstInvalid) {
      document.getElementById(`${id}-${firstInvalid}`)?.focus();
      return;
    }
    update.mutate(toProfileRequest(values), {
      onError: (error) => {
        const fields: Partial<Record<ProfileField, string>> = {};
        if (error instanceof ApiProblem && error.status === 400)
          for (const field of Object.keys(profileFieldLabels) as ProfileField[]) {
            const message = error.messagesFor(field)[0];
            if (message) fields[field] = message;
          }
        setApiErrors(fields);
        if (Object.keys(fields).length === 0) setFormError(saveFailed(error, intl));
      },
    });
  };

  return (
    <Section title={messages.yourDetails}>
      <form className="profile-form" onSubmit={submit} noValidate>
        <TextField
          id={`${id}-displayName`}
          label={messages.name}
          type="text"
          autoComplete="name"
          value={values.displayName}
          error={errorFor("displayName")}
          onChange={(e) => change("displayName", e.target.value)}
        />
        <TextField
          id={`${id}-email`}
          label={messages.email}
          hint={messages.emailHint}
          type="email"
          value={me.email ?? ""}
          readOnly
        />
        <TextField
          id={`${id}-handicapIndex`}
          label={messages.handicapIndex}
          hint={messages.optionalHint}
          type="text"
          inputMode="decimal"
          autoComplete="off"
          value={values.handicapIndex}
          error={errorFor("handicapIndex")}
          onChange={(e) => change("handicapIndex", e.target.value)}
        />
        <TextField
          id={`${id}-typical18Score`}
          label={messages.typical18Score}
          hint={messages.optionalHint}
          type="text"
          inputMode="numeric"
          autoComplete="off"
          value={values.typical18Score}
          error={errorFor("typical18Score")}
          onChange={(e) => change("typical18Score", e.target.value)}
        />
        <SaveStatus saved={update.isSuccess} error={formError} />
        <OfflineHint />
        <div>
          <button type="submit" className="button button--primary" disabled={update.isPending || !online}>
            <FormattedMessage {...(update.isPending ? messages.saving : messages.save)} />
          </button>
        </div>
      </form>
    </Section>
  );
}

// ---------------------------------------------------------------- preferences

function PreferencesSection({ me }: { me: Me }) {
  const intl = useIntl();
  const online = useOnline();
  const update = useUpdateMe();
  const [values, setValues] = useState<Preferences>(() => preferencesOf(me));
  const [formError, setFormError] = useState<string>();

  const change = (next: Partial<Preferences>) => {
    setValues((v) => ({ ...v, ...next }));
    update.reset();
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (update.isPending || !online) return;
    setFormError(undefined);
    update.mutate(values, { onError: (error) => setFormError(saveFailed(error, intl)) });
  };

  return (
    <Section title={messages.preferences}>
      <form className="profile-form" onSubmit={submit} noValidate>
        <UnitsField value={values.units} onChange={(units) => change({ units })} />
        <TrackingModeField value={values.trackingMode} onChange={(trackingMode) => change({ trackingMode })} />
        <GoalFocusField value={values.goalFocus} onChange={(goalFocus) => change({ goalFocus })} />
        <SaveStatus saved={update.isSuccess} error={formError} />
        <OfflineHint />
        <div>
          <button type="submit" className="button button--primary" disabled={update.isPending || !online}>
            <FormattedMessage {...(update.isPending ? messages.saving : messages.save)} />
          </button>
        </div>
      </form>
    </Section>
  );
}

// ---------------------------------------------------------------- account

function passwordErrorText(error: PasswordError | undefined, intl: IntlShape, label: PlainMessage) {
  if (!error) return undefined;
  switch (error.kind) {
    case "required":
      return intl.formatMessage(messages.required, { field: intl.formatMessage(label) });
    case "tooShort":
      return intl.formatMessage(messages.passwordTooShort, { min: error.min });
    case "tooLong":
      return intl.formatMessage(messages.atMostCharacters, { max: error.max });
    case "onlySpaces":
      return intl.formatMessage(messages.passwordOnlySpaces);
    case "isEmail":
      return intl.formatMessage(messages.passwordIsEmail);
    case "unchanged":
      return intl.formatMessage(messages.passwordUnchanged);
  }
}

/** A 429's wait, or the generic failure. */
function accountFailure(error: unknown, intl: IntlShape, generic: PlainMessage) {
  if (error instanceof ApiProblem && error.status === 429)
    return error.retryAfterSeconds !== undefined
      ? intl.formatMessage(messages.tooManyAttempts, { seconds: error.retryAfterSeconds })
      : intl.formatMessage(messages.tooManyAttemptsLater);
  return intl.formatMessage(error instanceof ApiProblem ? generic : messages.cantConnect);
}

function AccountSection({ email }: { email: string | null }) {
  return (
    <Section title={messages.account}>
      <ChangePasswordForm email={email} />
      <SignOut />
      <DeleteAccount />
    </Section>
  );
}

function ChangePasswordForm({ email }: { email: string | null }) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const id = useId();
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [apiErrors, setApiErrors] = useState<{ current?: string; next?: string }>({});
  const [formError, setFormError] = useState<string>();

  const change = useMutation({
    mutationFn: async (body: { currentPassword: string; newPassword: string }) =>
      unwrap(await api.POST("/me/password", { body })),
    onSuccess: () => {
      setCurrent("");
      setNext("");
      setSubmitted(false);
    },
    onError: (error) => {
      // A wrong current password is a 400 on its field: the user stays signed in.
      if (error instanceof ApiProblem && error.status === 400) {
        const fields = {
          current: error.messagesFor("currentPassword")[0],
          next: error.messagesFor("newPassword")[0],
        };
        setApiErrors(fields);
        if (fields.current || fields.next) return;
      }
      setFormError(accountFailure(error, intl, messages.passwordChangeFailed));
    },
  });

  const currentError =
    apiErrors.current ??
    (submitted && current === ""
      ? intl.formatMessage(messages.required, { field: intl.formatMessage(messages.currentPassword) })
      : undefined);
  const nextError =
    apiErrors.next ??
    (submitted ? passwordErrorText(checkNewPassword(next, current, email), intl, messages.newPassword) : undefined);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (change.isPending || !online) return;
    setSubmitted(true);
    setFormError(undefined);
    change.reset();
    if (current === "") return document.getElementById(`${id}-current`)?.focus();
    if (checkNewPassword(next, current, email)) return document.getElementById(`${id}-new`)?.focus();
    change.mutate({ currentPassword: current, newPassword: next });
  };

  return (
    <form className="profile-form account-block" onSubmit={submit} noValidate aria-labelledby={`${id}-title`}>
      <h3 id={`${id}-title`}>
        <FormattedMessage {...messages.changePassword} />
      </h3>
      <TextField
        id={`${id}-current`}
        label={messages.currentPassword}
        type="password"
        autoComplete="current-password"
        value={current}
        error={currentError}
        onChange={(e) => {
          setCurrent(e.target.value);
          setApiErrors((a) => ({ ...a, current: undefined }));
        }}
      />
      <TextField
        id={`${id}-new`}
        label={messages.newPassword}
        hint={messages.newPasswordHint}
        type="password"
        autoComplete="new-password"
        value={next}
        error={nextError}
        onChange={(e) => {
          setNext(e.target.value);
          setApiErrors((a) => ({ ...a, next: undefined }));
        }}
      />
      {formError && (
        <p className="form-error" role="alert">
          {formError}
        </p>
      )}
      <p className="visually-hidden" role="status">
        {change.isSuccess ? intl.formatMessage(messages.passwordChanged) : ""}
      </p>
      {change.isSuccess && (
        <p className="save-confirmation" aria-hidden="true">
          <FormattedMessage {...messages.passwordChanged} />
        </p>
      )}
      <OfflineHint />
      <div>
        <button type="submit" className="button" disabled={change.isPending || !online}>
          <FormattedMessage {...(change.isPending ? messages.saving : messages.changePassword)} />
        </button>
      </div>
    </form>
  );
}

function SignOut() {
  const session = useSession();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);

  const signOut = async () => {
    setBusy(true);
    try {
      await session.signOut();
    } catch {
      // API unreachable: the session is forgotten here anyway, and the cookie expires.
    }
    navigate("/welcome", { replace: true });
  };

  return (
    <div className="account-block">
      <button type="button" className="button" onClick={signOut} disabled={busy}>
        <FormattedMessage {...messages.signOut} />
      </button>
      <p className="hint">
        <FormattedMessage {...messages.signOutHint} />
      </p>
    </div>
  );
}

/**
 * Delete account (#84): explains what goes, asks for the password, deletes, then
 * signs out here (which also clears the web refresh cookie) and returns to Welcome.
 */
function DeleteAccount() {
  const intl = useIntl();
  const api = useApi();
  const session = useSession();
  const navigate = useNavigate();
  const online = useOnline();
  const id = useId();
  const [open, setOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();

  const remove = useMutation({
    mutationFn: async (body: { password: string }) => unwrap(await api.DELETE("/me", { body })),
    onSuccess: async () => {
      try {
        await session.signOut();
      } catch {
        // The account is gone either way; the cookie no longer refreshes anything.
      }
      navigate("/welcome", { replace: true });
    },
    onError: (failure) => {
      const wrong = failure instanceof ApiProblem && failure.status === 400 && failure.messagesFor("password")[0];
      setError(wrong || accountFailure(failure, intl, messages.deleteAccountFailed));
    },
  });

  const confirm = () => {
    setError(undefined);
    if (password === "") {
      setError(intl.formatMessage(messages.required, { field: intl.formatMessage(messages.password) }));
      document.getElementById(`${id}-password`)?.focus();
      return;
    }
    remove.mutate({ password });
  };

  return (
    <div className="account-block">
      <button
        type="button"
        className="button button--danger-outline"
        disabled={!online}
        onClick={() => {
          setPassword("");
          setError(undefined);
          setOpen(true);
        }}
      >
        <FormattedMessage {...messages.deleteAccount} />
      </button>
      <p className="hint">
        <FormattedMessage {...messages.deleteAccountHint} />
      </p>
      {open && (
        <ConfirmDialog
          title={messages.deleteAccountTitle}
          body={messages.deleteAccountBody}
          confirm={messages.deleteMyAccount}
          busyLabel={messages.deletingAccount}
          busy={remove.isPending}
          error={error}
          onConfirm={confirm}
          onCancel={() => setOpen(false)}
        >
          <div className="field">
            <label htmlFor={`${id}-password`}>
              <FormattedMessage {...messages.password} />
            </label>
            <input
              id={`${id}-password`}
              type="password"
              autoComplete="current-password"
              value={password}
              aria-invalid={error ? true : undefined}
              onChange={(e) => setPassword(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter") {
                  e.preventDefault();
                  if (!remove.isPending) confirm();
                }
              }}
            />
          </div>
        </ConfirmDialog>
      )}
    </div>
  );
}

// ---------------------------------------------------------------- about

function AboutSection() {
  const privacy = import.meta.env.VITE_PRIVACY_POLICY_URL;
  const terms = import.meta.env.VITE_TERMS_URL;
  return (
    <Section title={messages.about}>
      <p>
        <FormattedMessage {...messages.appVersion} values={{ version: __APP_VERSION__ }} />
      </p>
      {(privacy || terms) && (
        <ul className="about-links">
          {privacy && (
            <li>
              <a href={privacy} target="_blank" rel="noreferrer">
                <FormattedMessage {...messages.privacyPolicy} />
              </a>
            </li>
          )}
          {terms && (
            <li>
              <a href={terms} target="_blank" rel="noreferrer">
                <FormattedMessage {...messages.termsOfUse} />
              </a>
            </li>
          )}
        </ul>
      )}
    </Section>
  );
}
