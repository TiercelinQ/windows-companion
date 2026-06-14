import type { ReactNode } from "react";
import { APP_VERSION } from "../../../../shared/config";
import { t } from "../../i18n";

export interface AppStatus {
  message: string;
  busy: boolean;
}

/** Bottom bar: status message (left), progress (center, while busy), version (right). */
export function Statusbar({ status }: { status: AppStatus }): ReactNode {
  return (
    <footer id="statusbar">
      <span className="status-message">{status.message}</span>
      <span className="status-progress">
        {status.busy ? <progress className="status-bar-progress" /> : null}
      </span>
      <span className="status-version">{t("status.version", { version: APP_VERSION })}</span>
    </footer>
  );
}
