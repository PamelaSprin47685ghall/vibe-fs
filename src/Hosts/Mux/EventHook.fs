module Wanxiangshu.Hosts.Mux.EventHook

open Fable.Core
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.NudgeRuntime
open Wanxiangshu.Runtime.NudgeRuntimeEvent
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.Ports
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Hosts.Mux.Fallback.Hook
open Wanxiangshu.Hosts.Mux.EventHookCleanup
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.SubsessionChildObserver
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore
open Wanxiangshu.Hosts.Mux.EventHookHandlers
open Wanxiangshu.Runtime.Dispatch
open Wanxiangshu.Runtime.SubsessionPendingEvidence

let createEventHook (deps: obj) (reviewStore: ReviewStore) (scope: RuntimeScope) : obj =
    let getChatHistory =
        if Dyn.isNullish deps then
            None
        else
            let getter = Dyn.get deps "getChatHistory"

            if Dyn.isNullish getter then
                None
            else
                Some(fun (workspaceId: string) -> unbox<JS.Promise<obj array>> (Dyn.call1 getter workspaceId))

    let directory = if Dyn.isNullish deps then "" else Dyn.str deps "directory"

    let fallbackRuntime = FallbackRuntimeStore()
    scope.Add("fallbackRuntime", box fallbackRuntime)
    let fallbackConfigOpt = loadFallbackConfig directory

    let isReviewLoopActive (sessionID: string) =
        match reviewStore.getReviewState (sessionID) with
        | Some state -> ReviewSession.StateMachine.isActive state
        | None -> false

    let runtime =
        createNudgeRuntime getChatHistory directory fallbackRuntime isReviewLoopActive

    let configLookup: ConfigLookup =
        match fallbackConfigOpt with
        | Some cfg -> (fun _ -> cfg)
        | None -> (fun _ -> emptyConfig)

    let fallbackHandler =
        createMuxFallbackHandler fallbackRuntime configLookup deps directory

    let fn =
        System.Func<obj, obj, JS.Promise<unit>>(fun event helpers ->
            let decoded = decodeHookEvent event

            if not (shouldObserveMuxEvent decoded.eventType) then
                Promise.lift ()
            else
                processMuxEvent
                    decoded
                    fallbackRuntime
                    directory
                    scope
                    reviewStore
                    runtime
                    fallbackHandler
                    event
                    helpers)

    box fn
