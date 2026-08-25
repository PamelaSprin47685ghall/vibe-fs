// primary_owner: participant-horizon — ParticipantHorizon.SurfaceSurface — KEEP — participant-horizon-surface verified
namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// HOST-BOUNDARY-008 event-driven projection catch-up. The Host can publish the
/// assistant message before its public session projection is readable; a
/// `message.updated` signal is the causal hint that a re-read can now observe
/// it. Waiters are per-session one-shot completions with an ITimerPort deadline
/// backstop: the event is the fast path, the deadline keeps the re-read bounded
/// when no event ever arrives. Over-wake costs one extra bounded re-read;
/// under-wake is absorbed by the deadline.
type private VisibilityWaiter =
    { Completion: TaskCompletionSource<unit>
      Deadline: IDeadlineHandle }

type MessageVisibilityHub(timerPort: ITimerPort) =

    // DSL-MUTABLE: resource — per-session one-shot waiter registry
    let waiters = Dictionary<string, ResizeArray<VisibilityWaiter>>()
    let gate = obj ()

    let dropKeyWhenEmpty (key: string) (list: ResizeArray<VisibilityWaiter>) =
        if list.Count = 0 then
            waiters.Remove key |> ignore

    let removeWaiter (key: string) (waiter: VisibilityWaiter) =
        match waiters.TryGetValue key with
        | true, list ->
            list.Remove waiter |> ignore
            dropKeyWhenEmpty key list
        | _ -> ()

    let settle (key: string) (waiter: VisibilityWaiter) =
        lock gate (fun () -> removeWaiter key waiter)
        AsyncSupport.trySetResult waiter.Completion () |> ignore

    /// Wake every waiter whose session just published a message lifecycle event.
    member _.Notify(sessionId: SessionId) =
        let key = SessionId.value sessionId

        let pending =
            lock gate (fun () ->
                match waiters.TryGetValue key with
                | true, list ->
                    waiters.Remove key |> ignore
                    Seq.toList list
                | _ -> [])

        for waiter in pending do
            waiter.Deadline.Cancel()
            AsyncSupport.trySetResult waiter.Completion () |> ignore

    /// Resolves on the next `message.updated` for the session, or when the
    /// deadline budget expires without one. A settled waiter never stays
    /// registered.
    member _.AwaitChange (sessionId: SessionId) (budgetMilliseconds: int) : Task<unit> =
        let key = SessionId.value sessionId
        let completion = TaskCompletionSource<unit>()

        let waiter =
            { Completion = completion
              Deadline = timerPort.Delay budgetMilliseconds }

        lock gate (fun () ->
            match waiters.TryGetValue key with
            | true, list -> list.Add waiter
            | _ -> waiters.[key] <- ResizeArray [ waiter ])

        emitJsExpr (waiter.Deadline.Delay, (fun () -> settle key waiter)) "$0.then($1)"
        |> ignore

        completion.Task

    /// Pending waiter count for one session — leak-detection probe.
    member _.PendingCount(sessionId: SessionId) =
        lock gate (fun () ->
            match waiters.TryGetValue(SessionId.value sessionId) with
            | true, list -> list.Count
            | _ -> 0)

/// Raw-event intake for the hub: any `message.updated` is a potential
/// projection change for its session.
[<RequireQualifiedAccess>]
module MessageVisibilitySignal =

    let observeEvent (hub: MessageVisibilityHub) (rawInput: obj) =
        let raw = HostEventCodec.unwrap rawInput

        if HostEventCodec.eventTypeOf raw = "message.updated" then
            HostEventCodec.tryMessageSessionId raw |> Option.iter hub.Notify
