import { useEffect, useRef, useState, type ReactNode } from "react";
import { t } from "../i18n";
import { useToast } from "../hooks/useToast";
import type { AppStatus } from "./layout/Statusbar";

type RunState = "idle" | "running" | "checking" | "done" | "failed" | "canceled";

const SPINNER_FRAME = /^[-\\|/]$/;
const PROGRESS_FRAME = /[█▒]/;

/**
 * winget (non-TTY) emits each spinner tick and progress-bar frame on its own line instead
 * of redrawing via carriage return. Such transient frames overwrite a single active line;
 * persistent output (table rows, status messages) is appended to the log.
 */
function isTransientFrame(line: string): boolean {
  return SPINNER_FRAME.test(line.trim()) || PROGRESS_FRAME.test(line);
}

/** Software update view: runs winget and streams its output live. */
export function UpdateView({ onStatus }: { onStatus: (status: AppStatus) => void }): ReactNode {
  const toast = useToast();
  const [runState, setRunState] = useState<RunState>("idle");
  const [lines, setLines] = useState<string[]>([]);
  const [activeLine, setActiveLine] = useState("");
  const outputRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const offData = window.api.onUpdateData((raw) => {
      const line = raw.split("\r").pop() ?? raw;
      if (line.trim() === "") return;
      if (isTransientFrame(line)) {
        setActiveLine(line.trimEnd());
        return;
      }
      setActiveLine("");
      setLines((current) => [...current, line]);
    });
    const offEnd = window.api.onUpdateEnd((result) => {
      setActiveLine("");
      if (result.kind === "check") {
        setRunState("idle");
        if (result.exitCode === null) {
          onStatus({ message: t("update.checkFailed"), busy: false });
          toast({ type: "danger", message: t("update.checkFailed") });
        } else if (result.hasUpdates) {
          onStatus({ message: t("update.available"), busy: false });
          toast({ type: "info", message: t("update.available") });
        } else {
          onStatus({ message: t("update.upToDate"), busy: false });
          toast({ type: "success", message: t("update.upToDate") });
        }
        return;
      }
      if (result.canceled) {
        setRunState("canceled");
        onStatus({ message: t("update.canceled"), busy: false });
        toast({ type: "warning", message: t("update.canceled") });
      } else if (result.exitCode === 0) {
        setRunState("done");
        onStatus({ message: t("update.done"), busy: false });
        toast({ type: "success", message: t("update.done") });
      } else {
        setRunState("failed");
        onStatus({ message: t("update.failed"), busy: false });
        toast({ type: "danger", message: t("update.failed") });
      }
    });
    return () => {
      offData();
      offEnd();
    };
  }, [toast, onStatus]);

  useEffect(() => {
    const el = outputRef.current;
    if (el) el.scrollTop = el.scrollHeight;
  }, [lines, activeLine]);

  const start = async (): Promise<void> => {
    setLines([]);
    setActiveLine("");
    setRunState("running");
    onStatus({ message: t("update.running"), busy: true });
    const res = await window.api.startUpdate();
    if (!res.ok) {
      setRunState("failed");
      onStatus({ message: t("update.failed"), busy: false });
      toast(res.error);
    }
  };

  const check = async (): Promise<void> => {
    setLines([]);
    setActiveLine("");
    setRunState("checking");
    onStatus({ message: t("update.checking"), busy: true });
    const res = await window.api.checkUpdates();
    if (!res.ok) {
      setRunState("idle");
      onStatus({ message: t("update.checkFailed"), busy: false });
      toast(res.error);
    }
  };

  const cancel = async (): Promise<void> => {
    const res = await window.api.cancelUpdate();
    if (!res.ok) toast(res.error);
  };

  const running = runState === "running";
  const busy = running || runState === "checking";

  return (
    <section className="view">
      <div className="section-header">
        <h1 className="section-title">{t("update.title")}</h1>
        <p className="section-subtitle">{t("update.subtitle")}</p>
      </div>

      <div className="action-bar">
        <button type="button" className="btn btn-secondary" disabled={busy} onClick={check}>
          <i className="fa-solid fa-magnifying-glass" aria-hidden="true" />
          {t("update.check")}
        </button>
        <button type="button" className="btn btn-primary" disabled={busy} onClick={start}>
          <i className={`fa-solid fa-rotate${running ? " fa-spin" : ""}`} aria-hidden="true" />
          {t("update.start")}
        </button>
        <button type="button" className="btn btn-danger" disabled={!running} onClick={cancel}>
          <i className="fa-solid fa-stop" aria-hidden="true" />
          {t("update.cancel")}
        </button>
      </div>

      <div className="terminal-output" ref={outputRef} role="log" aria-live="polite">
        {lines.length === 0 && activeLine === "" && !busy ? (
          <span className="terminal-empty">{t("update.idle")}</span>
        ) : (
          <>
          {lines.map((line, i) => (
            <span key={i} className="terminal-line">
              {line || " "}
            </span>
          ))}
          {activeLine !== "" && (
            <span className="terminal-line terminal-line-active" aria-hidden="true">
              {activeLine}
            </span>
          )}
          </>
        )}
      </div>
    </section>
  );
}
