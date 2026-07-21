module Wanxiangshu.Hosts.Omp.SubagentRuntime

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Hosts.Omp.MessagingCodec
open Wanxiangshu.Hosts.Omp.PiResolve
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OmpHostBindings
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
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

let private getStartCount (session: obj) : int =
    let msgs = Dyn.get (Dyn.get session "sessionManager") "messages"

    if Dyn.isNullish msgs || not (Dyn.isArray msgs) then
        0
    else
        (unbox<obj[]> msgs).Length

let private resolveChainAndDirective
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    (childId: string)
    (parentSessionId: string)
    : FallbackConfig * string * FallbackChain * ModelDirective =
    let cfg =
        fallbackConfigOpt
        |> Option.defaultValue Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig

    let agentName = (fallbackRuntime.GetSession childId).AgentName

    let parentLiveModel =
        match (fallbackRuntime.GetSession parentSessionId).Model with
        | Some m -> Some m
        | None -> (fallbackRuntime.GetSession childId).Model

    let chain =
        Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.resolveSubagentChain
            cfg
            agentName
            (fallbackRuntime.GetSession childId).Chain
            (fallbackRuntime.GetSession parentSessionId).Chain
            parentLiveModel

    // Determine directive: non-empty chain → wanxiangshu owns retry; empty → delegate to host.
    let directive = if chain.IsEmpty then DelegateToHost else RetryChain chain

    // Cache the resolved chain so continue/recovery reuse it.
    fallbackRuntime.UpdateSession(childId, selectChain chain)

    match List.tryHead chain with
    | Some first -> fallbackRuntime.UpdateSession(childId, selectModel first)
    | None -> ()

    cfg, agentName, chain, directive

let private readSuccessText (sm: ISessionManager) (startCount: int) : string =
    let msgs = Dyn.get (box sm) "messages"

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

    readAssistantText sm finalStartIndex "\n\n" |> Option.defaultValue noOutputText

let runOmpSubagentCore
    (fallbackRuntime: FallbackRuntimeStore)
    (fallbackConfigOpt: FallbackConfig option)
    (root: string)
    (childId: string)
    (session: obj)
    (prompt: string)
    (resetPolicy: SubagentResetPolicy)
    (parentSessionId: string)
    (pi: obj)
    (abortSignal: obj option)
    : JS.Promise<string> =
    promise {
        let startCount = getStartCount session

        let (cfg, agentName, _, directive) =
            resolveChainAndDirective fallbackRuntime fallbackConfigOpt childId parentSessionId

        let workspaceRoot =
            if root <> "" then
                root
            else
                let fromSession = Dyn.str session "cwd"

                if fromSession <> "" then
                    fromSession
                else
                    let fromPi = Dyn.str pi "workspaceRoot"
                    if fromPi <> "" then fromPi else Dyn.str pi "cwd"

        let hostFactory (_sid: string) =
            createHost session agentName pi workspaceRoot

        let eventStoreFactory (_sid: string) = create workspaceRoot
        let service = SubsessionService(workspaceRoot, hostFactory, eventStoreFactory)

        try
            let! runResult =
                match abortSignal with
                | Some sig_ -> service.StartRun(childId, parentSessionId, prompt, cfg, directive, abortSignal = sig_)
                | None -> service.StartRun(childId, parentSessionId, prompt, cfg, directive)

            match runResult with
            | Succeeded output ->
                let sm = unbox<ISessionManager> (Dyn.get session "sessionManager")
                let text = readSuccessText sm startCount

                if text <> noOutputText && text <> "" then return text
                elif output <> "" then return output
                else return text
            | Cancelled -> return abortedPrefix
            | Failed reason -> return! Promise.reject (failwith (formatRunFailure reason))
        with err ->
            return! Promise.reject err
    }
