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

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

let runOmpSubagentCore
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    (childId: string)
    (session: obj)
    (prompt: string)
    (resetTaskComplete: bool)
    : JS.Promise<string> =
    promise {
        if childId <> "" && fallbackConfigOpt.IsSome then
            let initSt = fallbackRuntime.GetOrCreateState childId

            if resetTaskComplete then
                fallbackRuntime.UpdateState
                    childId
                    { initSt with
                        Lifecycle = FallbackLifecycle.Active }

            fallbackRuntime.SetSubsessionPending childId true

        try
            do! sessionPrompt session prompt
            do! sessionWaitForIdle session

            if childId <> "" then
                fallbackRuntime.SetSubsessionPending childId false

                if fallbackConfigOpt.IsSome then
                    do! waitForSubagentSettle fallbackRuntime childId

            let st = fallbackRuntime.GetOrCreateState childId

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
                return readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
        with err ->
            if childId <> "" && fallbackConfigOpt.IsSome then
                match translateJsError err with
                | MessageAborted
                | ClientCancellation _ ->
                    fallbackRuntime.SetSubsessionPending childId false
                    return abortedPrefix
                | other ->
                    do! waitForSubagentSettle fallbackRuntime childId
                    fallbackRuntime.SetSubsessionPending childId false

                    let st = fallbackRuntime.GetOrCreateState childId
                    let isSuccess = st.Lifecycle = FallbackLifecycle.TaskComplete

                    if isSuccess then
                        let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                        return readAssistantText sm 0 "\n\n" |> Option.defaultValue noOutputText
                    else
                        return! Promise.reject err
            else
                return! Promise.reject err
    }
