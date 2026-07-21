module Wanxiangshu.Runtime.Session.SessionActor

open Fable.Core
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Session.SessionFact
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Session.SessionActorState

/// Domain handler invoked only after the mailbox admits a fact.
type SessionFactHandler = SessionActorSnapshot -> SessionFact -> JS.Promise<unit>

/// Per-physical-session mailbox. Hooks enqueue facts; effect runners re-enter
/// with EffectIdentity. Long host work must not run synchronously here — the
/// handler may schedule effects that later Post back with a captured generation.
type SessionActor(workspaceKey: string, sessionId: string) =
    let queue = SerialQueue()
    let mutable snap = SessionActorSnapshot.empty
    let mutable handler: SessionFactHandler = fun _ _ -> Promise.lift ()
    let mutable handlerBound = false

    let commit (work: unit -> JS.Promise<'T>) : JS.Promise<'T> = queue.Enqueue work

    member _.WorkspaceKey = workspaceKey
    member _.SessionId = sessionId
    member _.Snapshot() : SessionActorSnapshot = snap
    member _.Generation = snap.Generation
    member _.IsClosed = snap.Closed
    member _.IsHandlerBound = handlerBound

    member _.Poisoned
        with get () = queue.Poisoned
        and set (v) = queue.Poisoned <- v

    member _.ResetPoison() : unit = queue.ResetPoison()

    /// Bind/replace the domain handler. Production hooks rebind on each event
    /// so a process-global actor always closes over the live CoreServices.
    member _.BindHandler(next: SessionFactHandler) : unit =
        handler <- next
        handlerBound <- true

    /// Force-replace handler (tests only).
    member _.ReplaceHandler(next: SessionFactHandler) : unit =
        handler <- next
        handlerBound <- true

    member _.BumpGeneration() : JS.Promise<int> =
        commit (fun () ->
            promise {
                snap <- SessionActorTransition.bumpGeneration snap
                return snap.Generation
            })

    member _.SetOwner(owner: SessionOwner) : JS.Promise<unit> =
        commit (fun () ->
            promise {
                snap <- SessionActorTransition.setOwner owner snap
            })

    member _.SetActiveDispatch(dispatchId: string option) : JS.Promise<unit> =
        commit (fun () ->
            promise {
                snap <- SessionActorTransition.setActiveDispatch dispatchId snap
            })

    /// Enqueue a fact. Returns after admission + domain handler complete.
    /// Dropped facts resolve successfully without invoking the handler.
    member _.Post(fact: SessionFact) : JS.Promise<FactAdmission> =
        commit (fun () ->
            promise {
                let decision = FactAdmission.decide snap fact

                if not (FactAdmission.isAccepted decision) then
                    snap <- SessionActorTransition.recordDropped snap
                    return decision
                else
                    snap <- SessionActorTransition.applyFactEpoch fact snap
                    let view = snap
                    do! handler view fact
                    snap <- SessionActorTransition.recordAccepted snap
                    return FactAdmission.Accept
            })

    /// Effect-result entry: builds a gated fact and posts it.
    member this.PostEffect
        (identity: EffectIdentity)
        (build: EffectIdentity -> SessionFact)
        : JS.Promise<FactAdmission> =
        this.Post(build identity)

    /// Capture generation for an outbound effect so its result can re-enter.
    member _.CaptureEffectIdentity(?dispatchId: string, ?owner: SessionOwner) : EffectIdentity =
        { ExpectedGeneration = snap.Generation
          ExpectedDispatchId = dispatchId
          ExpectedOwner = owner }

    /// Fire-and-forget effect launch: run work off-mailbox, then re-enter.
    /// Caller must embed EffectIdentity inside the produced fact.
    member this.LaunchEffect(work: unit -> JS.Promise<SessionFact>) : unit =
        work ()
        |> Promise.map (fun fact -> this.Post fact |> ignore)
        |> Promise.catch (fun _ -> ())
        |> ignore
