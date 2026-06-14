/** A system command exited with an error or produced unreadable output. */
export class CommandError extends Error {
  readonly detail?: string;
  constructor(message: string, detail?: string) {
    super(message);
    this.name = "CommandError";
    this.detail = detail;
  }
}

/** A system command exceeded its allotted time. */
export class CommandTimeoutError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "CommandTimeoutError";
  }
}
