namespace Wanxiangshu.Context.Companion.Blogger.OpenCode

open Wanxiangshu.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Git

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// ENFORCER-*: Blogger continuation parking, physical flight ownership and
/// drain windows for one plugin instance. Parked transforms are per-session
/// serial (the dictionary entry is the guard); flights live in SharedState
/// because they must be visible across worktree/root instances.
type PluginBloggerScope() =
    let shutdown = new CancellationTokenSource()
    /// ENFORCER-160/162: parked continuation transforms, keyed by session id.
    ///
    /// At most one parked transform per session (a session's step loop is
    /// serial, so two parks for one session cannot race in practice — the
    /// dictionary entry is the guard that makes the invariant structural).
    let parkedGate = obj ()
    /// DSL-cross-callback-proof: physical waiter — ParkedTransform owns a TaskCompletionSource transport fence
    // DSL-MUTABLE: resource — parked continuation transform registry by session id
    let parked = Dictionary<string, ParkedTransform>()
    // ENFORCER-047/050: dual slots without dual storage for PendingOffer.
    // CurrentRequest ownership = physical flight registry (entry = in-flight).
    // PendingOffer = separate dictionary for the next Main material while Parked.
    /// DSL-cross-callback-proof: physical — one-shot inbound material buffer owned by Blogger convergence
    // DSL-MUTABLE: resource — pending offer registry by session id
    let pendingOffer = Dictionary<string, BloggerRequestContext>()
    /// DSL-cross-callback-proof: physical episode gate — guards episodes and completions
    let episodeGate = obj ()
    /// DSL-MUTABLE: resource — live repair episode registry by BloggerSessionId
    let episodes = Dictionary<SessionId, BloggerRepairRendezvous>()
    /// DSL-MUTABLE: resource — stored episode completions to await on drain
    let episodeCompletions = List<Task>()

    let repairEpisodeForSession (bloggerSessionId: SessionId) =
        lock episodeGate (fun () ->
            match episodes.TryGetValue bloggerSessionId with
            | true, rendezvous -> Some rendezvous
            | false, _ -> None)

    let parkExistingOrCreate sessionId =
        match parked.TryGetValue sessionId with
        | true, existing -> existing.Completion
        | false, _ ->
            let created = ParkedTransform(sessionId)
            parked.[sessionId] <- created
            created.Completion

    let createFlightLease
        (thisHost: IBloggerRuntimeHost)
        (sessionId: string)
        (requestId: BloggerRequestId)
        : IBloggerFlightLease =
        let leaseGate = obj ()
        // DSL-MUTABLE: resource — one-shot flight lease dispose latch
        let mutable disposed = false

        { new IBloggerFlightLease with
            member _.RequestId = requestId

            member _.Dispose() =
                let shouldRelease =
                    lock leaseGate (fun () ->
                        if disposed then
                            false
                        else
                            disposed <- true
                            true)

                if shouldRelease then
                    thisHost.ReleaseCurrentRequest(sessionId, requestId) |> ignore }

    let isSameRepairEpisode (existing: BloggerRepairRendezvous) (identity: BloggerRepairEpisodeIdentity) =
        existing.Identity = identity

    let removeOwnedRepairEpisode (bloggerSid: SessionId) (identity: BloggerRepairEpisodeIdentity) =
        lock episodeGate (fun () ->
            match episodes.TryGetValue bloggerSid with
            | true, current when isSameRepairEpisode current identity ->
                episodes.Remove bloggerSid |> ignore
                episodeCompletions.Remove current.Completion |> ignore
            | _ -> ())

    let createRegisteredRepairEpisode (bloggerSid: SessionId) (identity: BloggerRepairEpisodeIdentity) =
        let onCompleted () =
            removeOwnedRepairEpisode bloggerSid identity

        let created = BloggerRepairRendezvous(identity, onCompleted)
        episodes.Add(bloggerSid, created)
        episodeCompletions.Add(created.Completion)
        created

    let decideRepairEpisodeSlot (bloggerSid: SessionId) (identity: BloggerRepairEpisodeIdentity) =
        match episodes.TryGetValue bloggerSid with
        | true, existing when isSameRepairEpisode existing identity -> Ok existing
        | true, existing ->
            Error
                $"Conflicting episode already active for {SessionId.value bloggerSid}: existing req {BloggerRequestId.value existing.Identity.RequestId} root {AuthorityRootUserMessageId.value existing.Identity.AuthorityRoot}"
        | false, _ -> Ok(createRegisteredRepairEpisode bloggerSid identity)

    let claimRepairEpisodeSlot (bloggerSid: SessionId) (identity: BloggerRepairEpisodeIdentity) =
        lock episodeGate (fun () ->
            if shutdown.IsCancellationRequested then
                Error "Blogger runtime is shutting down"
            else
                decideRepairEpisodeSlot bloggerSid identity)

    let admitFlightRepairEpisode
        (bloggerSid: SessionId)
        (identity: BloggerRepairEpisodeIdentity)
        (flightCtx: BloggerRequestContext)
        =
        let flightReqId = BloggerRequestContext.requestId flightCtx

        if flightReqId <> identity.RequestId then
            Error
                $"Claimed request {BloggerRequestId.value identity.RequestId} does not match active flight {BloggerRequestId.value flightReqId}"
        else
            claimRepairEpisodeSlot bloggerSid identity

    let decideFlightRepairClaim (sidStr: string) (bloggerSid: SessionId) (identity: BloggerRepairEpisodeIdentity) =
        match SharedState.BloggerFlights.TryGetValue sidStr with
        | false, _ -> Error $"No active flight for blogger session {sidStr}"
        | true, flightCtx -> admitFlightRepairEpisode bloggerSid identity flightCtx

    let snapshotRepairCompletions () : Task array =
        lock episodeGate (fun () -> episodeCompletions |> Seq.toArray)

    let forgetRepairCompletions (tasksToAwait: Task array) =
        lock episodeGate (fun () ->
            for completed in tasksToAwait do
                episodeCompletions.Remove completed |> ignore)

    let combineDrainFault (firstFault: exn option) (ex: exn) : exn option =
        match firstFault with
        | Some _ -> firstFault
        | None -> Some ex

    let settleDrainOne (head: Task) (firstFault: exn option) : Task<exn option> =
        task {
            try
                do! head
                return firstFault
            with ex ->
                return combineDrainFault firstFault ex
        }

    let rec drainAllRepair (remaining: Task list) (firstFault: exn option) : Task<exn option> =
        task {
            match remaining with
            | [] -> return firstFault
            | head :: tail ->
                let! next = settleDrainOne head firstFault
                return! drainAllRepair tail next
        }

    let raiseDrainFault (firstFault: exn option) =
        match firstFault with
        | Some ex -> raise ex
        | None -> ()

    interface IBloggerRuntimeHost with
        member _.Cancellation = shutdown.Token

        member this.ParkTransform(sessionId: string) : Task<ParkWake> =
            lock parkedGate (fun () ->
                match pendingOffer.TryGetValue sessionId with
                | true, context ->
                    pendingOffer.Remove sessionId |> ignore
                    Task.FromResult(ParkWake.MaterialAvailable context)
                | false, _ -> parkExistingOrCreate sessionId)

        member this.CancelParked(sessionId: string) : unit =
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    entry.TryCancel()
                    parked.Remove sessionId |> ignore
                | false, _ -> ()

                pendingOffer.Remove sessionId |> ignore)

        member this.TryGetFlight(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ClaimCurrentRequest(sessionId: string, context: BloggerRequestContext) : BloggerFlightClaim =
            let reqId = BloggerRequestContext.requestId context

            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | false, _ ->
                    SharedState.BloggerFlights.Add(sessionId, context)
                    BloggerFlightClaim.Claimed(createFlightLease this sessionId reqId)
                | true, existing when BloggerRequestContext.requestId existing = reqId ->
                    SharedState.BloggerFlights.[sessionId] <- context
                    BloggerFlightClaim.Refreshed(createFlightLease this sessionId reqId)
                | true, existing -> BloggerFlightClaim.Conflict(BloggerRequestContext.requestId existing))

        member this.TryPeekCurrentRequest(sessionId: string) : BloggerRequestContext option =
            lock SharedState.BloggerFlightGate (fun () ->
                match SharedState.BloggerFlights.TryGetValue sessionId with
                | true, ctx -> Some ctx
                | false, _ -> None)

        member this.ReleaseCurrentRequest(sessionId: string, requestId: BloggerRequestId) : BloggerFlightRelease =
            let release, repairEpisode =
                lock SharedState.BloggerFlightGate (fun () ->
                    match SharedState.BloggerFlights.TryGetValue sessionId with
                    | false, _ -> BloggerFlightRelease.Missing, None
                    | true, existing when BloggerRequestContext.requestId existing = requestId ->
                        SharedState.BloggerFlights.Remove sessionId |> ignore

                        let exactEpisode =
                            repairEpisodeForSession (SessionId.create sessionId)
                            |> Option.filter (fun rendezvous -> rendezvous.Identity.RequestId = requestId)

                        BloggerFlightRelease.Released, exactEpisode
                    | true, existing -> BloggerFlightRelease.Conflict(BloggerRequestContext.requestId existing), None)

            repairEpisode |> Option.iter _.Cancel()
            release

        member _.AcquireMaterialization(sessionId: string) : Task<BloggerMaterializationLease> =
            SharedState.BloggerMaterializationAdmission.Acquire sessionId

        member _.TryDeliverMaterial(sessionId: string, context: BloggerRequestContext) : bool =
            let parkedTransform =
                lock parkedGate (fun () ->
                    match parked.TryGetValue sessionId with
                    | true, entry ->
                        parked.Remove sessionId |> ignore
                        Some entry
                    | false, _ -> None)

            match parkedTransform with
            | Some entry ->
                entry.TryResume context
                true
            | None -> false

        member this.OfferMaterial(sessionId: string, context: BloggerRequestContext) : MaterialOfferDisposition =
            // Explicit newest-covers business rule:
            // If a transform is parked awaiting material, deliver immediately and resume the waiter.
            // If no transform is currently parked, stage into pendingOffer; any previously staged
            // material for this session is cleanly superseded (newest covers unread material).
            // Mailbox provides atomic delivery without slot-peeking or race conditions.
            lock parkedGate (fun () ->
                match parked.TryGetValue sessionId with
                | true, entry ->
                    parked.Remove sessionId |> ignore
                    entry.TryResume context
                    MaterialOfferDisposition.Delivered
                | false, _ ->
                    pendingOffer.[sessionId] <- context
                    MaterialOfferDisposition.Staged)

        member this.ClaimRepairEpisode
            (identity: BloggerRepairEpisodeIdentity)
            : Result<BloggerRepairRendezvous, string> =
            let bloggerSid = identity.BloggerSessionId
            let sidStr = SessionId.value bloggerSid

            // Under one gate (episodeGate nested inside BloggerFlightGate or episodeGate alone):
            // Claim requires current exact Blogger flight request id, replays same identity,
            // rejects any foreign request/root identity without overwrite; no peek-before-claim TOCTOU.
            lock SharedState.BloggerFlightGate (fun () -> decideFlightRepairClaim sidStr bloggerSid identity)

        member this.TryGetRepairEpisode(identity: BloggerRepairEpisodeIdentity) : BloggerRepairRendezvous option =
            let bloggerSid = identity.BloggerSessionId

            lock episodeGate (fun () ->
                match episodes.TryGetValue bloggerSid with
                | true, existing when existing.Identity = identity -> Some existing
                | _ -> None)

        member this.CancelRepairEpisode(identity: BloggerRepairEpisodeIdentity) : unit =
            repairEpisodeForSession identity.BloggerSessionId
            |> Option.filter (fun existing -> existing.Identity = identity)
            |> Option.iter _.Cancel()

        member this.DrainRepairEpisodes() : Task =
            let tasksToAwait = snapshotRepairCompletions ()

            task {
                let! firstFault = drainAllRepair (tasksToAwait |> Array.toList) None
                forgetRepairCompletions tasksToAwait
                return raiseDrainFault firstFault
            }
            :> Task

    member _.BeginShutdown() =
        let activeEpisodes =
            lock episodeGate (fun () ->
                if not shutdown.IsCancellationRequested then
                    shutdown.Cancel()

                episodes.Values |> Seq.toList)

        lock parkedGate (fun () ->
            for entry in parked.Values |> Seq.toList do
                entry.TryCancel()

            parked.Clear()
            pendingOffer.Clear())

        for rendezvous in activeEpisodes do
            rendezvous.Cancel()

    member this.CancelEpisodesForSession(sessionId: string) : unit =
        repairEpisodeForSession (SessionId.create sessionId) |> Option.iter _.Cancel()

    member this.DrainRepairEpisodes() : Task =
        (this :> IBloggerRuntimeHost).DrainRepairEpisodes()

    /// ENFORCER-162: plugin dispose emits Cancelled to every material waiter.
    member this.Dispose() =
        this.BeginShutdown()
        shutdown.Dispose()
