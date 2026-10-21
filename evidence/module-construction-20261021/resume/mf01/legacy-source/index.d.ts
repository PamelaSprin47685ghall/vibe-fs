/** Handle returned by `run`. The Host owns suspension and deadline changes. */
export interface SandboxRunHandle {
  readonly programPromise: Promise<string>
  readonly deadlineRef: { current: number }
  pause(): void
  resume(): void
}

export interface SandboxMessages {
  /**
   * Run modelSource in a context with no Host prototype and a realm membrane.
   * `programPromise` resolves to the wrapper JSON string (sentinels included).
   */
  run(
    api: object,
    deadlineMs: number,
    deadlineEpochMs: number,
    baseClassSource: string,
    modelSource: string,
  ): SandboxRunHandle

  resetDeadline(deadlineRef: { current: number }, epochMs: number): void
}

export function Sandbox(): SandboxMessages
