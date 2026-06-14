import { execFile, spawn } from "node:child_process";
import { CommandError, CommandTimeoutError } from "./errors";

/** Handlers for a streamed command. */
export interface StreamHandlers {
  onData: (line: string) => void;
  onClose: (code: number | null) => void;
}

const MAX_BUFFER = 16 * 1024 * 1024;

/**
 * Run a command to completion and return its stdout.
 * @throws {CommandTimeoutError} when the command exceeds `timeoutMs`.
 * @throws {CommandError} on a non-zero exit or spawn failure.
 */
export function runCommand(file: string, args: string[], timeoutMs: number): Promise<string> {
  return new Promise((resolve, reject) => {
    execFile(
      file,
      args,
      { timeout: timeoutMs, windowsHide: true, maxBuffer: MAX_BUFFER },
      (err, stdout, stderr) => {
        if (err) {
          const e = err as NodeJS.ErrnoException & { killed?: boolean; signal?: string };
          if (e.killed && e.signal === "SIGTERM") {
            reject(new CommandTimeoutError(`La commande ${file} a dépassé le délai imparti.`));
            return;
          }
          reject(new CommandError(`Échec de la commande ${file}.`, stderr.trim() || err.message));
          return;
        }
        resolve(stdout);
      },
    );
  });
}

/**
 * Run a command and parse its stdout as JSON.
 * @throws {CommandError} when stdout is empty or not valid JSON.
 */
export async function runJson<T>(file: string, args: string[], timeoutMs: number): Promise<T> {
  const out = (await runCommand(file, args, timeoutMs)).trim();
  if (!out) throw new CommandError(`Sortie vide pour ${file}.`);
  try {
    return JSON.parse(out) as T;
  } catch {
    throw new CommandError(`Réponse illisible de ${file}.`, out.slice(0, 500));
  }
}

/**
 * Spawn a command and stream its output line by line.
 * @returns a cancel function that terminates the process tree.
 */
export function startStream(file: string, args: string[], handlers: StreamHandlers): () => void {
  const child = spawn(file, args, { windowsHide: true });
  let buffer = "";

  const consume = (chunk: string): void => {
    buffer += chunk;
    let idx = buffer.indexOf("\n");
    while (idx >= 0) {
      handlers.onData(buffer.slice(0, idx).replace(/\r$/, ""));
      buffer = buffer.slice(idx + 1);
      idx = buffer.indexOf("\n");
    }
  };

  child.stdout.setEncoding("utf8");
  child.stderr.setEncoding("utf8");
  child.stdout.on("data", consume);
  child.stderr.on("data", consume);

  child.on("error", (err) => {
    handlers.onData(`Erreur : ${err.message}`);
    handlers.onClose(null);
  });
  child.on("close", (code) => {
    if (buffer.length) {
      handlers.onData(buffer.replace(/\r$/, ""));
      buffer = "";
    }
    handlers.onClose(code);
  });

  return () => {
    if (child.pid) {
      execFile("taskkill", ["/pid", String(child.pid), "/T", "/F"], { windowsHide: true }, () => {
        /* best-effort tree kill */
      });
    } else {
      child.kill();
    }
  };
}

/** Normalize PowerShell's single-item-vs-array JSON quirk into an array. */
export function asArray<T>(value: T | T[] | null | undefined): T[] {
  if (value == null) return [];
  return Array.isArray(value) ? value : [value];
}
