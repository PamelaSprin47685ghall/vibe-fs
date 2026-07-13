module Wanxiangshu.Omp.ChildSessionCommon

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OmpHostBindings
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.FallbackRecoveryWait
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Shell.Dyn

[<RequireQualifiedAccess>]
type SubagentResetPolicy =
    | ResetToActive
    | KeepState

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

let runOmpSubagentCore
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    (childId: string)
    (session: obj)
    (prompt: string)
    (resetPolicy: SubagentResetPolicy)
    (parentSessionId: string)
    : JS.Promise<string> =
    promise {
        let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")

        let startCount =
            let msgs = Dyn.get sm "messages"

            if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                0
            else
                (unbox<obj[]> msgs).Length

        let runId = "run-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)

        if childId <> "" && fallbackConfigOpt.IsSome then
            let initSt = fallbackRuntime.GetOrCreateState childId

            match resetPolicy with
            | SubagentResetPolicy.ResetToActive ->
                fallbackRuntime.UpdateState
                    childId
                    { initSt with
                        Lifecycle = FallbackLifecycle.Active }
            | SubagentResetPolicy.KeepState -> ()

            fallbackRuntime.StartSubsessionRun(childId, parentSessionId, runId)
            fallbackRuntime.SetSubsessionPending childId true

        try
            do! sessionPrompt session prompt
            do! sessionWaitForIdle session

            if childId <> "" then
                fallbackRuntime.SetSubsessionPending childId false

                if fallbackConfigOpt.IsSome then
                    do! waitForSubagentSettle fallbackRuntime childId runId

            let st = fallbackRuntime.GetOrCreateState childId

            if childId <> "" then
                let status =
                    if st.Lifecycle = FallbackLifecycle.TaskComplete then
                        SubsessionRunStatus.Settled
                    elif st.Lifecycle = FallbackLifecycle.Cancelled then
                        SubsessionRunStatus.Cancelled
                    elif st.Phase = FallbackPhase.Exhausted then
                        SubsessionRunStatus.Failed
                    else
                        SubsessionRunStatus.Settled

                fallbackRuntime.UpdateSubsessionRunStatus(childId, runId, status)

            if
                fallbackConfigOpt.IsSome
                && st.Lifecycle <> FallbackLifecycle.TaskComplete
                && st.Lifecycle <> FallbackLifecycle.Cancelled
            then
                return! Promise.reject (failwith "Subagent failed to complete")
            elif st.Lifecycle = FallbackLifecycle.Cancelled then
                return abortedPrefix
            else
                let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                let msgs = Dyn.get sm "messages"

                let lastUserIdx =
                    if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                        0
                    else
                        let arr = unbox<obj[]> msgs

                        arr
                        |> Array.tryFindIndexBack (fun m -> Dyn.str m "role" = "user")
                        |> Option.defaultValue 0

                let finalStartIndex =
                    if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                        lastUserIdx
                    else
                        let arr = unbox<obj[]> msgs

                        if startCount >= arr.Length then lastUserIdx
                        else if lastUserIdx >= startCount then lastUserIdx
                        else startCount

                return readAssistantText sm finalStartIndex "\n\n" |> Option.defaultValue noOutputText
        with err ->
            if childId <> "" && fallbackConfigOpt.IsSome then
                do! waitForSubagentSettle fallbackRuntime childId runId
                fallbackRuntime.SetSubsessionPending childId false

                let st = fallbackRuntime.GetOrCreateState childId

                let status =
                    if st.Lifecycle = FallbackLifecycle.TaskComplete then
                        SubsessionRunStatus.Settled
                    elif st.Lifecycle = FallbackLifecycle.Cancelled then
                        SubsessionRunStatus.Cancelled
                    else
                        SubsessionRunStatus.Failed

                fallbackRuntime.UpdateSubsessionRunStatus(childId, runId, status)

                match translateJsError err with
                | MessageAborted
                | ClientCancellation _ -> return abortedPrefix
                | other ->
                    if st.Lifecycle = FallbackLifecycle.TaskComplete then
                        let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                        let msgs = Dyn.get sm "messages"

                        let lastUserIdx =
                            if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                                0
                            else
                                let arr = unbox<obj[]> msgs

                                arr
                                |> Array.tryFindIndexBack (fun m -> Dyn.str m "role" = "user")
                                |> Option.defaultValue 0

                        let finalStartIndex =
                            if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                                lastUserIdx
                            else
                                let arr = unbox<obj[]> msgs

                                if startCount >= arr.Length then lastUserIdx
                                else if lastUserIdx >= startCount then lastUserIdx
                                else startCount

                        return readAssistantText sm finalStartIndex "\n\n" |> Option.defaultValue noOutputText
                    else
                        return! Promise.reject err
            else
                return! Promise.reject err
    }
