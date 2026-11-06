import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useId, useState, type FormEvent } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { ApiProblem, unwrap, type Schemas } from "../api";
import { queryKeys, useCoaches } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { LoadError, Skeleton, useOnline } from "../common/states";
import { messages, type PlainMessage } from "../i18n/messages";
import { Section, TextField } from "../profile/fields";
import { checkCoachEmail, sharedItems, type SharedItem, type Sharing } from "./coachForm";
import "./coaching.css";

export const sharedLabels: Record<SharedItem, PlainMessage> = {
  rounds: messages.shareRounds,
  reflections: messages.shareReflections,
  practice: messages.sharePractice,
};

/**
 * Profile → Coaches (#185, docs/coach-access.md → "Screens"): who coaches you, who's been
 * invited, and Invite a coach. Changing what a coach sees and removing them is #190.
 */
export function CoachesSection() {
  const coaches = useCoaches();

  return (
    <Section title={messages.coachesTitle}>
      {coaches.isPending ? (
        <Skeleton blocks={2} />
      ) : coaches.isError ? (
        <LoadError message={messages.coachesLoadError} onRetry={() => void coaches.refetch()} />
      ) : coaches.data.coaches.length === 0 && coaches.data.invitations.length === 0 ? (
        <p>
          <FormattedMessage {...messages.coachesEmpty} />
        </p>
      ) : (
        <>
          <ActiveCoaches coaches={coaches.data.coaches} />
          <PendingInvitations invitations={coaches.data.invitations} />
        </>
      )}
      <InviteForm />
    </Section>
  );
}

function useSharedText() {
  const intl = useIntl();
  return (sharing: Sharing) =>
    intl.formatMessage(messages.coachSees, {
      items: intl.formatList(sharedItems(sharing).map((item) => intl.formatMessage(sharedLabels[item]))),
    });
}

function ActiveCoaches({ coaches }: { coaches: Schemas["CoachResponse"][] }) {
  const sharedText = useSharedText();
  if (coaches.length === 0) return null;

  return (
    <ul className="coach-list">
      {coaches.map((coach) => (
        <li key={coach.coachLinkId}>
          <div>
            <strong>{coach.displayName}</strong>
            <p className="hint">{sharedText(coach)}</p>
          </div>
        </li>
      ))}
    </ul>
  );
}

function PendingInvitations({ invitations }: { invitations: Schemas["CoachInvitationResponse"][] }) {
  if (invitations.length === 0) return null;

  return (
    <div className="account-block">
      <h3>
        <FormattedMessage {...messages.coachesPending} />
      </h3>
      <ul className="coach-list">
        {invitations.map((invitation) => (
          <PendingInvitation key={invitation.coachInvitationId} invitation={invitation} />
        ))}
      </ul>
    </div>
  );
}

function PendingInvitation({ invitation }: { invitation: Schemas["CoachInvitationResponse"] }) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const queryClient = useQueryClient();
  const sharedText = useSharedText();

  const cancel = useMutation({
    mutationFn: async () =>
      unwrap(
        await api.DELETE("/me/coach-invitations/{id}", {
          params: { path: { id: invitation.coachInvitationId } },
        }),
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.coaches }),
    onError: (error) => {
      // Accepted, declined or expired meanwhile: the list shows what's true now.
      if (error instanceof ApiProblem && error.status === 404)
        void queryClient.invalidateQueries({ queryKey: queryKeys.coaches });
    },
  });

  return (
    <li>
      <div>
        <strong>{invitation.email}</strong>
        <p className="hint">
          <FormattedMessage
            {...messages.invitationExpires}
            values={{ date: intl.formatDate(invitation.expiresAt, { dateStyle: "medium" }) }}
          />
        </p>
        <p className="hint">{sharedText(invitation)}</p>
        {cancel.isError && (
          <p className="form-error" role="alert">
            <FormattedMessage
              {...(cancel.error instanceof ApiProblem ? messages.cancelInvitationFailed : messages.cantConnect)}
            />
          </p>
        )}
      </div>
      <button
        type="button"
        className="button"
        disabled={cancel.isPending || !online}
        aria-label={intl.formatMessage(messages.cancelInvitationOf, { email: invitation.email })}
        onClick={() => cancel.mutate()}
      >
        <FormattedMessage {...messages.cancel} />
      </button>
    </li>
  );
}

