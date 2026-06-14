import { useState, type ReactNode } from "react";
import { t } from "../i18n";
import { useToast } from "../hooks/useToast";
import { formatBytes, formatGhz, formatMhz } from "../utils/helpers";
import type { HardwareInfo } from "../../../shared/types";
import type { AppStatus } from "./layout/Statusbar";

/** Hardware specifications view. */
export function HardwareView({ onStatus }: { onStatus: (status: AppStatus) => void }): ReactNode {
  const toast = useToast();
  const [info, setInfo] = useState<HardwareInfo | null>(null);
  const [scanning, setScanning] = useState(false);
  const [exporting, setExporting] = useState(false);

  const scan = async (): Promise<void> => {
    setScanning(true);
    onStatus({ message: t("common.scanning"), busy: true });
    const res = await window.api.scanHardware();
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
    const res = await window.api.exportHardware(info);
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
        <h1 className="section-title">{t("hardware.title")}</h1>
        <p className="section-subtitle">{t("hardware.subtitle")}</p>
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
          <h2 className="result-group-title">{t("hardware.cpu")}</h2>
          <table className="data-table">
            <tbody>
              <tr>
                <th scope="row">{t("hardware.model")}</th>
                <td>{info.cpu.model}</td>
              </tr>
              <tr>
                <th scope="row">{t("hardware.cores")}</th>
                <td>{info.cpu.cores}</td>
              </tr>
              <tr>
                <th scope="row">{t("hardware.logical")}</th>
                <td>{info.cpu.logical}</td>
              </tr>
              <tr>
                <th scope="row">{t("hardware.clock")}</th>
                <td>{formatGhz(info.cpu.clockGhz)}</td>
              </tr>
            </tbody>
          </table>

          <h2 className="result-group-title">
            {t("hardware.memory")} — {formatBytes(info.totalMemoryBytes)}
          </h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>{t("hardware.stick")}</th>
                <th>{t("hardware.capacity")}</th>
                <th>{t("hardware.frequency")}</th>
                <th>{t("hardware.manufacturer")}</th>
              </tr>
            </thead>
            <tbody>
              {info.memory.map((m, i) => (
                <tr key={i}>
                  <td>{i + 1}</td>
                  <td>{formatBytes(m.capacityBytes)}</td>
                  <td>{formatMhz(m.speedMhz)}</td>
                  <td>{m.manufacturer}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <h2 className="result-group-title">{t("hardware.gpu")}</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>{t("hardware.model")}</th>
                <th>{t("hardware.vram")}</th>
              </tr>
            </thead>
            <tbody>
              {info.gpu.map((g, i) => (
                <tr key={i}>
                  <td>{g.model}</td>
                  <td>{g.vramBytes != null ? formatBytes(g.vramBytes) : t("common.unknown")}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <h2 className="result-group-title">{t("hardware.disks")}</h2>
          <table className="data-table">
            <thead>
              <tr>
                <th>{t("hardware.model")}</th>
                <th>{t("hardware.type")}</th>
                <th>{t("hardware.capacity")}</th>
              </tr>
            </thead>
            <tbody>
              {info.disks.map((d, i) => (
                <tr key={i}>
                  <td>{d.model}</td>
                  <td>{d.mediaType}</td>
                  <td>{formatBytes(d.sizeBytes)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <h2 className="result-group-title">{t("hardware.motherboard")}</h2>
          <table className="data-table">
            <tbody>
              <tr>
                <th scope="row">{t("hardware.manufacturer")}</th>
                <td>{info.motherboard.manufacturer}</td>
              </tr>
              <tr>
                <th scope="row">{t("hardware.model")}</th>
                <td>{info.motherboard.product}</td>
              </tr>
            </tbody>
          </table>

          <h2 className="result-group-title">{t("hardware.bios")}</h2>
          <table className="data-table">
            <tbody>
              <tr>
                <th scope="row">{t("hardware.version")}</th>
                <td>{info.bios.version}</td>
              </tr>
              <tr>
                <th scope="row">{t("hardware.date")}</th>
                <td>{info.bios.releaseDate || t("common.unknown")}</td>
              </tr>
            </tbody>
          </table>
        </div>
      ) : (
        <p className="empty-hint">{t("common.noScan")}</p>
      )}
    </section>
  );
}
