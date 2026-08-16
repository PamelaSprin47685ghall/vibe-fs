namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Composition.Durable

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
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

/// C5 item 20: crash windows for the Blogger vertical slice only.
///
/// Recovery inputs (item 23): durable request context + Host snapshot + Journal
/// receipts. No TOML reverse parse, no guess from latest X, no log strings.
///
/// Live in-process: materialize → EnsureRecoveryDone may run before/during the
/// first provider step. Never abandon or stomp CurrentRequest when the host
/// already holds physical flight ownership (HasFlight).
module BloggerCrashRecovery =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    let BloggerMissingToolRepairKind = "blogger-missing-tool"

    [<RequireQualifiedAccess>]
    type WindowOutcome =
        /// A: Host session gone after materialize → abandon, clear flight.
        | AbandonedUnsent of BloggerRequestId
        /// C: tool results present, no receipt → restore physical flight for re-entry.
        | Recommitted of ProviderRunIdentity
        /// D: receipt present, no waiter → nothing to restore; next material
        /// flows through startFrozen and the drain re-checks receipts.
        | ReceiptedIdle of SessionId
        /// E: Parked, new material exists → leave for next coordinator offer.
        | PendingMaterial of SessionId
        /// Still open and Host still running — restore physical flight in memory.
        | RestoredInFlight of SessionId
        /// Live process already owns the request; recovery is a no-op.
        | AlreadyLive of SessionId
        | Unreadable of SessionId * reason: string

    let private openRequests (journal: AgentJournal) : (SessionId * OpenBloggerRequest) list =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.toList
        |> List.collect (fun (mainSessionId, session) ->
            match session.BloggerCycles with
            | None -> []
            | Some cycles ->
                cycles.OpenByRequestId
                |> Map.toList
                |> List.map (fun (_, openReq) -> mainSessionId, openReq))

    let private abandon (journal: AgentJournal) (openReq: OpenBloggerRequest) (reason: string) : Task =
        BloggerAbandon.byRequestId journal openReq.RequestId openReq.MainSessionId openReq.BloggerSessionId reason

    /// <summary>Pure decision for window A/B/C given Host tool-call presence and
    /// receipts. Exported so tests can exercise the cold-window table directly
    /// without driving a full crash.</summary>
    let classifyOpenRequest
        (hasPhysicalAccepted: bool)
        (hasCompletedBlogTool: bool)
        (hasCycleReceipt: bool)
        : WindowOutcome option =
        if hasCycleReceipt then
            None
        elif not hasPhysicalAccepted then
            Some(WindowOutcome.AbandonedUnsent(BloggerRequestId.create "decision"))
        elif hasCompletedBlogTool then
            Some(WindowOutcome.Recommitted(ProviderRunIdentity.create "decision"))
        else
            Some(WindowOutcome.RestoredInFlight(SessionId.create "decision"))

    /// Rebuild or clear physical flight ownership. Drain stays Closed.
    /// Does not match cell.State: SetCurrentRequest / ClearCurrentRequest are
    /// the flight authority; SetDrainWindow keeps the physical drain slot closed
    /// without re-authoring any shadow state.
    let private restoreRuntime
        (host: IParkedTransformHost)
        (bloggerSessionId: SessionId)
        (flight: BloggerRequestContext option)
        : unit =
        let key = SessionId.value bloggerSessionId

        match flight with
        | Some ctx ->
            host.SetCurrentRequest(key, ctx)
            // Keep Drain Closed without re-authoring ownership via State match.
            host.SetDrainWindow(key, DrainWindow.Closed)
        | None ->
            host.ClearCurrentRequest key
            host.SetDrainWindow(key, DrainWindow.Closed)

    /// C5: one reload path — EnforcerFrameRecovery.tryReloadRequestContext (full cutoff/digest).
    let private tryReloadMainContext
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        : Task<BloggerRequestContext option> =
        EnforcerFrameRecovery.tryReloadRequestContext journal openReq

    /// Window D: receipt present, no open request, no parked waiter.
    /// Busy authority = HasFlight, not cell.State.
    /// Cycle already receipted → NoRecovery (ENFORCER-063 success path).
    /// Forcing Parked would stage PendingOffer with no ParkedTransform (arming is
    /// NotArmed after restart) — leave flight clear so material flows through
    /// startFrozen; drain re-checks receipts via tryRefreshMainContextFromJournal.
    let private receiptedIdleDecision
        (host: IParkedTransformHost)
        (cycles: BloggerCycleProjectionState)
        (bloggerId: SessionId)
        : WindowOutcome option =
        let key = SessionId.value bloggerId
        let hasOpen = Map.containsKey bloggerId cycles.OpenByBlogger
        let hasAnyReceipt = not (Map.isEmpty cycles.ByProviderRun)

        if host.HasFlight key then
            None
        elif hasAnyReceipt && not hasOpen && not (host.HasParked key) then
            Some(WindowOutcome.ReceiptedIdle bloggerId)
        else
            None

    let private companionBloggerId (session: SessionAgentProjection) =
        match session.Companion with
        | Some companion -> companion.BloggerSessionId
        | None -> None

    let private tryReceiptedIdle (host: IParkedTransformHost) (session: SessionAgentProjection) =
        match session.BloggerCycles, companionBloggerId session with
        | Some cycles, Some bloggerId -> receiptedIdleDecision host cycles bloggerId
        | _ -> None

    let private collectReceiptedIdle (host: IParkedTransformHost) (durable: AgentJournal) =
        (AgentJournal.snapshot durable).AgentProjections.Sessions
        |> Map.toList
        |> List.choose (fun (_, session) -> tryReceiptedIdle host session)

    let private partSignalsCompletedChronicle (part: SessionToolPart) =
        part.ToolName = "chronicle"
        && match part.State with
           | SnapshotToolPartState.Completed _ -> true
           | _ -> false

    let private messagesHaveCompletedBlog (messages: SessionMessage list) =
        messages
        |> List.rev
        |> List.tryFind (fun message -> message.Role = "assistant")
        |> Option.exists (fun message ->
            match message.ToolParts |> Array.filter (fun part -> part.ToolName = "chronicle") with
            | [| part |] -> partSignalsCompletedChronicle part
            | _ -> false)

    let private outcomeAfterRestore (hasCompletedBlog: bool) (bloggerSessionId: SessionId) =
        if hasCompletedBlog then
            WindowOutcome.Recommitted(ProviderRunIdentity.create "pending-tool")
        else
            WindowOutcome.RestoredInFlight bloggerSessionId

    let private restoreFromMessages
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (openReq: OpenBloggerRequest)
        (messages: SessionMessage list)
        : Task<WindowOutcome> =
        task {
            match! tryReloadMainContext journal openReq with
            | None -> return WindowOutcome.Unreadable(openReq.BloggerSessionId, "context blob unreadable")
            | Some ctx ->
                restoreRuntime host openReq.BloggerSessionId (Some ctx)
                return outcomeAfterRestore (messagesHaveCompletedBlog messages) openReq.BloggerSessionId
        }

    let private abandonUnreadableSnapshot
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (openReq: OpenBloggerRequest)
        (reason: string)
        : Task<WindowOutcome> =
        task {
            // Cold crash: Host session unreadable → abandon window A.
            do! abandon journal openReq (sprintf "crash-window-A: host snapshot error: %s" reason)
            restoreRuntime host openReq.BloggerSessionId None
            return WindowOutcome.AbandonedUnsent openReq.RequestId
        }

    let private reconcileOpenWithSnapshot
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (snapshot: ISessionSnapshotPort)
        (openReq: OpenBloggerRequest)
        : Task<WindowOutcome> =
        task {
            match! snapshot.GetMessages openReq.BloggerSessionId with
            | Error reason -> return! abandonUnreadableSnapshot journal host openReq reason
            | Ok messages -> return! restoreFromMessages journal host openReq messages
        }

    let private reconcileOpenWhenIdle
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (snapshotOpt: ISessionSnapshotPort option)
        (openReq: OpenBloggerRequest)
        : Task<WindowOutcome> =
        match snapshotOpt with
        | None -> Task.FromResult(WindowOutcome.Unreadable(openReq.BloggerSessionId, "no snapshot port"))
        | Some snapshot -> reconcileOpenWithSnapshot journal host snapshot openReq

    let private reconcileOpenRequest
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (snapshotOpt: ISessionSnapshotPort option)
        (openReq: OpenBloggerRequest)
        : Task<WindowOutcome> =
        task {
            let bloggerKey = SessionId.value openReq.BloggerSessionId

            // Live process already owns this request: do not stomp.
            // Physical flight registry is the authority (not cell.State).
            if host.HasFlight bloggerKey then
                return WindowOutcome.AlreadyLive openReq.BloggerSessionId
            else
                return! reconcileOpenWhenIdle journal host snapshotOpt openReq
        }

    let private reconcileAllOpen
        (journal: AgentJournal)
        (host: IParkedTransformHost)
        (snapshotOpt: ISessionSnapshotPort option)
        : Task<WindowOutcome list> =
        task {
            let results = ResizeArray<WindowOutcome>()

            for _, openReq in openRequests journal do
                let! outcome = reconcileOpenRequest journal host snapshotOpt openReq
                results.Add outcome

            return results |> Seq.toList
        }

    /// Startup pass: walk open materializations + receipts.
    let reconcile
        (journal: AgentJournal option)
        (host: IParkedTransformHost)
        (snapshotOpt: ISessionSnapshotPort option)
        : Task<WindowOutcome list> =
        task {
            match journal with
            | None -> return []
            | Some durable ->
                let receipted = collectReceiptedIdle host durable
                let! opened = reconcileAllOpen durable host snapshotOpt
                return receipted @ opened
        }

    /// Single-flight gate, same lifecycle as PromptRecovery (not in constructor).
    type RecoveryGate
        (journal: AgentJournal option, host: IParkedTransformHost, snapshotOpt: ISessionSnapshotPort option) =

        let gate = obj ()
        // DSL-MUTABLE: single-flight — memoized reconcile task (latch, not a stage)
        let mutable pass: Task<WindowOutcome list> option = None

        member _.EnsureDone() : Task =
            let active =
                lock gate (fun () ->
                    match pass with
                    | Some t -> t
                    | None ->
                        let t = reconcile journal host snapshotOpt
                        pass <- Some t
                        t)

            task {
                let! _ = active
                ()
            }
            :> Task
