module VibeFs.Kernel.AbortKernel

open Fable.Core
open Fable.Core.JsInterop

/// The two error names the host uses to signal an aborted message.
let isAbortErrorName (name: string option) : bool =
    match name with
    | Some n -> n = "MessageAbortedError" || n = "AbortError"
    | None -> false

/// Recognise a JS error object (DOMException or Error) carrying an abort name.
let isAbortError (error: obj) : bool =
    if isNull error then false
    else
        let name = error?name
        isAbortErrorName (if isNull name then None else Some(string name))
