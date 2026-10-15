import { createContext, useContext, useSyncExternalStore, type ReactNode } from "react";
import { Navigate, Outlet, useLocation } from "react-router";
import { FormattedMessage } from "react-intl";
import { messages } from "../i18n/messages";
import type { Session, SessionState } from "./session";

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ session, children }: { session: Session; children: ReactNode }) {
  return <SessionContext.Provider value={session}>{children}</SessionContext.Provider>;
}

export function useSession(): Session {
  const session = useContext(SessionContext);
  if (!session) throw new Error("useSession must be used inside <SessionProvider>.");
  return session;
}

/** The current sign-in state; re-renders when it changes. */
export function useSessionState(): SessionState {
  const session = useSession();
  return useSyncExternalStore(session.subscribe, session.getState);
}

/** Wraps the signed-in app: signed-out users go to Welcome, and come back afterwards. */
export function RequireSession() {
  const state = useSessionState();
  const location = useLocation();

  if (state.status === "restoring") return <Restoring />;
  if (state.status === "signedOut") return <Navigate to="/welcome" replace state={{ from: location.pathname }} />;
  return <Outlet />;
}

/** Wraps the sign-in screens: someone already signed in goes to the app instead. */
export function PublicOnly() {
  const state = useSessionState();

  if (state.status === "restoring") return <Restoring />;
  if (state.status === "signedIn") return <Navigate to="/" replace />;
  return <Outlet />;
}

function Restoring() {
  return (
    <p className="restoring" role="status">
      <FormattedMessage {...messages.restoringSession} />
    </p>
  );
}
