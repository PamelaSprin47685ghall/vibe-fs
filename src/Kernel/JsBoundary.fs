module VibeFs.Kernel.JsBoundary

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.DomainError

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

/// Parse a string boundary value into an int when possible.
let parseJsBoundary (s: string) : int option =
    match System.Int32.TryParse s with
    | true, n -> Some n
    | _ -> None

/// Parse every element of an array as an int, dropping unparseable values.
let parseJsBoundaryArray (arr: obj array) : int array =
    arr |> Array.choose (fun x -> parseJsBoundary (string x))

[<Emit("Object.keys($0)")>]
let private objectKeys (_: obj) : string array = jsNative

/// Parse a plain JS object into a string->int map, dropping non-numeric values.
let parseJsBoundaryObj (o: obj) : Map<string, int> =
    objectKeys o
    |> Array.choose (fun key ->
        let v = Dyn.get o key
        match System.Int32.TryParse (string v) with
        | true, n -> Some (key, n)
        | _ -> None)
    |> Map.ofArray
