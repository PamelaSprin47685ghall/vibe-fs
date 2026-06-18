module VibeFs.Kernel.JsBoundary

open Fable.Core.JsInterop
open Fable.Core

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

let private containsAbortText (message: string) : bool =
    not (System.String.IsNullOrWhiteSpace message)
    && message.ToLowerInvariant().Contains("abort")

/// Translate a raw JS error object into a strongly-typed domain error.
/// Walks nested `error`, `data`, and `cause` shapes so wrapped host exceptions
/// are classified by their `name` or `_tag` rather than by scanning text.
let translateJsError (error: obj) : DomainError =
    let rec classify (value: obj) (seen: obj list) =
        if Dyn.isNullish value then SystemPanic "Null error context"
        elif List.exists (fun seenObj -> obj.ReferenceEquals(value, seenObj)) seen then SystemPanic "Cyclic error context"
        elif Dyn.typeIs value "string" then
            let message = string value
            if containsAbortText message then MessageAborted else UnknownJsError(message)
        else
            let seenNext = value :: seen
            let name = Dyn.str value "name"
            let tag = Dyn.str value "_tag"
            if name = "AbortError" || name = "MessageAbortedError" || tag = "MessageAborted" then
                MessageAborted
            elif name = "SessionBusyError" || tag = "SessionBusy" then
                SessionBusy
            elif name = "ForegroundWaitBackgroundedError" || tag = "TaskWaitBackgrounded" then
                TaskWaitBackgrounded
            else
                let nested = Dyn.get value "error"
                if not (Dyn.isNullish nested) then classify nested seenNext
                else
                    let data = Dyn.get value "data"
                    if not (Dyn.isNullish data) && Dyn.typeIs data "object" then
                        classify data seenNext
                    else
                        let cause = Dyn.get value "cause"
                        if not (Dyn.isNullish cause) then classify cause seenNext
                        else
                            let message = Dyn.str value "message"
                            if containsAbortText message then MessageAborted else UnknownJsError(message)
    classify error []
