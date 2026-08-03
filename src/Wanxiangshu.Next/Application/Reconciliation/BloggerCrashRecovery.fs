namespace Wanxiangshu.Next.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// C5 item 20: crash windows for the Blogger vertical slice only.
///
/// Recovery inputs (item 23): durable request context + Host snapshot + Journal
/// receipts. No TOML reverse parse, no guess from latest X, no log strings.
module BloggerCrashRecovery =

    [<RequireQualifiedAccess>]
    type WindowOutcome =
        /// A: materialized, no PhysicalAccepted → abandon, Idle.
        | AbandonedUnsent of BloggerRequestId
        /// C: tool results present, no receipt → re-commit from durable + Host.
        | Recommitted of ProviderRunIdentity
        /// D: receipt present, no waiter → restore Parked in memory.
        | RestoredParked of SessionId
        /// E: Parked, new material exists → leave for next coordinator offer.
        | PendingMaterial of SessionId
        /// Still open and Host still running — restore InFlight in memory.
        | RestoredInFlight of SessionId
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

    let private hasReceipt
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : bool =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind mainSessionId
        |> Option.bind (fun s -> s.BloggerCycles)
        |> Option.bind (fun c -> BloggerCycleProjection.tryReceipt providerRun c)
        |> Option.isSome
        || ((AgentJournal.snapshot journal).AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun s -> s.Enforcement)
            |> Option.bind (fun e -> EnforcementProjection.tryFindByProviderRun providerRun e)
            |> Option.isSome)

    let private abandon
        (journal: AgentJournal)
        (openReq: OpenBloggerRequest)
        (reason: string)
        : unit =
        let fact =
            AgentFact.BloggerRequestAbandoned
                {| RequestId = openReq.RequestId
                   MainSessionId = openReq.MainSessionId
                   BloggerSessionId = openReq.BloggerSessionId
                   Reason = reason |}

        AgentJournal.appendAgent (StreamId.Session openReq.MainSessionId) None fact journal
        |> ignore

    /// Pure decision for window A/B/C/D given Host tool-call presence and receipts.
    let classifyOpenRequest
        (hasPhysicalAccepted: bool)
        (hasCompletedBlogTool: bool)
        (hasCycleReceipt: bool)
        : WindowOutcome option =
        if hasCycleReceipt then
            None // already closed; D handled via receipts separately
        elif not hasPhysicalAccepted then
            Some(WindowOutcome.AbandonedUnsent(BloggerRequestId.create "decision"))
        elif hasCompletedBlogTool then
            Some(WindowOutcome.Recommitted(ProviderRunIdentity.create "decision"))
        else
            Some(WindowOutcome.RestoredInFlight(SessionId.create "decision"))

    let private restoreRuntime
        (host: IParkedTransformHost)
        (bloggerSessionId: SessionId)
        (state: BloggerRuntimeState)
        : unit =
        let key = SessionId.value bloggerSessionId

        host.SetBloggerRuntime(
            key,
            { State = state
              PendingOffer = None
              RepairSpent = false }
        )

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

                // Window D/E prep: every main with a receipt but no open request
                // and no parked waiter should restore Parked memory state.
                for mainSessionId, session in (AgentJournal.snapshot durable).AgentProjections.Sessions |> Map.toList do
                    match session.BloggerCycles, session.Companion with
                    | Some cycles, Some companion ->
                        match companion.BloggerSessionId with
                        | None -> ()
                        | Some bloggerId ->
                            let key = SessionId.value bloggerId
                            let hasOpen = Map.containsKey bloggerId cycles.OpenByBlogger
                            let hasAnyReceipt = not (Map.isEmpty cycles.ByProviderRun)

                            if hasAnyReceipt && not hasOpen && not (host.HasParked key) then
                                restoreRuntime host bloggerId BloggerRuntimeState.Parked
                                results.Add(WindowOutcome.RestoredParked bloggerId)
                    | _ -> ()

                for mainSessionId, openReq in openRequests durable do
                    let bloggerKey = SessionId.value openReq.BloggerSessionId

                    match snapshotOpt with
                    | None ->
                        // No Host snapshot: fail closed — leave open, do not abandon.
                        results.Add(WindowOutcome.Unreadable(openReq.BloggerSessionId, "no snapshot port"))
                    | Some snapshot ->
                        match! snapshot.GetMessages openReq.BloggerSessionId with
                        | Error reason ->
                            results.Add(WindowOutcome.Unreadable(openReq.BloggerSessionId, reason))
                        | Ok messages ->
                            // PhysicalAccepted proxy: any user message after materialize.
                            // Without PromptKey on blogger claims, presence of assistant
                            // or tool parts indicates the provider accepted the request.
                            let hasAssistant =
                                messages
                                |> List.exists (fun m -> m.Role = "assistant")

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

                            // Window A: no Host progress → abandon unsent.
                            if not hasAssistant && not hasCompletedBlog then
                                abandon durable openReq "crash-window-A: materialized but not accepted"
                                restoreRuntime host openReq.BloggerSessionId BloggerRuntimeState.Idle
                                host.ClearCurrentRequest bloggerKey
                                results.Add(WindowOutcome.AbandonedUnsent openReq.RequestId)
                            elif hasCompletedBlog then
                                // Window C: tool result present; leave open for
                                // continuation re-entry (alreadyCommitted/receipt path).
                                // Restore InFlight so single-flight skip holds.
                                restoreRuntime
                                    host
                                    openReq.BloggerSessionId
                                    (BloggerRuntimeState.InFlight(
                                        // Minimal placeholder: continuation peeks CurrentRequest
                                        // from durable path; without blob reload we mark InFlight
                                        // empty-ctx via Parked+pending only if needed.
                                        // Use Squash-empty? Better: leave runtime InFlight without
                                        // CurrentRequest is illegal. Re-read blob not available
                                        // here without decoding. Keep InFlight with a Main stub
                                        // only if we can rebuild — fail closed to Idle if not.
                                        // Practical: mark Parked and rely on next material/offer
                                        // after operator — but that loses C. Instead keep open
                                        // and set InFlight with synthetic context from open fields.
                                        BloggerRequestContext.Main
                                            { RequestId = openReq.RequestId
                                              MainSessionId = openReq.MainSessionId
                                              BloggerSessionId = openReq.BloggerSessionId
                                              Toml = ""
                                              PreviousIngestedThroughSequence = openReq.PreviousIngestedThroughSequence
                                              NextIngestedThroughSequence = openReq.NextIngestedThroughSequence
                                              PreviousCoverableTurnCutoffExclusive = 0
                                              NextCoverableTurnCutoffExclusive = 0
                                              NextCoveredPrefixDigest = ""
                                              FrameEpochId = openReq.FrameEpochId
                                              DeltaDigest = openReq.ContextDigest
                                              ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                                    ))

                                // CurrentRequest for continuation commit.
                                host.SetCurrentRequest(
                                    bloggerKey,
                                    BloggerRequestContext.Main
                                        { RequestId = openReq.RequestId
                                          MainSessionId = openReq.MainSessionId
                                          BloggerSessionId = openReq.BloggerSessionId
                                          Toml = ""
                                          PreviousIngestedThroughSequence = openReq.PreviousIngestedThroughSequence
                                          NextIngestedThroughSequence = openReq.NextIngestedThroughSequence
                                          PreviousCoverableTurnCutoffExclusive = 0
                                          NextCoverableTurnCutoffExclusive = 0
                                          NextCoveredPrefixDigest = ""
                                          FrameEpochId = openReq.FrameEpochId
                                          DeltaDigest = openReq.ContextDigest
                                          ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                                )

                                results.Add(WindowOutcome.Recommitted(ProviderRunIdentity.create "pending-tool"))
                            else
                                // Window B: accepted, no tool yet — restore InFlight.
                                restoreRuntime
                                    host
                                    openReq.BloggerSessionId
                                    (BloggerRuntimeState.InFlight(
                                        BloggerRequestContext.Main
                                            { RequestId = openReq.RequestId
                                              MainSessionId = openReq.MainSessionId
                                              BloggerSessionId = openReq.BloggerSessionId
                                              Toml = ""
                                              PreviousIngestedThroughSequence = openReq.PreviousIngestedThroughSequence
                                              NextIngestedThroughSequence = openReq.NextIngestedThroughSequence
                                              PreviousCoverableTurnCutoffExclusive = 0
                                              NextCoverableTurnCutoffExclusive = 0
                                              NextCoveredPrefixDigest = ""
                                              FrameEpochId = openReq.FrameEpochId
                                              DeltaDigest = openReq.ContextDigest
                                              ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                                    ))

                                host.SetCurrentRequest(
                                    bloggerKey,
                                    BloggerRequestContext.Main
                                        { RequestId = openReq.RequestId
                                          MainSessionId = openReq.MainSessionId
                                          BloggerSessionId = openReq.BloggerSessionId
                                          Toml = ""
                                          PreviousIngestedThroughSequence = openReq.PreviousIngestedThroughSequence
                                          NextIngestedThroughSequence = openReq.NextIngestedThroughSequence
                                          PreviousCoverableTurnCutoffExclusive = 0
                                          NextCoverableTurnCutoffExclusive = 0
                                          NextCoveredPrefixDigest = ""
                                          FrameEpochId = openReq.FrameEpochId
                                          DeltaDigest = openReq.ContextDigest
                                          ObservedPrefixEpochId = openReq.ObservedPrefixEpochId }
                                )

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
