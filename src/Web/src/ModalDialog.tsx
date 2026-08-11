import { useEffect, useRef } from "react";
import type { ReactNode } from "react";

export function ModalDialog({
  labelledBy,
  className = "",
  onClose,
  children,
}: {
  labelledBy: string;
  className?: string;
  onClose: () => void;
  children: ReactNode;
}) {
  const dialog = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const element = dialog.current;
    if (!element) return;
    if (typeof element.showModal === "function") element.showModal();
    else element.setAttribute("open", "");
    return () => {
      if (element.open && typeof element.close === "function") element.close();
    };
  }, []);

  return (
    <dialog
      ref={dialog}
      aria-labelledby={labelledBy}
      className={`app-dialog ${className}`.trim()}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onKeyDown={(event) => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        onClose();
      }}
    >
      {children}
    </dialog>
  );
}
