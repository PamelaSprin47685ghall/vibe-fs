namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

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
                let results = ResizeArray<WindowOutcome>()

                // Window D: receipt present, no open request, no parked waiter.
                // Busy authority = HasFlight, not cell.State.
                for mainSessionId, session in (AgentJournal.snapshot durable).AgentProjections.Sessions |> Map.toList do
                    match session.BloggerCycles, session.Companion with
                    | Some cycles, Some companion ->
                        match companion.BloggerSessionId with
                        | None -> ()
                        | Some bloggerId ->
                            let key = SessionId.value bloggerId
                            let hasOpen = Map.containsKey bloggerId cycles.OpenByBlogger
                            let hasAnyReceipt = not (Map.isEmpty cycles.ByProviderRun)

                            if host.HasFlight key then
                                ()
                            elif hasAnyReceipt && not hasOpen && not (host.HasParked key) then
                                // Cycle already receipted → NoRecovery (ENFORCER-063 success path).
                                //
                                // Nothing to restore: forcing `Parked` here would stage the next
                                // material as a PendingOffer with no ParkedTransform to resume it
                                // (arming is NotArmed after restart, so no squash path starts
                                // either) — the session would stall on its next material. Leaving
                                // flight clear lets the material flow through startFrozen, and
                                // the drain path after its commit re-checks receipts via
                                // tryRefreshMainContextFromJournal.
                                results.Add(WindowOutcome.ReceiptedIdle bloggerId)
                    | _ -> ()

                for mainSessionId, openReq in openRequests durable do
                    let bloggerKey = SessionId.value openReq.BloggerSessionId

                    // Live process already owns this request: do not stomp.
                    // Physical flight registry is the authority (not cell.State).
                    if host.HasFlight bloggerKey then
                        results.Add(WindowOutcome.AlreadyLive openReq.BloggerSessionId)
                    else
                        match snapshotOpt with
                        | None -> results.Add(WindowOutcome.Unreadable(openReq.BloggerSessionId, "no snapshot port"))
                        | Some snapshot ->
                            match! snapshot.GetMessages openReq.BloggerSessionId with
                            | Error reason ->
                                // Cold crash: Host session unreadable → abandon window A.
                                do! abandon durable openReq (sprintf "crash-window-A: host snapshot error: %s" reason)

                                restoreRuntime host openReq.BloggerSessionId None
                                results.Add(WindowOutcome.AbandonedUnsent openReq.RequestId)
                            | Ok messages ->
                                let hasCompletedBlog =
                                    messages
                                    |> List.exists (fun m ->
                                        m.Role = "assistant"
                                        && m.Parts
                                           |> Array.exists (fun p ->
                                               match p with
                                               | MessagePart.ToolCall(_, name, _) when name = "blog" -> true
                                               | MessagePart.ToolResult(_, _) -> true
                                               | _ -> false))

                                match! tryReloadMainContext durable openReq with
                                | None ->
                                    results.Add(
                                        WindowOutcome.Unreadable(openReq.BloggerSessionId, "context blob unreadable")
                                    )
                                | Some ctx ->
                                    restoreRuntime host openReq.BloggerSessionId (Some ctx)

                                    if hasCompletedBlog then
                                        results.Add(
                                            WindowOutcome.Recommitted(ProviderRunIdentity.create "pending-tool")
                                        )
                                    else
                                        results.Add(WindowOutcome.RestoredInFlight openReq.BloggerSessionId)

                return results |> Seq.toList
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
