import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { FormattedMessage, useIntl } from "react-intl";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import { unwrap } from "../api";
import { queryKeys, useCoachInvitation } from "../api/queries";
import { useApi } from "../app/ApiContext";
import { AuthFrame, FormError } from "../auth/AuthPages";
import { LoadError, OfflineBanner, Skeleton, useOnline } from "../common/states";
import { messages } from "../i18n/messages";
import { useSession, useSessionState } from "../session/SessionContext";
import { sharedLabels } from "./CoachesSection";
import { invitationFailure, sharedItems, type InvitationFailure } from "./coachForm";
import "../auth/AuthPages.css";
import "./coaching.css";

/**
 * The page the invitation email links to: /coach-invitation?token=… (#185). Open whether
 * or not someone is signed in, so a signed-out coach learns why they should sign in, and
 * comes back here afterwards. The token stays out of the API's URLs.
 */
export function CoachInvitationPage() {
  const [params] = useSearchParams();
  const token = params.get("token") ?? "";
  const state = useSessionState();

  return (
    <AuthFrame>
      <OfflineBanner />
      <h1>
        <FormattedMessage {...messages.invitationTitle} />
      </h1>
      {token === "" ? (
        <Failure failure="gone" />
      ) : state.status === "restoring" ? (
        <Skeleton blocks={2} />
      ) : state.status === "signedOut" ? (
        <SignedOut />
      ) : (
        <Invitation token={token} />
      )}
    </AuthFrame>
  );
}

function SignedOut() {
  const location = useLocation();
  // Where the sign-in screens return to; the query string holds the token.
  const back = { from: `${location.pathname}${location.search}` };

  return (
    <>
      <p className="auth-intro">
        <FormattedMessage {...messages.invitationSignedOut} />
      </p>
      <div className="auth-actions">
        <Link to="/sign-in" state={back} className="button button--primary">
          <FormattedMessage {...messages.signIn} />
        </Link>
        <Link to="/create-account" state={back} className="button">
          <FormattedMessage {...messages.createAccount} />
        </Link>
      </div>
    </>
  );
}

function Invitation({ token }: { token: string }) {
  const intl = useIntl();
  const api = useApi();
  const online = useOnline();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const preview = useCoachInvitation(token);
  const [declined, setDeclined] = useState(false);

  const accept = useMutation({
    mutationFn: async () => unwrap(await api.POST("/coach-invitations/accept", { body: { token } })),
    // The new student is on Students, and the section is now in the navigation.
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.me });
      navigate("/students", { replace: true });
    },
  });
  const decline = useMutation({
    mutationFn: async () => unwrap(await api.POST("/coach-invitations/decline", { body: { token } })),
    onSuccess: () => setDeclined(true),
  });

  if (declined)
    return (
      <>
        <p className="auth-intro" role="status">
          <FormattedMessage {...messages.invitationDeclined} />
        </p>
        <Link to="/" className="button">
          <FormattedMessage {...messages.backHome} />
        </Link>
      </>
    );

  if (preview.isPending) return <Skeleton blocks={2} />;

  if (preview.isError) {
    const failure = invitationFailure(preview.error);
    return failure === "gone" || failure === "wrongAccount" ? (
      <Failure failure={failure} />
    ) : (
      <LoadError message={messages.invitationLoadError} onRetry={() => void preview.refetch()} />
    );
  }

  // Used or cancelled since this page opened: the same plain messages as on arrival.
  const failed = accept.error ?? decline.error;
  const failure = failed ? invitationFailure(failed) : undefined;
  if (failure === "gone" || failure === "wrongAccount") return <Failure failure={failure} />;

  const busy = accept.isPending || decline.isPending;
  return (
    <>
      <p className="auth-intro">
        <FormattedMessage {...messages.invitationFrom} values={{ name: preview.data.golferDisplayName }} />
      </p>
      <p className="auth-intro">
        <FormattedMessage {...messages.invitationShares} />
      </p>
      <ul className="invitation-shares">
        {sharedItems(preview.data).map((item) => (
          <li key={item}>
            <FormattedMessage {...sharedLabels[item]} />
          </li>
        ))}
      </ul>
      <FormError
        message={
          failure &&
          intl.formatMessage(
            failure === "full"
              ? messages.invitationFull
              : failure === "connection"
                ? messages.cantConnect
                : messages.somethingWentWrong,
          )
        }
      />
      {!online && (
        <p className="hint">
          <FormattedMessage {...messages.offlineCantRespond} />
        </p>
      )}
      <div className="auth-actions">
        <button
          type="button"
          className="button button--primary"
          disabled={busy || !online}
          onClick={() => accept.mutate()}
        >
          <FormattedMessage {...(accept.isPending ? messages.working : messages.acceptInvitation)} />
        </button>
        <button type="button" className="button" disabled={busy || !online} onClick={() => decline.mutate()}>
          <FormattedMessage {...(decline.isPending ? messages.working : messages.declineInvitation)} />
        </button>
      </div>
    </>
  );
}

/** A link that can't be used: why, and for the wrong account, the way to the right one. */
function Failure({ failure }: { failure: Extract<InvitationFailure, "gone" | "wrongAccount"> }) {
  const session = useSession();

  return failure === "gone" ? (
    <>
      <p className="auth-intro" role="alert">
        <FormattedMessage {...messages.invitationGone} />
      </p>
      <Link to="/" className="button">
        <FormattedMessage {...messages.backHome} />
      </Link>
    </>
  ) : (
    <>
      <p className="auth-intro" role="alert">
        <FormattedMessage {...messages.invitationWrongAccount} />
      </p>
      {/* Signed out, this same page offers Sign in, and returns here with the token. */}
      <button type="button" className="button button--primary" onClick={() => void session.signOut().catch(() => {})}>
        <FormattedMessage {...messages.signInAnotherAccount} />
      </button>
    </>
  );
}
