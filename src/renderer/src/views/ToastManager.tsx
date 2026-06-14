import { useCallback, useRef, useState, type ReactNode } from "react";
import { TOAST_DURATION_MS } from "../../../shared/config";
import type { ToastType } from "../../../shared/types";
import { ToastContext, type ToastItem, type ToastOptions } from "../hooks/useToast";

const ICONS: Record<ToastType, string> = {
  success: "fa-circle-check",
  info: "fa-circle-info",
  warning: "fa-triangle-exclamation",
  danger: "fa-circle-exclamation",
};

/** Provides the toast dispatch and renders the toast queue (top-right). */
export function ToastManager({ children }: { children: ReactNode }): ReactNode {
  const [items, setItems] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    setItems((current) => current.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback(
    (options: ToastOptions) => {
      const id = nextId.current++;
      setItems((current) => [...current, { ...options, id }]);
      if (options.type !== "danger") {
        window.setTimeout(() => dismiss(id), TOAST_DURATION_MS);
      }
    },
    [dismiss],
  );

  return (
    <ToastContext.Provider value={toast}>
      {children}
      <div id="toast-container">
        {items.map((t) => (
          <div key={t.id} className={`toast toast-${t.type}`} role="alert">
            <i className={`fa-solid ${ICONS[t.type]} toast-icon`} aria-hidden="true" />
            <div className="toast-body">
              <span className="toast-message">{t.message}</span>
              {t.description ? <span className="toast-description">{t.description}</span> : null}
            </div>
            <button
              type="button"
              className="toast-close"
              aria-label="Fermer"
              onClick={() => dismiss(t.id)}
            >
              <i className="fa-solid fa-xmark" aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
