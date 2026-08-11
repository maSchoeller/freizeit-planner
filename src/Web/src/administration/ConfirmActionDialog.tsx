import { ModalDialog } from "../ModalDialog";
import type { PendingAction } from "./support";

export function ConfirmActionDialog({
  pendingAction,
  busy,
  setPendingAction,
}: {
  pendingAction: PendingAction;
  busy: string | null;
  setPendingAction: (action: PendingAction | null) => void;
}) {
  return (
    <ModalDialog
      labelledBy="confirm-admin-action"
      className="danger-dialog"
      onClose={() => setPendingAction(null)}
    >
      <h2 id="confirm-admin-action">{pendingAction.title}</h2>
      <p>{pendingAction.description}</p>
      <div className="dialog-actions">
        <button
          className="danger-action"
          disabled={busy !== null}
          onClick={() => {
            const action = pendingAction;
            setPendingAction(null);
            void action.run();
          }}
          type="button"
        >
          {pendingAction.confirmLabel}
        </button>
        <button
          autoFocus
          className="secondary-action"
          disabled={busy !== null}
          onClick={() => setPendingAction(null)}
          type="button"
        >
          Abbrechen
        </button>
      </div>
    </ModalDialog>
  );
}
