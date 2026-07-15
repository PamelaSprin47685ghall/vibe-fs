module Wanxiangshu.Omp.ChildSessionCommon

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Omp.MessagingCodec
open Wanxiangshu.Omp.PiResolve
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OmpHostBindings
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.SubsessionService
open Wanxiangshu.Shell.SubsessionEventStore
open Wanxiangshu.Shell.ErrorClassify
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Omp.SubsessionHostAdapter

module Dyn = Wanxiangshu.Shell.Dyn

[<RequireQualifiedAccess>]
type SubagentResetPolicy =
    | ResetToActive
    | KeepState

let noOutputText = "(no output)"
let abortedPrefix = "(aborted)"

let private formatRunFailure (f: RunFailure) : string =
    match f with
    | NoModelConfigured -> "No model available in fallback chain"
    | FallbackExhausted err -> err.Message
    | RecoveryExhausted reason -> reason
    | ProtocolViolation reason -> reason
    | InfrastructureFailure reason -> reason

let runOmpSubagentCore
    (fallbackRuntime: FallbackRuntimeState)
    (fallbackConfigOpt: FallbackConfig option)
    (childId: string)
    (session: obj)
    (prompt: string)
    (resetPolicy: SubagentResetPolicy)
    (parentSessionId: string)
    (pi: obj)
    (abortSignal: obj option)
    : JS.Promise<string> =
    promise {
        let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")

        let startCount =
            let msgs = Dyn.get sm "messages"

            if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
                0
            else
                (unbox<obj[]> msgs).Length

        let cfg =
            fallbackConfigOpt
            |> Option.defaultValue Wanxiangshu.Shell.FallbackConfigCodec.emptyConfig

        let agentName = fallbackRuntime.GetAgentName childId

        let parentLiveModel =
            match fallbackRuntime.GetModel parentSessionId with
            | Some m -> Some m
            | None -> fallbackRuntime.GetModel childId

        let chain =
            Wanxiangshu.Shell.FallbackConfigCodec.resolveSubagentChain
                cfg
                agentName
                (fallbackRuntime.GetChain childId)
                (fallbackRuntime.GetChain parentSessionId)
                parentLiveModel

        if chain.IsEmpty then
            return! Promise.reject (failwith (formatRunFailure NoModelConfigured))

        // Cache the resolved chain so continue/recovery reuse it.
        fallbackRuntime.SetChain childId chain

        match List.tryHead chain with
        | Some first -> fallbackRuntime.SetModel childId first
        | None -> ()

        let hostFactory (_sid: string) = createHost session agentName pi

        let root = Wanxiangshu.Omp.MagicTodo.workspaceRoot
        let eventStoreFactory (_sid: string) = create root
        let service = SubsessionService(root, hostFactory, eventStoreFactory)

        try
            let! runResult =
                match abortSignal with
                | Some sig_ ->
                    service.StartRun(childId, parentSessionId, prompt, cfg, RetryChain chain, abortSignal = sig_)
                | None -> service.StartRun(childId, parentSessionId, prompt, cfg, RetryChain chain)

            match runResult with
            | Succeeded _output ->
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
            | Cancelled -> return abortedPrefix
            | Failed reason -> return! Promise.reject (failwith (formatRunFailure reason))
        with err ->
            return! Promise.reject err
    }
