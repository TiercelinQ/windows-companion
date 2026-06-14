import { useState, type ReactNode } from "react";
import { t } from "../i18n";
import { useToast } from "../hooks/useToast";
import { joinList } from "../utils/helpers";
import type { NetworkInfo } from "../../../shared/types";
import type { AppStatus } from "./layout/Statusbar";

/** Network configuration view. */
export function NetworkView({ onStatus }: { onStatus: (status: AppStatus) => void }): ReactNode {
  const toast = useToast();
  const [info, setInfo] = useState<NetworkInfo | null>(null);
  const [scanning, setScanning] = useState(false);
  const [exporting, setExporting] = useState(false);

  const scan = async (): Promise<void> => {
    setScanning(true);
    onStatus({ message: t("common.scanning"), busy: true });
    const res = await window.api.scanNetwork();
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
    const res = await window.api.exportNetwork(info);
    setExporting(false);
    if (!res.ok) {
      toast(res.error);
      return;
    }
    if (!res.data.canceled) {
      toast({ type: "success", message: t("common.exported"), description: res.data.path });
    }
  };

  return (
    <section className="view">
      <div className="section-header">
        <h1 className="section-title">{t("network.title")}</h1>
        <p className="section-subtitle">{t("network.subtitle")}</p>
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
          <p className="result-host">{t("network.host", { host: info.hostName })}</p>
          {info.adapters.map((a, i) => (
            <div key={i} className="result-adapter">
              <h2 className="result-group-title">{a.name}</h2>
              <table className="data-table">
                <tbody>
                  <tr>
                    <th scope="row">{t("network.description")}</th>
                    <td>{a.description}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.status")}</th>
                    <td>{a.status}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.mac")}</th>
                    <td>{a.macAddress || "—"}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.ipv4")}</th>
                    <td>{joinList(a.ipv4)}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.ipv6")}</th>
                    <td>{joinList(a.ipv6)}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.subnet")}</th>
                    <td>{joinList(a.subnet)}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.gateway")}</th>
                    <td>{joinList(a.gateway)}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.dns")}</th>
                    <td>{joinList(a.dns)}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.dhcp")}</th>
                    <td>{a.dhcpEnabled ? t("common.enabled") : t("common.disabled")}</td>
                  </tr>
                  <tr>
                    <th scope="row">{t("network.linkSpeed")}</th>
                    <td>{a.linkSpeed || "—"}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          ))}
        </div>
      ) : (
        <p className="empty-hint">{t("common.noScan")}</p>
      )}
    </section>
  );
}
