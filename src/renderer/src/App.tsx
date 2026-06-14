import { useState, type ReactNode } from "react";
import { useTheme } from "./hooks/useTheme";
import { t } from "./i18n";
import { ErrorBoundary } from "./views/ErrorBoundary";
import { HardwareView } from "./views/HardwareView";
import { NetworkView } from "./views/NetworkView";
import { SystemView } from "./views/SystemView";
import { ToastManager } from "./views/ToastManager";
import { UpdateView } from "./views/UpdateView";
import { Statusbar, type AppStatus } from "./views/layout/Statusbar";
import { Topbar } from "./views/layout/Topbar";

export type TabId = "update" | "hardware" | "system" | "network";
export interface TabDef {
  id: TabId;
  labelKey: string;
  icon: string;
}

const TABS: TabDef[] = [
  { id: "update", labelKey: "nav.update", icon: "fa-rotate" },
  { id: "hardware", labelKey: "nav.hardware", icon: "fa-microchip" },
  { id: "system", labelKey: "nav.system", icon: "fa-circle-info" },
  { id: "network", labelKey: "nav.network", icon: "fa-network-wired" },
];

/** Application shell: topbar, active view, statusbar. */
export function App(): ReactNode {
  const { theme, toggle } = useTheme();
  const [active, setActive] = useState<TabId>("update");
  const [status, setStatus] = useState<AppStatus>({ message: t("common.ready"), busy: false });

  return (
    <ErrorBoundary>
      <ToastManager>
        <div id="app-shell">
          <Topbar
            tabs={TABS}
            active={active}
            onSelect={setActive}
            theme={theme}
            onToggleTheme={toggle}
          />
          <main id="main-content">
            {active === "update" && <UpdateView onStatus={setStatus} />}
            {active === "hardware" && <HardwareView onStatus={setStatus} />}
            {active === "system" && <SystemView onStatus={setStatus} />}
            {active === "network" && <NetworkView onStatus={setStatus} />}
          </main>
          <Statusbar status={status} />
        </div>
      </ToastManager>
    </ErrorBoundary>
  );
}
