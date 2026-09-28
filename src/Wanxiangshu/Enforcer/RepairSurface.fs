namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Enforcer.Cycle

/// JSON-only owner boundary for Host abort/cleanup evidence. The repair
/// predicates remain private to EnforcerRepair; semantic callers observe only
/// the two mutually exclusive booleans needed by the fallback decision.
[<RequireQualifiedAccess>]
module RepairSurface =

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private messagesOf (value: obj) : obj list =
        if isNullish value then
            []
        else
            unbox<obj array> value |> Array.toList

    /// Classify the given Host assistant step. `interrupted=true` wins over the
    /// generic error status, so one abort residue cannot be counted twice.
    let classifyBlogAttempt (rawMessages: obj array) : obj =
        match EnforcerCycleDecode.latestAssistant (messagesOf (box rawMessages)) with
        | None -> box {| aborted = false; errored = false |}
        | Some step ->
            box
                {| aborted = EnforcerRepair.hasAbortedBlogAttempt step
                   errored = EnforcerRepair.hasErroredBlogAttempt step |}
