module Wanxiangshu.Runtime.SubsessionService

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.CommandProcessor
open Wanxiangshu.Runtime.SubsessionPorts
open Wanxiangshu.Runtime.SubsessionActor
open Wanxiangshu.Runtime.SubsessionPendingEvidence
open Wanxiangshu.Runtime.SubsessionActorRegistry
open Wanxiangshu.Runtime.SubsessionEventStore

type SubsessionService
    (
        workspaceRoot: string,
        hostFactory: string -> ISubsessionHost,
        ?eventStoreFactory: string -> ISubsessionEventStore,
        ?initBarrier: unit -> JS.Promise<unit>
    ) =

    let storeFor (childId: string) =
        match eventStoreFactory with
        | Some f -> f childId
        | None -> create ""

    let waitInit () =
        match initBarrier with
        | Some barrier -> barrier ()
        | None -> Promise.lift ()

    member _.StartRun
        (
            childSessionId: string,
            parentSessionId: string,
            prompt: string,
            cfg: FallbackConfig,
            directive: ModelDirective,
            ?abortSignal: obj
        ) : JS.Promise<RunResult> =
        promise {
            // Initialization barrier: reconcile must complete before any StartRun.
            do! waitInit ()

            let host = hostFactory childSessionId
            let store = storeFor childSessionId

            let actor =
                SubsessionActorRegistry.GetOrCreate workspaceRoot childSessionId host store

            // Snapshot aborted BEFORE BeginRun so InitiallyCancelled is set when
            // the call already cancelled. Race after this snapshot is closed by:
            //   1) atomic BeginRun (register+decide+append+commit in one queue item)
            //   2) bind listener AFTER BeginRun is queued
            //   3) re-check aborted and Post CancelRequested (now after StartRun in queue)
            let initiallyCancelled =
                match abortSignal with
                | Some signal when not (isNull signal) && not (Dyn.isNullish signal) ->
                    Dyn.truthy (Dyn.get signal "aborted")
                | _ -> false

            let request =
                { RunId = RunId.newId ()
                  SessionId = SessionId.create childSessionId
                  ParentSessionId = SessionId.create parentSessionId
                  Prompt = prompt
                  FallbackConfig = cfg
                  Directive = directive
                  InitiallyCancelled = initiallyCancelled }

            let turnEpoch = SubsessionPendingEvidence.BeginRun childSessionId

            // 1. Atomic BeginRun enqueued first.
            let runPromise = actor.BeginRun request

            // Drain evidence that arrived before the actor had an active turn.
            let buffered = SubsessionPendingEvidence.TakeAllEpoch childSessionId turnEpoch

            for ev in buffered do
                actor.Post(EvidenceUpdated { TurnId = None; Evidence = ev }) |> ignore

            // 2. Bind AbortSignal AFTER BeginRun so CancelRequested is ordered after StartRun.
            let mutable onAbort: System.Action<obj> option = None
            let mutable boundSignal: obj option = None

            try
                match abortSignal with
                | Some signal when not (isNull signal) && not (Dyn.isNullish signal) && not initiallyCancelled ->
                    let handler = System.Action<obj>(fun _ -> actor.Post CancelRequested |> ignore)

                    onAbort <- Some handler
                    boundSignal <- Some signal

                    try
                        signal?addEventListener ("abort", handler)
                    with _ ->
                        try
                            signal?addEventListener ("abort", box handler)
                        with _ ->
                            ()

                    // 3. Re-check: if aborted between snapshot and now, Post Cancel
                    //    (queue order: StartRun then CancelRequested).
                    if Dyn.truthy (Dyn.get signal "aborted") then
                        do! actor.Post CancelRequested
                | _ -> ()

                // 5. Await run result.
                return! runPromise
            finally
                match boundSignal, onAbort with
                | Some signal, Some handler ->
                    try
                        signal?removeEventListener ("abort", handler)
                    with _ ->
                        try
                            signal?removeEventListener ("abort", box handler)
                        with _ ->
                            ()
                | _ -> ()

                SubsessionPendingEvidence.EndRun childSessionId turnEpoch
        }

    member _.TryPost (childSessionId: string) (cmd: Command) : JS.Promise<unit> =
        match SubsessionActorRegistry.TryGet workspaceRoot childSessionId with
        | Some actor -> actor.Post cmd
        | None -> Promise.lift ()

    member _.RemoveSession(childSessionId: string) : unit =
        SubsessionActorRegistry.Remove workspaceRoot childSessionId
        SubsessionPendingEvidence.ForgetSession childSessionId
