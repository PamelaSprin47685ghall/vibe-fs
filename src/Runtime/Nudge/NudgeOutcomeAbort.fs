module Wanxiangshu.Runtime.NudgeOutcomeAbort

open Fable.Core
open Wanxiangshu.Kernel.Nudge
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Runtime.NudgeLease
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntime
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.MuxLogicalReceipt
open Wanxiangshu.Runtime.NudgeDispatchClaim

let isAbortUnavailableMessage (msg: string) : bool =
    if System.String.IsNullOrWhiteSpace msg then
        false
    else
        msg.Contains "AbortUnavailable" || msg.Contains "cannot confirm cancel"

let safeNudgeAbort (abortRun: string -> JS.Promise<unit>) (sessionKey: string) : JS.Promise<Result<unit, exn>> =
    promise {
        try
            do! abortRun sessionKey
            return Ok()
        with ex ->
            return Error ex
    }

let abortDeliveredNudge
    (runtime: FallbackRuntimeStore)
    (workspaceRoot: string)
    (sessionKey: string)
    (lease: NudgeLease)
    (abortRun: string -> JS.Promise<unit>)
    : JS.Promise<unit> =
    promise {
        let session = runtime.GetSession sessionKey

        if session.AbortUnavailable then
            do!
                finishNudge
                    runtime
                    workspaceRoot
                    sessionKey
                    lease
                    NudgeOutcome.AbortUnknown
                    abortUnavailableMessage
                    ""
                    ""
        else
            let! abortResult = safeNudgeAbort abortRun sessionKey

            match abortResult with
            | Ok() ->
                do!
                    finishNudge
                        runtime
                        workspaceRoot
                        sessionKey
                        lease
                        NudgeOutcome.Cancelled
                        "Cancelled after dispatch"
                        ""
                        ""
            | Error ex when isAbortUnavailableMessage (string ex) ->
                runtime.Update(sessionKey, setAbortUnavailable true)

                do!
                    finishNudge
                        runtime
                        workspaceRoot
                        sessionKey
                        lease
                        NudgeOutcome.AbortUnknown
                        ("AbortUnavailable: " + (string ex))
                        ""
                        ""
            | Error ex -> return! Promise.reject ex
    }