function InviteForm() {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const queryClient = useQueryClient();
  const id = useId();
  const [email, setEmail] = useState("");
  const [sharing, setSharing] = useState<Sharing>({ shareReflections: true, sharePractice: true });
  const [emailError, setEmailError] = useState<string>();
  const [formError, setFormError] = useState<string>();

  const invite = useMutation({
    mutationFn: async (body: Schemas["CreateCoachInvitationRequest"]) =>
      unwrap(await api.POST("/me/coach-invitations", { body })),
    onSuccess: () => {
      setEmail("");
      setSharing({ shareReflections: true, sharePractice: true });
      void queryClient.invalidateQueries({ queryKey: queryKeys.coaches });
    },
    onError: (error) => {
      const onEmail = error instanceof ApiProblem && error.status === 400 && error.messagesFor("email")[0];
      if (onEmail) {
        setEmailError(onEmail);
        document.getElementById(`${id}-email`)?.focus();
      } else if (error instanceof ApiProblem && error.status === 429) {
        setFormError(intl.formatMessage(messages.tooManyInvitations));
      } else {
        setFormError(intl.formatMessage(error instanceof ApiProblem ? messages.inviteFailed : messages.cantConnect));
      }
    },
  });

  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (invite.isPending || !online) return;
    setFormError(undefined);
    invite.reset();
    const problem = checkCoachEmail(email);
    if (problem) {
      setEmailError(
        problem === "required"
          ? intl.formatMessage(messages.required, { field: intl.formatMessage(messages.coachEmail) })
          : intl.formatMessage(messages.invalidEmail),
      );
      document.getElementById(`${id}-email`)?.focus();
      return;
    }
    setEmailError(undefined);
    invite.mutate({ email: email.trim(), ...sharing });
  };

  const sent = invite.data
    ? intl.formatMessage(invite.data.resent ? messages.invitationResent : messages.invitationSent, {
        email: invite.data.email,
      })
    : "";

  return (
    <form className="profile-form account-block" onSubmit={submit} noValidate aria-labelledby={`${id}-title`}>
      <h3 id={`${id}-title`}>
        <FormattedMessage {...messages.inviteCoach} />
      </h3>
      <TextField
        id={`${id}-email`}
        label={messages.coachEmail}
        type="email"
        autoComplete="off"
        value={email}
        error={emailError}
        onChange={(e) => {
          setEmail(e.target.value);
          setEmailError(undefined);
        }}
      />
      <fieldset className="choice-group">
        <legend className="choice-legend">
          <FormattedMessage {...messages.whatCoachSees} />
        </legend>
        <ShareChoice label={messages.shareRounds} hint={messages.shareRoundsHint} checked disabled />
        <ShareChoice
          label={messages.shareReflections}
          hint={messages.shareReflectionsHint}
          checked={sharing.shareReflections}
          onChange={(shareReflections) => setSharing((s) => ({ ...s, shareReflections }))}
        />
        <ShareChoice
          label={messages.sharePractice}
          hint={messages.sharePracticeHint}
          checked={sharing.sharePractice}
          onChange={(sharePractice) => setSharing((s) => ({ ...s, sharePractice }))}
        />
        <p className="hint coach-never-shared">
          <FormattedMessage {...messages.neverShared} />
        </p>
      </fieldset>
      {formError && (
        <p className="form-error" role="alert">
          {formError}
        </p>
      )}
      <p className="visually-hidden" role="status">
        {sent}
      </p>
      {sent && (
        <p className="save-confirmation" aria-hidden="true">
          {sent}
        </p>
      )}
      {!online && (
        <p className="hint">
          <FormattedMessage {...messages.offlineCantInvite} />
        </p>
      )}
      <div>
        <button type="submit" className="button button--primary" disabled={invite.isPending || !online}>
          <FormattedMessage {...(invite.isPending ? messages.working : messages.sendInvitation)} />
        </button>
      </div>
    </form>
  );
}

function ShareChoice({
  label,
  hint,
  checked,
  disabled,
  onChange,
}: {
  label: PlainMessage;
  hint: PlainMessage;
  checked: boolean;
  disabled?: boolean;
  onChange?: (checked: boolean) => void;
}) {
  return (
    <label className="choice">
      <input
        type="checkbox"
        checked={checked}
        disabled={disabled}
        readOnly={!onChange}
        onChange={(e) => onChange?.(e.target.checked)}
      />
      <span className="choice-label">
        <FormattedMessage {...label} />
        <span className="hint">
          <FormattedMessage {...hint} />
        </span>
      </span>
    </label>
  );
}
