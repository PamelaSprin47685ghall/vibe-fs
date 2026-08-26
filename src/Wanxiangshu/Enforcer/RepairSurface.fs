// primary_owner: behavior-diagnosis — BehaviorDiagnosis.SurfaceSurface — KEEP — behavior-diagnosis-surface verified
namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop

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

    /// Classify the final Host assistant step. `interrupted=true` wins over the
    /// generic error status, so one abort residue cannot be counted twice.
    let classifyBlogAttempt (rawMessages: obj array) : obj =
        let messages = messagesOf (box rawMessages)

        box
            {| aborted = EnforcerRepair.hasAbortedBlogAttempt messages
               errored = EnforcerRepair.hasErroredBlogAttempt messages |}
