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
/// first provider step. Never abandon or stomp CurrentRequest when the runtime
/// cell is already InFlight with a real context.
module BloggerCrashRecovery =

    /// Must match EnforcerHost interactionNudge repairKind (ENFORCER-066 claim scope).
    [<Literal>]
    let BloggerMissingToolRepairKind = "blogger-missing-tool"

    [<RequireQualifiedAccess>]
    type WindowOutcome =
        /// A: Host session gone after materialize → abandon, Idle.
        | AbandonedUnsent of BloggerRequestId
        /// C: tool results present, no receipt → restore InFlight for re-entry.
        | Recommitted of ProviderRunIdentity
        /// D: receipt present, no waiter → restore Parked in memory.
        | RestoredParked of SessionId
        /// E: Parked, new material exists → leave for next coordinator offer.
        | PendingMaterial of SessionId
        /// Still open and Host still running — restore InFlight in memory.
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

    let private abandon (journal: AgentJournal) (openReq: OpenBloggerRequest) (reason: string) : unit =
        BloggerAbandon.byRequestId journal openReq.RequestId openReq.MainSessionId openReq.BloggerSessionId reason

    /// Pure decision for window A/B/C given Host tool-call presence and receipts.
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

    /// ENFORCER-153 pure rejudge from already-resolved evidence.
    ///
    /// `claimedTerminalRun`: durable InteractionRepair claim for blogger-missing-tool
    /// (payload digests terminal run). `completedAssistants`: chronological completed
    /// assistant terminals as (runId, hasBlogToolCall).
    ///
    /// Conservative (no AABB re-spend without second pure-prose evidence; no second
    /// nudge when claim exists):
    /// - no claim → NoRecovery
    /// - claim + no blog after claim (any number of pure-prose terminals) →
    ///   InteractionNudgeIssued claimed
    /// - claim + valid blog after claim → NoRecovery (cycle completed / success)
    ///
    /// AabbRepairConsumed is never derived on cold rejudge: AABB is memory-only
    /// (markAabbRepairConsumed + transform injection, no journal fact). A second
    /// pure-prose terminal is the trigger for aabbRepair (ENFORCER-067), not its
    /// receipt — deriving consumed here would let the hot path fatalEnd without
    /// ever injecting the AABB repair (budget stolen across a crash).
    let rejudgeFromEvidence
        (claimedTerminalRun: string option)
        (completedAssistants: (string * bool) list)
        : BloggerToolRecovery =
        match claimedTerminalRun with
        | None -> BloggerToolRecovery.NoRecovery
        | Some claimed ->
            let afterClaimed =
                completedAssistants
                |> List.skipWhile (fun (id, _) -> id <> claimed)
                |> function
                    | _ :: rest -> rest
                    | [] ->
                        // Claimed run absent from transcript: keep nudge stage, never invent AABB.
                        []

            let hasBlogAfter = afterClaimed |> List.exists (fun (_, hasBlog) -> hasBlog)

            if hasBlogAfter then
                BloggerToolRecovery.NoRecovery
            else
                // No durable AABB evidence exists (AABB = memory mark + transform
                // injection only): never invent AabbRepairConsumed. Restore as
                // InteractionNudgeIssued claimed; the hot path re-runs aabbRepair
                // on the next *new* pure-prose terminal (issuedRun <> terminalRun).
                BloggerToolRecovery.InteractionNudgeIssued(ProviderRunIdentity.create claimed)

    let private hasBlogToolCall (parts: MessagePart array) : bool =
        parts
        |> Array.exists (function
            | MessagePart.ToolCall(_, name, _) when name = "blog" -> true
            | _ -> false)

    /// Completed assistant terminals: (message id = ProviderRunIdentity, has blog tool call).
    let private completedAssistantEvidence (messages: SessionMessage list) : (string * bool) list =
        messages
        |> List.choose (fun m ->
            if
                m.Role = "assistant"
                && m.Completed
                && not (System.String.IsNullOrWhiteSpace m.Id)
            then
                Some(m.Id, hasBlogToolCall m.Parts)
            else
                None)

    /// Durable claim for repairKind against a terminal run (ClaimSequences read).
    let private repairClaimedFor
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (terminalRun: ProviderRunIdentity)
        : bool =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        match
            PromptAuthorityLedger.activeProfile bloggerSessionId projections,
            PromptAuthorityLedger.projectionFor bloggerSessionId projections
        with
        | Some profile, Some authProj ->
            PromptAuthority.repairAlreadyClaimed
                profile.SessionId
                profile.LogicalRunId
                terminalRun
                BloggerMissingToolRepairKind
                authProj
        | _ -> false

    /// When the claimed terminal is absent from the Host snapshot, recover its run id
    /// from ClaimSequences scopes (session \u001f run \u001f InteractionRepair \u001f run \u001f kind).
    let private claimedRunFromSequences (journal: AgentJournal) (bloggerSessionId: SessionId) : string option =
        let projections = (AgentJournal.snapshot journal).AgentProjections

        match PromptAuthorityLedger.projectionFor bloggerSessionId projections with
        | None -> None
        | Some authProj ->
            let suffix = "\u001f" + BloggerMissingToolRepairKind

            authProj.ClaimSequences
            |> Map.toList
            |> List.tryPick (fun (scope, seq) ->
                if seq < 1 then
                    None
                elif not (scope.EndsWith(suffix, System.StringComparison.Ordinal)) then
                    None
                else
                    let withoutKind = scope.Substring(0, scope.Length - suffix.Length)
                    let sep = withoutKind.LastIndexOf('\u001f')

                    if sep < 0 then
                        None
                    else
                        let runId = withoutKind.Substring(sep + 1)

                        if System.String.IsNullOrWhiteSpace runId then
                            None
                        else
                            Some runId)

    /// ENFORCER-153: rejudge BloggerToolRecovery from claim + Host transcript.
    let rejudgeToolRecovery
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (messages: SessionMessage list)
        : BloggerToolRecovery =
        let terminals = completedAssistantEvidence messages

        let claimedFromTerminals =
            terminals
            |> List.tryPick (fun (id, _) ->
                let run = ProviderRunIdentity.create id

                if repairClaimedFor journal bloggerSessionId run then
                    Some id
                else
                    None)

        let claimedTerminalRun =
            match claimedFromTerminals with
            | Some _ as hit -> hit
            | None -> claimedRunFromSequences journal bloggerSessionId

        rejudgeFromEvidence claimedTerminalRun terminals

    /// True when a raw Host message is an AABB repair instruction we injected for `requestKey`.
    let private aabbRepairInjected (requestKey: string) (rawMessages: obj list) : bool =
        rawMessages
        |> List.choose (fun m ->
            if isNull m then
                None
            else
                let info = if isNull m?info then m else m?info

                if
                    not (isNull info)
                    && not (isNull info?source)
                    && unbox<string> info?source = "interaction-repair"
                    && not (isNull info?synthetic)
                    && unbox<bool> info?synthetic
                    && not (isNull info?requestKey)
                    && unbox<string> info?requestKey = requestKey
                then
                    Some()
                else
                    None)
        |> List.isEmpty
        |> not

    /// ENFORCER-153 hot path: derive recovery state from durable claim + visible
    /// transcript. No mutable runtime field is consulted.
    let repairState
        (journal: AgentJournal)
        (bloggerSessionId: SessionId)
        (requestKey: string)
        (terminalRun: ProviderRunIdentity)
        (rawMessages: obj list)
        : BloggerToolRecovery =
        if aabbRepairInjected requestKey rawMessages then
            BloggerToolRecovery.AabbRepairConsumed
        elif repairClaimedFor journal bloggerSessionId terminalRun then
            BloggerToolRecovery.InteractionNudgeIssued terminalRun
        else
            BloggerToolRecovery.NoRecovery

    let private restoreRuntime
        (host: IParkedTransformHost)
        (bloggerSessionId: SessionId)
        (state: BloggerRuntimeState)
        (recovery: BloggerToolRecovery)
        : unit =
        let key = SessionId.value bloggerSessionId

        host.SetBloggerRuntime(
            key,
            { State = state
              PendingOffer = None
              Recovery = recovery
              Drain = DrainWindow.Closed }
        )

    /// C5: one reload path — EnforcerHost.tryReloadRequestContext (full cutoff/digest).
    let private tryReloadMainContext
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        : BloggerRequestContext option =
        EnforcerHost.tryReloadRequestContext journal openReq

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

                // Window D: receipt present, no open request, no parked waiter → Parked.
                for mainSessionId, session in (AgentJournal.snapshot durable).AgentProjections.Sessions |> Map.toList do
                    match session.BloggerCycles, session.Companion with
                    | Some cycles, Some companion ->
                        match companion.BloggerSessionId with
                        | None -> ()
                        | Some bloggerId ->
                            let key = SessionId.value bloggerId
                            let hasOpen = Map.containsKey bloggerId cycles.OpenByBlogger
                            let hasAnyReceipt = not (Map.isEmpty cycles.ByProviderRun)
                            let live = host.GetBloggerRuntime key

                            match live.State with
                            | BloggerRuntimeState.InFlight _
                            | BloggerRuntimeState.Parked
                            | BloggerRuntimeState.Sealed
                            | BloggerRuntimeState.Disposed -> ()
                            | BloggerRuntimeState.Idle when hasAnyReceipt && not hasOpen && not (host.HasParked key) ->
                                // Cycle already receipted → NoRecovery (ENFORCER-063 success path).
                                restoreRuntime host bloggerId BloggerRuntimeState.Parked BloggerToolRecovery.NoRecovery
                                results.Add(WindowOutcome.RestoredParked bloggerId)
                            | BloggerRuntimeState.Idle -> ()
                    | _ -> ()

                for mainSessionId, openReq in openRequests durable do
                    let bloggerKey = SessionId.value openReq.BloggerSessionId
                    let live = host.GetBloggerRuntime bloggerKey
                    let liveCurrent = host.TryPeekCurrentRequest bloggerKey

                    // Live process already owns this request: do not stomp.
                    match live.State, liveCurrent with
                    | BloggerRuntimeState.InFlight _, Some _ ->
                        results.Add(WindowOutcome.AlreadyLive openReq.BloggerSessionId)
                    | BloggerRuntimeState.Disposed, _ ->
                        results.Add(WindowOutcome.Unreadable(openReq.BloggerSessionId, "disposed"))
                    | _ ->
                        match snapshotOpt with
                        | None -> results.Add(WindowOutcome.Unreadable(openReq.BloggerSessionId, "no snapshot port"))
                        | Some snapshot ->
                            match! snapshot.GetMessages openReq.BloggerSessionId with
                            | Error reason ->
                                // Cold crash: Host session unreadable → abandon window A.
                                abandon durable openReq (sprintf "crash-window-A: host snapshot error: %s" reason)

                                restoreRuntime
                                    host
                                    openReq.BloggerSessionId
                                    BloggerRuntimeState.Idle
                                    BloggerToolRecovery.NoRecovery

                                host.ClearCurrentRequest bloggerKey
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

                                match tryReloadMainContext durable openReq with
                                | None ->
                                    results.Add(
                                        WindowOutcome.Unreadable(openReq.BloggerSessionId, "context blob unreadable")
                                    )
                                | Some ctx ->
                                    // ENFORCER-153: rejudge from claim + transcript, not memory bool.
                                    let recovery = rejudgeToolRecovery durable openReq.BloggerSessionId messages

                                    restoreRuntime
                                        host
                                        openReq.BloggerSessionId
                                        (BloggerRuntimeState.InFlight ctx)
                                        recovery

                                    if liveCurrent.IsNone then
                                        host.SetCurrentRequest(bloggerKey, ctx)

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
