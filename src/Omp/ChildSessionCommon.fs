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
open Wanxiangshu.Shell.ChildSessionMailbox
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types

module Dyn = Wanxiangshu.Shell.Dyn

type OmpSessionTurnHost(pi: obj, childId: string, fallbackRuntime: FallbackRuntimeState, session: obj) =
    interface ISessionTurnHost with
        member _.RunOneTurn(sessionId, model, prompt) =
            let deferred = createDeferred<TurnOutcome> ()

            let mailbox =
                ChildSessionMailboxRegistry.GetOrCreate(sessionId, (fun () -> sessionAbort session |> ignore))

            let sendFn turnId =
                promise {
                    let modelStr =
                        match model.Variant with
                        | Some v -> sprintf "%s/%s:%s" model.ProviderID model.ModelID v
                        | None -> sprintf "%s/%s" model.ProviderID model.ModelID

                    let pObj =
                        let p =
                            {| text = prompt
                               model = modelStr
                               continuationID = turnId |}

                        let agent = fallbackRuntime.GetAgentName sessionId
                        if agent <> "" then Dyn.withKey p "agent" agent else box p

                    let body = box {| prompt = pObj |}
                    let arg = box {| sessionId = sessionId; body = body |}

                    try
                        do! unbox<JS.Promise<obj>> (session?prompt (arg)) |> Promise.map ignore
                    with _ex ->
                        () // Prompt rejection handled via event bridge

                    do! mailbox.Post(TurnStarted(turnId))
                }

            let turnId =
                "wanxiangshu-turn-" + System.Guid.NewGuid().ToString("N").Substring(0, 8)

            mailbox.Post(RunTurn(model, prompt, turnId, sendFn, deferred)) |> ignore
            deferred.Promise


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
    (pi: obj)
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
            let started = fallbackRuntime.StartSubsessionRun(childId, parentSessionId, runId)

            if not started then
                failwith "Subagent session already running"
            else
                fallbackRuntime.SetSubsessionPending childId true
                let root = Wanxiangshu.Omp.MagicTodo.workspaceRoot

                if root <> "" then
                    let! exists = Wanxiangshu.Shell.EventLogRuntimeStore.directoryExists root

                    if exists then
                        do!
                            Wanxiangshu.Shell.EventLogRuntimeAppend.appendSubsessionRunStartedOrFail
                                root
                                childId
                                childId
                                parentSessionId
                                runId

        try
            try
                try
                    let host = OmpSessionTurnHost(pi, childId, fallbackRuntime, session)

                    let cfg =
                        fallbackConfigOpt
                        |> Option.defaultValue Wanxiangshu.Shell.FallbackConfigCodec.emptyConfig

                    let agentName = fallbackRuntime.GetAgentName childId

                    let chain =
                        let configChain =
                            let agentChainOpt =
                                if agentName <> "" then
                                    Map.tryFind agentName cfg.AgentChains
                                else
                                    None

                            match agentChainOpt with
                            | Some c -> c
                            | None -> cfg.DefaultChain

                        if configChain.IsEmpty then
                            let runtimeChain = fallbackRuntime.GetChain childId

                            if runtimeChain.IsEmpty then
                                [ { ProviderID = "default"
                                    ModelID = "default"
                                    Variant = None
                                    Temperature = None
                                    TopP = None
                                    MaxTokens = None
                                    ReasoningEffort = None
                                    Thinking = false } ]
                            else
                                runtimeChain
                        else
                            configChain

                    let fetchMessages (sid: string) =
                        promise {
                            let sessionApi = Dyn.get pi "session"

                            if not (Dyn.isNullish sessionApi) then
                                let arg = box {| sessionId = sid |}
                                let! resp = unbox<JS.Promise<obj>> (sessionApi?sessionMessages (arg))
                                let data = Dyn.get resp "data"

                                if Dyn.isArray data then
                                    return (unbox<obj[]> data)
                                else
                                    return [||]
                            else
                                return [||]
                        }

                    let! loopResult = runSubsessionLoop host childId prompt cfg chain fallbackRuntime fetchMessages

                    let st = fallbackRuntime.GetOrCreateState childId

                    let isSuccess =
                        match loopResult with
                        | Ok() -> true
                        | Error _ -> false

                    let status =
                        if isSuccess then
                            SubsessionRunStatus.Settled
                        elif st.Lifecycle = FallbackLifecycle.Cancelled then
                            SubsessionRunStatus.Cancelled
                        elif st.Phase = FallbackPhase.Exhausted then
                            SubsessionRunStatus.Failed
                        else
                            SubsessionRunStatus.Settled

                    fallbackRuntime.UpdateSubsessionRunStatus(childId, runId, status)

                    if st.Lifecycle = FallbackLifecycle.Cancelled then
                        return abortedPrefix
                    elif isSuccess then
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
                        match loopResult with
                        | Error msg -> return! Promise.reject (failwith msg)
                        | Ok() -> return ""
                with err ->
                    let st = fallbackRuntime.GetOrCreateState childId
                    let isSuccess = st.Lifecycle = FallbackLifecycle.TaskComplete

                    let status =
                        if isSuccess then
                            SubsessionRunStatus.Settled
                        elif st.Lifecycle = FallbackLifecycle.Cancelled then
                            SubsessionRunStatus.Cancelled
                        else
                            SubsessionRunStatus.Failed

                    fallbackRuntime.UpdateSubsessionRunStatus(childId, runId, status)

                    if st.Lifecycle = FallbackLifecycle.Cancelled then
                        return abortedPrefix
                    elif isSuccess then
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
            finally
                ChildSessionMailboxRegistry.Remove childId

                if childId <> "" then
                    fallbackRuntime.ClearSubsessionPending childId
                    fallbackRuntime.ClearSubsessionRun(childId, runId)
        with err ->
            return! Promise.reject err
    }
