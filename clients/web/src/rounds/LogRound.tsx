import { createContext, useContext, useEffect, useId, useRef, useState, type ReactNode } from "react";
import { Link, useNavigate } from "react-router";
import { LogRoundForm } from "./LogRoundForm";

/**
 * Where Log round opens (#92): a dialog over the current page on desktop, and its own
 * page on phones, so the form gets the whole screen. Either way it's one tap from every
 * section (docs/clients/ux-design.md → "Navigation"), and /rounds/new works as a link.
 */

const OpenLogRound = createContext<(() => void) | null>(null);

/** Desktop, where the form fits in a dialog. Matches the layout's sidebar breakpoint. */
const dialogQuery = "(min-width: 901px)";

/** Holds the dialog for the signed-in app; AppLayout wraps every section in it. */
export function LogRoundProvider({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();

  return (
    <OpenLogRound.Provider value={() => setOpen(true)}>
      {children}
      {open && (
        <LogRoundDialog
          onClose={() => setOpen(false)}
          onSaved={(roundId) => {
            setOpen(false);
            navigate(`/rounds/${roundId}`);
          }}
        />
      )}
    </OpenLogRound.Provider>
  );
}

/** A link to Log round that opens the dialog instead, where there's room for one. */
export function LogRoundLink({ className, children }: { className?: string; children: ReactNode }) {
  const open = useContext(OpenLogRound);

  return (
    <Link
      to="/rounds/new"
      className={className}
      onClick={(event) => {
        if (open && window.matchMedia?.(dialogQuery).matches) {
          event.preventDefault();
          open();
        }
      }}
    >
      {children}
    </Link>
  );
}

/**
 * A modal dialog: the browser's own <dialog> traps focus and closes on Escape. Focus goes
 * back to what opened it when it closes; that's done here, because React removes the
 * element before the browser would restore it. After Save, the new page takes focus.
 */
function LogRoundDialog({ onClose, onSaved }: { onClose: () => void; onSaved: (roundId: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const element = dialog.current!;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    if (!element.open) element.showModal();
    return () => {
      element.close();
      opener?.focus();
    };
  }, []);

  return (
    <dialog
      ref={dialog}
      className="log-round-dialog"
      aria-labelledby={titleId}
      onCancel={(event) => {
        // Escape: close through React state, so the dialog and the state agree.
        event.preventDefault();
        onClose();
      }}
    >
      <LogRoundForm headingLevel={2} titleId={titleId} onSaved={onSaved} onCancel={onClose} />
    </dialog>
  );
}

/** /rounds/new: the form as a page, on phones or from a link. */
export function LogRoundPage() {
  const navigate = useNavigate();

  return (
    <section className="page log-round-page">
      <LogRoundForm
        headingLevel={1}
        // Replace the form in history, so Back from the new round doesn't reopen it.
        onSaved={(roundId) => navigate(`/rounds/${roundId}`, { replace: true })}
        onCancel={() => ((window.history.state as { idx?: number } | null)?.idx ? navigate(-1) : navigate("/"))}
      />
    </section>
  );
}
