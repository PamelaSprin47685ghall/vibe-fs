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
        | Disposed
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
        (projection: ProviderSemanticProjection)
        : BloggerRequestContext option =
        let ingestCursor =
            XTraceProjection.semanticCursorFor blog.Coverage.IngestedThroughSequence xTrace

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
                        AgentFact.BloggerRequestMaterialized
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
            // Order (C2/C5): materialize durable → CurrentRequest → InFlight → send.
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
            let maySquash =
                RecoverySlot.mayRecover
                    (if host.IsRecoveryArmed then
                         SlotArming.ArmedByAdvance
                     else
                         SlotArming.NotArmed)
                    (host.BloggerCursorOffset())
                    (List.length blog.Frames > 0)

            if not maySquash then
                return None
            else
                match CompanionHostBlogger.tryBuildSquashContext mainSessionId bloggerSessionId observedEpoch blog with
                | None -> return None
                | Some squashCtx ->
                    match BloggerRuntime.onMaterial cell squashCtx with
                    | Ok(nextCell, BloggerRuntime.Decision.Start startCtx)
                    | Ok(nextCell, BloggerRuntime.Decision.Offer startCtx) ->
                        host.DisarmRecoverySlot()
                        let! effect = startFrozen scope host journal key nextCell startCtx
                        return Some effect
                    | Ok(nextCell, BloggerRuntime.Decision.Skip) ->
                        scope.SetBloggerRuntime(key, nextCell)
                        return Some DecisionEffect.SkippedInFlight
                    | Ok(nextCell, _) ->
                        scope.SetBloggerRuntime(key, nextCell)
                        return Some DecisionEffect.NoMaterial
                    | Error BloggerRuntime.TransitionError.Disposed -> return Some DecisionEffect.Disposed
                    | Error BloggerRuntime.TransitionError.Sealed -> return Some DecisionEffect.Sealed
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

            if blocksNew journal mainSessionId scope key then
                forceSealRuntime scope key
                return DecisionEffect.Sealed
            else
                let cell = scope.GetBloggerRuntime key

                match cell.State with
                | BloggerRuntimeState.Disposed -> return DecisionEffect.Disposed
                | BloggerRuntimeState.Sealed ->
                    // Durable may have been reactivated; state still Sealed is stale.
                    if BloggerRuntime.isDrainOpen cell then
                        scope.SetBloggerRuntime(key, BloggerRuntime.onReactivate cell)
                    else
                        ()

                    let cell2 = scope.GetBloggerRuntime key

                    match cell2.State with
                    | BloggerRuntimeState.Sealed -> return DecisionEffect.Sealed
                    | _ ->
                        // fall through by tail-calling logic via Idle path
                        let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host

                        match nextMainContext mainSessionId bloggerSessionId observedEpoch blog xTrace projection with
                        | None -> return DecisionEffect.NoMaterial
                        | Some ctx ->
                            match BloggerRuntime.onMaterial (scope.GetBloggerRuntime key) ctx with
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
                            | Error BloggerRuntime.TransitionError.Disposed -> return DecisionEffect.Disposed
                            | Error BloggerRuntime.TransitionError.Sealed -> return DecisionEffect.Sealed
                            | Error _ -> return DecisionEffect.SkippedInFlight
                | BloggerRuntimeState.InFlight _ -> return DecisionEffect.SkippedInFlight
                | BloggerRuntimeState.Idle
                | BloggerRuntimeState.Parked ->
                    let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host

                    let! squashEffect =
                        tryStartSquash scope host journal mainSessionId bloggerSessionId observedEpoch key cell blog

                    match squashEffect with
                    | Some effect -> return effect
                    | None ->
                        match nextMainContext mainSessionId bloggerSessionId observedEpoch blog xTrace projection with
                        | None -> return DecisionEffect.NoMaterial
                        | Some ctx ->
                            match BloggerRuntime.onMaterial (scope.GetBloggerRuntime key) ctx with
                            | Error BloggerRuntime.TransitionError.Disposed -> return DecisionEffect.Disposed
                            | Error BloggerRuntime.TransitionError.Sealed -> return DecisionEffect.Sealed
                            | Error _ -> return DecisionEffect.SkippedInFlight
                            | Ok(nextCell, decision) ->
                                match decision with
                                | BloggerRuntime.Decision.Skip ->
                                    scope.SetBloggerRuntime(key, nextCell)
                                    return DecisionEffect.SkippedInFlight
                                | BloggerRuntime.Decision.Offer offerCtx ->
                                    if blocksNew journal mainSessionId scope key then
                                        forceSealRuntime scope key
                                        return DecisionEffect.Sealed
                                    else
                                        scope.SetBloggerRuntime(key, nextCell)
                                        let resumed = scope.SetPendingOffer(key, offerCtx)
                                        return DecisionEffect.OfferedParked resumed
                                | BloggerRuntime.Decision.Start startCtx ->
                                    return! startFrozen scope host journal key nextCell startCtx
                                | BloggerRuntime.Decision.Ignore ->
                                    scope.SetBloggerRuntime(key, nextCell)
                                    return DecisionEffect.NoMaterial
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
            let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host
            nextMainContext mainSessionId bloggerSessionId observedEpoch blog xTrace projection
