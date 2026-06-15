module VibeFs.Kernel.DomainError

/// Strongly-typed domain failures.  Sealed union so every consumer must handle
/// each case explicitly; no hidden catch-all can swallow a new error kind.
type DomainError =
    | MessageAborted
    | SessionBusy
    | TaskWaitBackgrounded
    | ExecutorExecutableMissing
    | SystemPanic of message: string
    | UnknownJsError of message: string

let isAbort (error: DomainError) : bool =
    match error with
    | MessageAborted -> true
    | SessionBusy
    | TaskWaitBackgrounded
    | ExecutorExecutableMissing
    | SystemPanic _
    | UnknownJsError _ -> false
