namespace Wanxiangshu.Session

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// ENFORCER-047/050: the ONE main-session decision entry for Blogger material.
module BloggerCoordinator =

    type DecisionEffect =
        | Started
        | StartedSquash
        | SkippedInFlight
        | OfferedParked of resumed: bool
        | NoMaterial
        | Sealed
        | StartFailed of string
        | MaterializeFailed of string

    let private loadProjections
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (host: CompanionHost)
        : BlogProjectionState * XTraceProjectionState * PrefixEpochId =
        match journal with
        | Some j ->
            let projections = (AgentJournal.snapshot j).AgentProjections
            let session = projections.Sessions |> Map.tryFind mainSessionId

            let blog =
                session
                |> Option.bind (fun s -> s.Blog)
                |> Option.defaultValue BlogProjection.empty

            let xTrace =
                session
                |> Option.bind (fun s -> s.XTrace)
                |> Option.defaultValue XTraceProjection.empty

            let epoch =
                session
                |> Option.bind (fun s -> s.PrefixEpoch)
                |> Option.map (fun e -> e.EpochId)
                |> Option.defaultValue PrefixEpochId.initial

            blog, xTrace, epoch
        | None -> host.Memory.Blog, host.Memory.XTrace, PrefixEpochId.initial

    let private nextMainContext
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (blog: BlogProjectionState)
        (xTrace: XTraceProjectionState)
        (floorSequence: int64 option)
        (projection: ProviderSemanticProjection)
        : BloggerRequestContext option =
        // GLORY-023: the Manager Life's protected prefix never enters Y. The
        // effective ingest start is max(blog coverage, life floor); a chunk that
        // would span the floor is cut at it by the cursor itself (GLORY-024).
        let effectiveIngested =
            floorSequence
            |> Option.map (fun floor -> max blog.Coverage.IngestedThroughSequence floor)
            |> Option.defaultValue blog.Coverage.IngestedThroughSequence

        let ingestCursor = XTraceProjection.semanticCursorFor effectiveIngested xTrace

        match
            BloggerDelta.nextChunk
                BloggerDelta.DeltaLimitBytes
                ingestCursor
                blog.Coverage.CoverableTurnCutoffExclusive
                projection.Messages
        with
        | None -> None
        | Some chunk ->
            // Birth gate: mapping failure / Next≤Prev → None (no Start, no fatal).
            // Commit-path fatal remains only for contexts that somehow escaped this gate.
            Wanxiangshu.OpenCode.EnforcerHost.mainContextFromChunk
                mainSessionId
                bloggerSessionId
                observedEpoch
                blog
                xTrace
                projection
                chunk

    /// C5: durable materialization. Context blob is the irrecomputable semantic
    /// input. Pre-send PromptKey=None; after physical send, re-append with the
    /// same ContextDigest + Some PromptKey so commit can prove ownership.
    let private materializeRequest
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (promptKey: PromptKey option)
        : Result<unit, string> =
        let requestId = BloggerRequestContext.requestId ctx
        let mainSessionId = BloggerRequestContext.mainSessionId ctx
        let bloggerSessionId = BloggerRequestContext.bloggerSessionId ctx
        let epoch = BloggerRequestContext.observedPrefixEpoch ctx
        let frameEpoch = BloggerRequestContext.frameEpochId ctx

        let kind, prevSeq, nextSeq, selectedDigests, contextPayload =
            match ctx with
            | BloggerRequestContext.Main main ->
                "main",
                main.PreviousIngestedThroughSequence,
                main.NextIngestedThroughSequence,
                [],
                createObj
                    [ "kind", box "main"
                      "toml", box main.Toml
                      "delta_digest", box (BlobDigest.value main.DeltaDigest)
                      "prev_ingest", box main.PreviousIngestedThroughSequence
                      "next_ingest", box main.NextIngestedThroughSequence
                      "prev_cutoff", box main.PreviousCoverableTurnCutoffExclusive
                      "next_cutoff", box main.NextCoverableTurnCutoffExclusive
                      "next_prefix_digest", box main.NextCoveredPrefixDigest
                      "frame_epoch", box (FrameEpochId.value main.FrameEpochId)
                      "observed_prefix_epoch", box (PrefixEpochId.value main.ObservedPrefixEpochId) ]
            | BloggerRequestContext.Squash squash ->
                "squash",
                0L,
                0L,
                squash.FrameDigests,
                createObj
                    [ "kind", box "squash"
                      "frame_epoch", box (FrameEpochId.value squash.FrameEpochId)
                      "covered_frame_count", box squash.CoveredFrameCount
                      "frame_digests", box (squash.FrameDigests |> List.map BlobDigest.value |> Array.ofList)
                      "observed_prefix_epoch", box (PrefixEpochId.value squash.ObservedPrefixEpochId) ]

        // One open request per Blogger. Restart / re-offer with a new RequestId
        // must supersede a stale open slot (fold rejects two opens on one session).
        // PromptKey fill-in reuses the existing open context blob/digest.
        let projections = AgentJournal.snapshot journal

        let existingOpen =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun s -> s.BloggerCycles)
            |> Option.bind (fun cycles -> Map.tryFind requestId cycles.OpenByRequestId)

        let staleOpen =
            projections.AgentProjections.Sessions
            |> Map.tryFind mainSessionId
            |> Option.bind (fun s -> s.BloggerCycles)
            |> Option.bind (fun cycles -> BloggerCycleProjection.tryOpenByBlogger bloggerSessionId cycles)

        match staleOpen with
        | Some openReq when openReq.RequestId <> requestId ->
            BloggerAbandon.byRequestId
                journal
                openReq.RequestId
                mainSessionId
                bloggerSessionId
                "superseded-by-new-materialize"

            Ok()
        | _ -> Ok()
        |> function
            | Error e -> Error e
            | Ok() ->
                let writeContext () =
                    match existingOpen with
                    | Some openReq when openReq.PromptKey.IsNone && promptKey.IsSome ->
                        // Fill-in: keep the frozen pre-send blob/digest.
                        Ok(openReq.ContextRef, openReq.ContextDigest)
                    | Some openReq when openReq.PromptKey = promptKey -> Ok(openReq.ContextRef, openReq.ContextDigest)
                    | _ ->
                        match journal.WriteBlob(Wanxiangshu.OpenCode.CanonicalJson.canonicalJson contextPayload) with
                        | Error error -> Error error
                        | Ok blob -> Ok(blob.BlobRef, blob.BlobDigest)

                match writeContext () with
                | Error error -> Error error
                | Ok(contextRef, contextDigest) ->
                    let fact =
                        ContextFact.BloggerRequestMaterialized
                            {| RequestId = requestId
                               MainSessionId = mainSessionId
                               BloggerSessionId = bloggerSessionId
                               RequestKind = kind
                               ContextRef = contextRef
                               ContextDigest = contextDigest
                               ObservedPrefixEpochId = epoch
                               PreviousIngestedThroughSequence = prevSeq
                               NextIngestedThroughSequence = nextSeq
                               FrameEpochId = frameEpoch
                               SelectedFrameDigests = selectedDigests
                               PromptKey = promptKey |}

                    match AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal with
                    | Error failure -> Error(JournalAppendFailure.describe failure)
                    | Ok _ -> Ok()

    let private abandonRequest (journal: AgentJournal option) (ctx: BloggerRequestContext) (reason: string) : unit =
        match journal with
        | None -> ()
        | Some j ->
            BloggerAbandon.openRequest
                j
                (BloggerRequestContext.mainSessionId ctx)
                (BloggerRequestContext.bloggerSessionId ctx)
                (Some ctx)
                reason

    let private blocksNew = BloggerRuntimeHost.blocksNew
    let private forceSealRuntime = BloggerRuntimeHost.forceSealRuntime

    /// New Authority Root on main after join/return: allow Blogger again.
    let reactivateAfterNewRoot = BloggerRuntimeHost.reactivateAfterNewRoot

    let private startFrozen
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            // Order (C2/C5): materialize durable → flight ownership (SetCurrentRequest dual-writes State shadow) → send.
            let mainId = BloggerRequestContext.mainSessionId ctx

            match journal with
            | None -> return DecisionEffect.MaterializeFailed "no journal"
            | Some j ->
                if blocksNew (Some j) mainId scope key then
                    forceSealRuntime scope key
                    return DecisionEffect.Sealed
                else
                    // Pre-send: freeze semantic context with PromptKey=None.
                    match materializeRequest j ctx None with
                    | Error reason -> return DecisionEffect.MaterializeFailed reason
                    | Ok() ->
                        // Re-check after materialize: main may complete during blob write.
                        // DrainWindow.Open still allows send until next seal cycle ends.
                        if blocksNew (Some j) mainId scope key then
                            abandonRequest journal ctx "main-sealed-before-send"
                            forceSealRuntime scope key
                            return DecisionEffect.Sealed
                        else
                            scope.SetBloggerRuntime(key, cell)
                            scope.SetCurrentRequest(key, ctx)

                            let! sent = host.StartFromContext(ctx)

                            match sent with
                            | Ok promptKey ->
                                // Post-send: bind PromptKey on the open request so commit
                                // can prove this RequestId owns the assistant parent.
                                // Detached accept may still leave parent unresolved until
                                // PhysicalAccepted; binding the key itself is durable now.
                                match materializeRequest j ctx (Some promptKey) with
                                | Error reason ->
                                    abandonRequest journal ctx reason

                                    match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                                    | Ok failed -> scope.SetBloggerRuntime(key, failed)
                                    | Error _ -> ()

                                    scope.ClearCurrentRequest key
                                    host.InvalidateBloggerCache()
                                    return DecisionEffect.StartFailed reason
                                | Ok() ->
                                    match ctx with
                                    | BloggerRequestContext.Squash _ -> return DecisionEffect.StartedSquash
                                    | BloggerRequestContext.Main _ -> return DecisionEffect.Started
                            | Error reason ->
                                abandonRequest journal ctx reason

                                match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                                | Ok failed -> scope.SetBloggerRuntime(key, failed)
                                | Error _ -> ()

                                scope.ClearCurrentRequest key
                                host.InvalidateBloggerCache()
                                return DecisionEffect.StartFailed reason
        }

    let private tryStartSquash
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (observedEpoch: PrefixEpochId)
        (key: string)
        (cell: BloggerRuntimeCell)
        (blog: BlogProjectionState)
        : Task<DecisionEffect option> =
        task {
            // Material wakes the recovery waiter first. Presence of the waiter is the
            // opportunity (physical possession), not a cross-call Armed flag.
            if not (host.OfferRecoveryMaterial()) then
                return None
            else
                let maySquash =
                    RecoverySlot.mayRecover
                        SlotArming.ArmedByAdvance
                        (host.BloggerCursorOffset())
                        (List.length blog.Frames > 0)

                if not maySquash then
                    return None
                else
                    match
                        CompanionHostBlogger.tryBuildSquashContext mainSessionId bloggerSessionId observedEpoch blog
                    with
                    | None -> return None
                    | Some squashCtx ->
                        match BloggerRuntime.onMaterial (scope.HasParked key) cell squashCtx with
                        | Ok(nextCell, BloggerRuntime.Decision.Start startCtx)
                        | Ok(nextCell, BloggerRuntime.Decision.Offer startCtx) ->
                            let! effect = startFrozen scope host journal key nextCell startCtx
                            return Some effect
                        | Ok(nextCell, BloggerRuntime.Decision.Skip) ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return Some DecisionEffect.SkippedInFlight
                        | Ok(nextCell, _) ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return Some DecisionEffect.NoMaterial
                        | Error _ -> return Some DecisionEffect.SkippedInFlight
        }

    /// Unique production entry for main-session material → Blogger lifecycle.
    let onMainMaterial
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : Task<DecisionEffect> =
        task {
            let key = SessionId.value bloggerSessionId

            // GLORY-023: the Manager Life compression floor. `None` for every
            // non-Manager session and for Managers without a Life.
            let floorSequence =
                match journal with
                | None -> None
                | Some durable ->
                    let floor =
                        AgentProjection.tryFind mainSessionId (AgentJournal.snapshot durable).AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                        |> Option.bind (fun life -> life.ProtectedPrefixEnd)
                        |> Option.map (fun cursor -> cursor.Sequence)

                    floor

            if blocksNew journal mainSessionId scope key then
                forceSealRuntime scope key
                return DecisionEffect.Sealed
            elif scope.HasFlight key then
                // Busy = physical flight ownership, not cell.State match.
                return DecisionEffect.SkippedInFlight
            else
                let cell = scope.GetBloggerRuntime key
                // No sealed mirror: blocksNew above already applied the durable
                // seal (DecisionEffect.Sealed) before this point.
                let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host

                let! squashEffect =
                    tryStartSquash scope host journal mainSessionId bloggerSessionId observedEpoch key cell blog

                match squashEffect with
                | Some effect -> return effect
                | None ->
                    match
                        nextMainContext
                            mainSessionId
                            bloggerSessionId
                            observedEpoch
                            blog
                            xTrace
                            floorSequence
                            projection
                    with
                    | None -> return DecisionEffect.NoMaterial
                    | Some ctx ->
                        match BloggerRuntime.onMaterial (scope.HasParked key) (scope.GetBloggerRuntime key) ctx with
                        | Ok(nextCell, BloggerRuntime.Decision.Start startCtx) ->
                            return! startFrozen scope host journal key nextCell startCtx
                        | Ok(nextCell, BloggerRuntime.Decision.Offer offerCtx) ->
                            scope.SetBloggerRuntime(key, nextCell)
                            let resumed = scope.SetPendingOffer(key, offerCtx)
                            return DecisionEffect.OfferedParked resumed
                        | Ok(nextCell, BloggerRuntime.Decision.Skip) ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return DecisionEffect.SkippedInFlight
                        | Ok(nextCell, _) ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return DecisionEffect.NoMaterial
                        | Error _ -> return DecisionEffect.SkippedInFlight
        }

    /// AABB: rebuild Main context from latest projection at the same prev ingest.
    /// Does not advance coverage; replaces frozen Toml/RequestId/next_* for retry.
    let tryRefreshMainContext
        (journal: AgentJournal option)
        (host: CompanionHost)
        (scope: IParkedTransformHost)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : BloggerRequestContext option =
        let key = SessionId.value bloggerSessionId

        if blocksNew journal mainSessionId scope key then
            None
        else
            // GLORY-023: same Manager Life floor as onMainMaterial.
            let floorSequence =
                match journal with
                | None -> None
                | Some durable ->
                    AgentProjection.tryFind mainSessionId (AgentJournal.snapshot durable).AgentProjections
                    |> Option.bind (fun session -> session.ManagerLife)
                    |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                    |> Option.bind (fun life -> life.ProtectedPrefixEnd)
                    |> Option.map (fun cursor -> cursor.Sequence)

            let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host
            nextMainContext mainSessionId bloggerSessionId observedEpoch blog xTrace floorSequence projection
