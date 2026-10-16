import { useEffect, useId, useRef } from "react";
import { FormattedMessage } from "react-intl";
import { messages, type PlainMessage } from "../i18n/messages";

/**
 * Asks before something that can't be undone, as a modal <dialog>: focus stays inside,
 * Escape cancels, and focus returns to what opened it. Cancel comes first and has focus,
 * so pressing Enter by habit doesn't confirm.
 */
export function ConfirmDialog({
  title,
  body,
  confirm,
  busyLabel,
  busy,
  error,
  onConfirm,
  onCancel,
}: {
  title: PlainMessage;
  body: PlainMessage;
  confirm: PlainMessage;
  busyLabel: PlainMessage;
  busy: boolean;
  error?: string;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const cancel = useRef<HTMLButtonElement>(null);
  const titleId = useId();
  const bodyId = useId();

  useEffect(() => {
    const element = dialog.current!;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    // jsdom has no showModal; there the dialog is simply shown.
    if (typeof element.showModal === "function") element.showModal();
    else element.setAttribute("open", "");
    // After noting the opener, so focus can go back to it when the dialog closes.
    cancel.current?.focus();
    return () => {
      if (typeof element.close === "function") element.close();
      opener?.focus();
    };
  }, []);

  return (
    <dialog
      ref={dialog}
      className="confirm-dialog"
      aria-labelledby={titleId}
      aria-describedby={bodyId}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onCancel();
      }}
    >
      <h2 id={titleId}>
        <FormattedMessage {...title} />
      </h2>
      <p id={bodyId}>
        <FormattedMessage {...body} />
      </p>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      <div className="confirm-actions">
        <button ref={cancel} type="button" className="button" onClick={onCancel} disabled={busy}>
          <FormattedMessage {...messages.cancel} />
        </button>
        <button type="button" className="button button--danger" onClick={onConfirm} disabled={busy} aria-busy={busy}>
          <FormattedMessage {...(busy ? busyLabel : confirm)} />
        </button>
      </div>
    </dialog>
  );
}
