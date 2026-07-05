import type { ReactNode } from "react";
import { APP_NAME } from "../../../../shared/config";
import { t } from "../../i18n";
import type { TabDef, TabId } from "../../App";
import logoUrl from "../../assets/logo.png";

interface TopbarProps {
  tabs: TabDef[];
  active: TabId;
  onSelect: (id: TabId) => void;
  theme: "light" | "dark";
  onToggleTheme: () => void;
}

/** Top bar: logo + app name, navigation tabs (left), theme toggle (right). */
export function Topbar({ tabs, active, onSelect, theme, onToggleTheme }: TopbarProps): ReactNode {
  return (
    <header id="topbar">
      <div className="topbar-brand">
        <img src={logoUrl} className="logo-icon" alt="" />
        <span className="topbar-title">{APP_NAME}</span>
      </div>
      <nav id="topbar-tabs">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            type="button"
            className={`tab${tab.id === active ? " is-active" : ""}`}
            aria-current={tab.id === active}
            onClick={() => onSelect(tab.id)}
          >
            <i className={`fa-solid ${tab.icon} tab-icon`} aria-hidden="true" />
            {t(tab.labelKey)}
          </button>
        ))}
      </nav>
      <button
        type="button"
        className="theme-toggle"
        title={theme === "dark" ? t("theme.toLight") : t("theme.toDark")}
        aria-label={theme === "dark" ? t("theme.toLight") : t("theme.toDark")}
        onClick={onToggleTheme}
      >
        <i className={`fa-solid ${theme === "dark" ? "fa-sun" : "fa-moon"}`} aria-hidden="true" />
      </button>
    </header>
  );
}
