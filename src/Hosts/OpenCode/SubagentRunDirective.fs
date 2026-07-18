module Wanxiangshu.Hosts.Opencode.SubagentRunDirective

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.Fallback.FallbackMessageCodec
open Wanxiangshu.Runtime.DelegatedAiSettings
open Wanxiangshu.Runtime.OpencodeClientCodec
open Wanxiangshu.Hosts.Opencode.SubagentTypes
open Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection
open Wanxiangshu.Runtime.ChildAgentRegistry

module Dyn = Wanxiangshu.Runtime.Dyn

open Wanxiangshu.Runtime.SessionIoSpawn
open Wanxiangshu.Hosts.Opencode.SubagentSpawn

let resolveParentLiveModel
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (parentSessionID: string)
    : JS.Promise<FallbackModel option> =
    promise {
        if parentSessionID = "" then
            return None
        else
            match (runtime.GetSession parentSessionID).Model with
            | Some m -> return Some m
            | None ->
                match getSessionApiFromClient client with
                | Error _ -> return None
                | Ok session ->
                    let! msgsResp = invoke1 (box {| path = box {| id = parentSessionID |} |}) "messages" session
                    let data = Dyn.get msgsResp "data"
                    let msgs = if Dyn.isArray data then unbox<obj[]> data else [||]

                    match Wanxiangshu.Runtime.Fallback.FallbackMessageCodec.tryGetLatestUserModel msgs with
                    | Some m -> return Some m
                    | None ->
                        return!
                            Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection.tryReadCurrentModel
                                client
                                parentSessionID
    }

let applyDirective (runtime: FallbackRuntimeStore) (childID: string) (directive: ModelDirective) : unit =
    match directive with
    | RetryChain chain ->
        runtime.UpdateSession(childID, selectChain chain)
        runtime.UpdateSession(childID, selectModel (List.head chain))
    | DelegateToHost -> runtime.UpdateSession(childID, selectChain [])

let extractRunDirective
    (registry: ChildAgentRegistry)
    (runtime: FallbackRuntimeStore)
    (client: obj)
    (agent: string)
    (directory: string)
    (sessionID: string)
    (childID: string)
    : JS.Promise<FallbackConfig * ModelDirective> =
    promise {
        let dir = if directory = "" then "." else directory

        let cfg =
            match Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.loadFallbackConfig dir with
            | Some c -> c
            | None -> Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.emptyConfig

        let parentSessionID =
            if sessionID = "" then
                ""
            else
                registry.ResolveSubsessionParentID(Some sessionID)
                |> Option.defaultValue sessionID

        let! parentLiveModel = resolveParentLiveModel runtime client parentSessionID

        let! hostExplicitModelOpt =
            Wanxiangshu.Hosts.Opencode.Fallback.HostEventInspection.tryGetAgentExplicitModel client agent

        let hostConfigured = Option.isSome hostExplicitModelOpt

        return
            cfg,
            Wanxiangshu.Runtime.Fallback.FallbackConfigCodec.resolveModelDirective
                cfg
                agent
                hostConfigured
                ((runtime.GetSession childID).Chain)
                ((runtime.GetSession parentSessionID).Chain)
                parentLiveModel
    }
