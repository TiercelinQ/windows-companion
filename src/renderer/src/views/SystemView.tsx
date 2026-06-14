import { useState, type ReactNode } from "react";
import { t } from "../i18n";
import { useToast } from "../hooks/useToast";
import type { SystemInfo } from "../../../shared/types";
import type { AppStatus } from "./layout/Statusbar";

/** Detailed system information view. */
export function SystemView({ onStatus }: { onStatus: (status: AppStatus) => void }): ReactNode {
  const toast = useToast();
  const [info, setInfo] = useState<SystemInfo | null>(null);
  const [scanning, setScanning] = useState(false);
  const [exporting, setExporting] = useState(false);

  const scan = async (): Promise<void> => {
    setScanning(true);
    onStatus({ message: t("common.scanning"), busy: true });
    const res = await window.api.scanSystem();
    setScanning(false);
    if (res.ok) {
      setInfo(res.data);
      onStatus({ message: t("common.scanDone"), busy: false });
    } else {
      onStatus({ message: t("common.ready"), busy: false });
      toast(res.error);
    }
  };

  const exportReport = async (): Promise<void> => {
    if (!info) return;
    setExporting(true);
    const res = await window.api.exportSystem(info);
    setExporting(false);
    if (!res.ok) {
      toast(res.error);
      return;
    }
    if (!res.data.canceled) {
      toast({ type: "success", message: t("common.exported"), description: res.data.path });
    }
  };

  const rows: [string, string][] = info
    ? [
        [t("system.osName"), info.osName],
        [t("system.edition"), info.edition],
        [t("system.version"), info.version],
        [t("system.build"), info.build],
        [t("system.arch"), info.arch],
        [t("system.machineName"), info.machineName],
        [t("system.user"), info.user],
        [t("system.installDate"), info.installDate],
        [t("system.lastBoot"), info.lastBoot],
        [t("system.uptime"), info.uptime],
      ]
    : [];

  return (
    <section className="view">
      <div className="section-header">
        <h1 className="section-title">{t("system.title")}</h1>
        <p className="section-subtitle">{t("system.subtitle")}</p>
      </div>

      <div className="action-bar">
        <button type="button" className="btn btn-primary" disabled={scanning} onClick={scan}>
          <i className="fa-solid fa-magnifying-glass" aria-hidden="true" />
          {t("common.scan")}
        </button>
        <button
          type="button"
          className="btn btn-secondary"
          disabled={!info || exporting}
          onClick={exportReport}
        >
          <i className="fa-solid fa-file-export" aria-hidden="true" />
          {t("common.export")}
        </button>
      </div>

      {info ? (
        <div className="result">
          <table className="data-table">
            <tbody>
              {rows.map(([label, value]) => (
                <tr key={label}>
                  <th scope="row">{label}</th>
                  <td>{value}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="empty-hint">{t("common.noScan")}</p>
      )}
    </section>
  );
}
