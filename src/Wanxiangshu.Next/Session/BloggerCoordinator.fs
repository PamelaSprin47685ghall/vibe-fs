namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

/// ENFORCER-047/050: the ONE main-session decision entry for Blogger material.
module BloggerCoordinator =

    type DecisionEffect =
        | Started
        | StartedSquash
        | SkippedInFlight
        | OfferedParked of resumed: bool
        | NoMaterial
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
            Some(
                EnforcerHost.mainContextFromChunk
                    mainSessionId
                    bloggerSessionId
                    observedEpoch
                    blog
                    xTrace
                    projection
                    chunk
            )

    /// C5: durable materialization BEFORE physical send. Context blob is the
    /// irrecomputable semantic input; recovery reads this + Host snapshot + receipts.
    let private materializeRequest
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
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
                      "frame_digests",
                      box (squash.FrameDigests |> List.map BlobDigest.value |> Array.ofList)
                      "observed_prefix_epoch", box (PrefixEpochId.value squash.ObservedPrefixEpochId) ]

        match journal.WriteBlob(CanonicalJson.canonicalJson contextPayload) with
        | Error error -> Error error
        | Ok blob ->
            let fact =
                AgentFact.BloggerRequestMaterialized
                    {| RequestId = requestId
                       MainSessionId = mainSessionId
                       BloggerSessionId = bloggerSessionId
                       RequestKind = kind
                       ContextRef = blob.BlobRef
                       ContextDigest = blob.BlobDigest
                       ObservedPrefixEpochId = epoch
                       PreviousIngestedThroughSequence = prevSeq
                       NextIngestedThroughSequence = nextSeq
                       FrameEpochId = frameEpoch
                       SelectedFrameDigests = selectedDigests
                       PromptKey = None |}

            match AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal with
            | Error failure -> Error(JournalAppendFailure.describe failure)
            | Ok _ -> Ok()

    let private abandonRequest
        (journal: AgentJournal option)
        (ctx: BloggerRequestContext)
        (reason: string)
        : unit =
        match journal with
        | None -> ()
        | Some j ->
            let fact =
                AgentFact.BloggerRequestAbandoned
                    {| RequestId = BloggerRequestContext.requestId ctx
                       MainSessionId = BloggerRequestContext.mainSessionId ctx
                       BloggerSessionId = BloggerRequestContext.bloggerSessionId ctx
                       Reason = reason |}

            AgentJournal.appendAgent
                (StreamId.Session(BloggerRequestContext.mainSessionId ctx))
                None
                fact
                j
            |> ignore

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
            match journal with
            | None -> return DecisionEffect.MaterializeFailed "no journal"
            | Some j ->
                match materializeRequest j ctx with
                | Error reason -> return DecisionEffect.MaterializeFailed reason
                | Ok() ->
                    scope.SetBloggerRuntime(key, cell)
                    scope.SetCurrentRequest(key, ctx)

                    let! sent = host.StartFromContext(ctx)

                    match sent with
                    | Ok _ ->
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
                match
                    CompanionHostBlogger.tryBuildSquashContext
                        mainSessionId
                        bloggerSessionId
                        observedEpoch
                        blog
                with
                | None -> return None
                | Some squashCtx ->
                    match BloggerRuntime.onMaterial cell squashCtx with
                    | Ok(_, BloggerRuntime.Decision.Start startCtx)
                    | Ok(_, BloggerRuntime.Decision.Offer startCtx) ->
                        host.DisarmRecoverySlot()

                        let inflight =
                            { State = BloggerRuntimeState.InFlight startCtx
                              PendingOffer = None
                              RepairSpent = false }

                        let! effect = startFrozen scope host journal key inflight startCtx
                        return Some effect
                    | Ok(nextCell, BloggerRuntime.Decision.Skip) ->
                        scope.SetBloggerRuntime(key, nextCell)
                        return Some DecisionEffect.SkippedInFlight
                    | Ok(nextCell, _) ->
                        scope.SetBloggerRuntime(key, nextCell)
                        return Some DecisionEffect.NoMaterial
                    | Error BloggerRuntime.TransitionError.Disposed -> return Some DecisionEffect.Disposed
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
            let cell = scope.GetBloggerRuntime key

            match cell.State with
            | BloggerRuntimeState.Disposed -> return DecisionEffect.Disposed
            | BloggerRuntimeState.InFlight _ -> return DecisionEffect.SkippedInFlight
            | BloggerRuntimeState.Idle
            | BloggerRuntimeState.Parked ->
                let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host

                let! squashEffect =
                    tryStartSquash
                        scope
                        host
                        journal
                        mainSessionId
                        bloggerSessionId
                        observedEpoch
                        key
                        cell
                        blog

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
                            projection
                    with
                    | None -> return DecisionEffect.NoMaterial
                    | Some ctx ->
                        match BloggerRuntime.onMaterial (scope.GetBloggerRuntime key) ctx with
                        | Error BloggerRuntime.TransitionError.Disposed -> return DecisionEffect.Disposed
                        | Error _ -> return DecisionEffect.SkippedInFlight
                        | Ok(nextCell, decision) ->
                            match decision with
                            | BloggerRuntime.Decision.Skip ->
                                scope.SetBloggerRuntime(key, nextCell)
                                return DecisionEffect.SkippedInFlight
                            | BloggerRuntime.Decision.Offer offerCtx ->
                                scope.SetBloggerRuntime(key, nextCell)
                                let resumed = scope.SetPendingOffer(key, offerCtx)
                                return DecisionEffect.OfferedParked resumed
                            | BloggerRuntime.Decision.Start startCtx ->
                                return! startFrozen scope host journal key nextCell startCtx
                            | BloggerRuntime.Decision.Ignore ->
                                scope.SetBloggerRuntime(key, nextCell)
                                return DecisionEffect.NoMaterial
        }
