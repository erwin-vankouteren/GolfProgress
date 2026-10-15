import { useState } from "react";
import { FormattedMessage } from "react-intl";
import { useNavigate } from "react-router";
import { messages } from "../i18n/messages";
import { useSession } from "../session/SessionContext";

/** Profile (#75); sign out (#69). Preferences and account settings arrive in M2. */
export function ProfilePage() {
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
    <section className="page">
      <h1>
        <FormattedMessage {...messages.profile} />
      </h1>
      <div className="empty-state">
        <p>
          <FormattedMessage {...messages.profileIntro} />
        </p>
        <button type="button" className="button" onClick={signOut} disabled={busy}>
          <FormattedMessage {...messages.signOut} />
        </button>
      </div>
    </section>
  );
}
