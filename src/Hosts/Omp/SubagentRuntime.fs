module Wanxiangshu.Hosts.Omp.SubagentRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.PiResolve
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions
open Wanxiangshu.Runtime.SubsessionService
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Runtime.ErrorClassify
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Hosts.Omp.SubsessionHostAdapter

module Dyn = Wanxiangshu.Runtime.Dyn

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

// ARCHITECTURE_EXEMPT: split this 94-line function later
let runOmpSubagentCore
    (fallbackRuntime: FallbackRuntimeStore)
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
            |> Option.defaultValue Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig

        let agentName = fallbackRuntime.GetAgentName childId

        let parentLiveModel =
            match fallbackRuntime.GetModel parentSessionId with
            | Some m -> Some m
            | None -> fallbackRuntime.GetModel childId

        let chain =
            Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.resolveSubagentChain
                cfg
                agentName
                (fallbackRuntime.GetChain childId)
                (fallbackRuntime.GetChain parentSessionId)
                parentLiveModel

        // Determine directive: non-empty chain → wanxiangshu owns retry; empty → delegate to host.
        let directive = if chain.IsEmpty then DelegateToHost else RetryChain chain

        // Cache the resolved chain so continue/recovery reuse it.
        fallbackRuntime.SetChain childId chain

        match List.tryHead chain with
        | Some first -> fallbackRuntime.SetModel childId first
        | None -> ()

        let hostFactory (_sid: string) = createHost session agentName pi

        let root = Wanxiangshu.Hosts.Omp.MagicTodo.workspaceRoot
        let eventStoreFactory (_sid: string) = create root
        let service = SubsessionService(root, hostFactory, eventStoreFactory)

        try
            let! runResult =
                match abortSignal with
                | Some sig_ -> service.StartRun(childId, parentSessionId, prompt, cfg, directive, abortSignal = sig_)
                | None -> service.StartRun(childId, parentSessionId, prompt, cfg, directive)

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
