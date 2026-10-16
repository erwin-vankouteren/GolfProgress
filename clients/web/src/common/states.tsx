import { useSyncExternalStore } from "react";
import { FormattedMessage, useIntl, type MessageDescriptor } from "react-intl";
import { messages } from "../i18n/messages";
import "./states.css";

/**
 * The states every screen handles (docs/clients/ux-design.md → "States every screen
 * handles"): loading as skeletons, errors with Retry, and offline.
 */

function subscribeToConnection(onChange: () => void) {
  window.addEventListener("online", onChange);
  window.addEventListener("offline", onChange);
  return () => {
    window.removeEventListener("online", onChange);
    window.removeEventListener("offline", onChange);
  };
}

/** Whether the browser has a connection; re-renders when that changes. */
export function useOnline(): boolean {
  return useSyncExternalStore(subscribeToConnection, () => navigator.onLine);
}

/** "You're offline", across the top of the app while there's no connection (v1 is online-only). */
export function OfflineBanner() {
  const online = useOnline();
  return online ? null : (
    <p className="offline-banner" role="status">
      <FormattedMessage {...messages.offline} />
    </p>
  );
}

/** A failed load: what couldn't be loaded, and Retry. */
export function LoadError({ message, onRetry }: { message: MessageDescriptor; onRetry: () => void }) {
  return (
    <div className="load-error" role="alert">
      <p>
        <FormattedMessage {...message} />
      </p>
      <button type="button" className="button" onClick={onRetry}>
        <FormattedMessage {...messages.tryAgain} />
      </button>
    </div>
  );
}

/**
 * Placeholders in the shape of the content that's coming. Screen readers hear
 * "Loading…" once, instead of the empty boxes.
 */
export function Skeleton({ blocks, className }: { blocks: number; className?: string }) {
  const intl = useIntl();
  return (
    <div className={`skeleton ${className ?? ""}`} role="status" aria-label={intl.formatMessage(messages.loading)}>
      {Array.from({ length: blocks }, (_, i) => (
        <div key={i} className="skeleton-block" aria-hidden="true" />
      ))}
    </div>
  );
}
