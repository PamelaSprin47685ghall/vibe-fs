namespace Wanxiangshu.Context.Companion.Blogger.Runtime

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Fable.Core.JsInterop
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
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection
open Wanxiangshu.Host
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

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

    let private encodeContextPayload (ctx: BloggerRequestContext) =
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

    let private abandonStaleOpen
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (requestId: BloggerRequestId)
        (staleOpen: OpenBloggerRequest option)
        : Task<unit> =
        task {
            match staleOpen with
            | Some openReq when openReq.RequestId <> requestId ->
                do!
                    BloggerAbandon.byRequestId
                        journal
                        openReq.RequestId
                        mainSessionId
                        bloggerSessionId
                        "superseded-by-new-materialize"
            | _ -> ()
        }

    let private resolveContextBlob
        (journal: AgentJournal)
        (existingOpen: OpenBloggerRequest option)
        (promptKey: PromptKey option)
        (contextPayload: obj)
        : Task<Result<BlobRef * BlobDigest, string>> =
        match existingOpen with
        | Some openReq when openReq.PromptKey.IsNone && promptKey.IsSome ->
            Task.FromResult(Ok(openReq.ContextRef, openReq.ContextDigest))
        | Some openReq when openReq.PromptKey = promptKey ->
            Task.FromResult(Ok(openReq.ContextRef, openReq.ContextDigest))
        | _ ->
            taskResult {
                let! blob = journal.WriteBlob(CanonicalJson.canonicalJson contextPayload)
                return blob.BlobRef, blob.BlobDigest
            }

    let private appendMaterialized
        (journal: AgentJournal)
        (mainSessionId: SessionId)
        (fact: AgentFact)
        : Task<Result<unit, string>> =
        task {
            let! result = AgentJournal.appendAgent (StreamId.Session mainSessionId) None fact journal
            return result |> Result.map ignore |> Result.mapError JournalAppendFailure.describe
        }

    /// C5: durable materialization. Context blob is the irrecomputable semantic
    /// input. Pre-send PromptKey=None; after physical send, re-append with the
    /// same ContextDigest + Some PromptKey so commit can prove ownership.
    let materializeRequest
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (promptKey: PromptKey option)
        : Task<Result<unit, string>> =
        taskResult {
            let requestId = BloggerRequestContext.requestId ctx
            let mainSessionId = BloggerRequestContext.mainSessionId ctx
            let bloggerSessionId = BloggerRequestContext.bloggerSessionId ctx
            let epoch = BloggerRequestContext.observedPrefixEpoch ctx
            let frameEpoch = BloggerRequestContext.frameEpochId ctx

            let kind, prevSeq, nextSeq, selectedDigests, contextPayload =
                encodeContextPayload ctx

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

            do!
                abandonStaleOpen journal mainSessionId bloggerSessionId requestId staleOpen
                |> TaskResultCE.ofTask

            let! contextRef, contextDigest = resolveContextBlob journal existingOpen promptKey contextPayload

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

            do! appendMaterialized journal mainSessionId fact
        }

    let private abandonRequest (journal: AgentJournal option) (ctx: BloggerRequestContext) (reason: string) : Task =
        match journal with
        | None -> Task.FromResult(()) :> Task
        | Some j ->
            BloggerAbandon.openRequest
                j
                (BloggerRequestContext.mainSessionId ctx)
                (BloggerRequestContext.bloggerSessionId ctx)
                (Some ctx)
                reason

    let stageContinuationContext
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<unit, string>> =
        task {
            match! materializeRequest journal ctx None with
            | Error reason -> return Error reason
            | Ok() ->
                scope.SetCurrentRequest(SessionId.value (BloggerRequestContext.bloggerSessionId ctx), ctx)
                return Ok()
        }

    let bindContinuationContext (journal: AgentJournal) (ctx: BloggerRequestContext) (promptKey: PromptKey) =
        materializeRequest journal ctx (Some promptKey)

    let abandonContinuationContext
        (scope: IParkedTransformHost)
        (journal: AgentJournal)
        (ctx: BloggerRequestContext)
        (reason: string)
        : Task =
        task {
            do! abandonRequest (Some journal) ctx reason
            scope.ClearCurrentRequest(SessionId.value (BloggerRequestContext.bloggerSessionId ctx))
        }

    let private blocksNew = BloggerRuntimeHost.blocksNew
    let private forceSealRuntime = BloggerRuntimeHost.forceSealRuntime

    /// New Authority Root on main after join/return: allow Blogger again.
    let reactivateAfterNewRoot = BloggerRuntimeHost.reactivateAfterNewRoot

    let private startedEffect (ctx: BloggerRequestContext) =
        match ctx with
        | BloggerRequestContext.Squash _ -> DecisionEffect.StartedSquash
        | BloggerRequestContext.Main _ -> DecisionEffect.Started

    let private failAfterSend
        (journal: AgentJournal option)
        (host: CompanionHost)
        (scope: IParkedTransformHost)
        (key: string)
        (ctx: BloggerRequestContext)
        (reason: string)
        : Task<DecisionEffect> =
        task {
            do! abandonRequest journal ctx reason
            scope.ClearCurrentRequest key
            host.InvalidateBloggerCache()
            return DecisionEffect.StartFailed reason
        }

    let private bindSendAndPostMaterialize
        (host: CompanionHost)
        (j: AgentJournal)
        (ctx: BloggerRequestContext)
        : Task<Result<DecisionEffect, string>> =
        taskResult {
            let! promptKey = host.StartFromContext(ctx)
            do! materializeRequest j ctx (Some promptKey)
            return startedEffect ctx
        }

    let private proceedAfterPreSend
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            scope.SetCurrentRequest(key, ctx)
            let! outcome = bindSendAndPostMaterialize host j ctx

            match outcome with
            | Ok effect -> return effect
            | Error reason -> return! failAfterSend journal host scope key ctx reason
        }

    let private afterPreSendMaterialize
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (mainId: SessionId)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        if blocksNew (Some j) mainId scope key then
            task {
                do! abandonRequest journal ctx "main-sealed-before-send"
                forceSealRuntime scope key
                return DecisionEffect.Sealed
            }
        else
            proceedAfterPreSend scope host journal j key ctx

    let private materializeThenSend
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (mainId: SessionId)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            match! materializeRequest j ctx None with
            | Error reason -> return DecisionEffect.MaterializeFailed reason
            | Ok() -> return! afterPreSendMaterialize scope host journal j mainId key ctx
        }

    let private startWithJournal
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (j: AgentJournal)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        // Order (C2/C5): materialize durable → flight ownership (SetCurrentRequest) → send.
        // No cell.State write: physical flight registry is the busy authority.
        let mainId = BloggerRequestContext.mainSessionId ctx

        if blocksNew (Some j) mainId scope key then
            forceSealRuntime scope key
            Task.FromResult DecisionEffect.Sealed
        else
            materializeThenSend scope host journal j mainId key ctx

    let private startFrozen
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        match journal with
        | None -> Task.FromResult(DecisionEffect.MaterializeFailed "no journal")
        | Some j -> startWithJournal scope host journal j key ctx

    let private applyMainDecision
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        match BloggerRuntime.decideMaterial (scope.HasParked key) (scope.HasFlight key) ctx with
        | BloggerRuntime.Decision.Start startCtx -> startFrozen scope host journal key startCtx
        | BloggerRuntime.Decision.Offer offerCtx ->
            let resumed = scope.SetPendingOffer(key, offerCtx)
            Task.FromResult(DecisionEffect.OfferedParked resumed)
        | BloggerRuntime.Decision.Skip -> Task.FromResult DecisionEffect.SkippedInFlight

    let private startMainOrNone
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (key: string)
        (ctxOpt: BloggerRequestContext option)
        : Task<DecisionEffect> =
        match ctxOpt with
        | None -> Task.FromResult DecisionEffect.NoMaterial
        | Some ctx -> applyMainDecision scope host journal key ctx

    /// Unique production entry for main-session material → Blogger lifecycle.
    let onMainMaterial
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (bloggerSessionId: SessionId)
        (projection: ProviderSemanticProjection)
        : Task<DecisionEffect> =
        let key = SessionId.value bloggerSessionId

        if blocksNew journal mainSessionId scope key then
            forceSealRuntime scope key
            Task.FromResult DecisionEffect.Sealed
        elif scope.HasFlight key then
            // Busy = physical flight ownership, not cell.State match.
            Task.FromResult DecisionEffect.SkippedInFlight
        else
            // No sealed mirror: blocksNew above already applied the durable
            // seal (DecisionEffect.Sealed) before this point.
            // No cell: decideMaterial routes from HasParked + HasFlight only.
            task {
                let blog, xTrace, observedEpoch = loadProjections journal mainSessionId host
                return!
                    startMainOrNone
                        scope
                        host
                        journal
                        key
                        (BloggerMainContext.fromProjection
                            journal
                            mainSessionId
                            bloggerSessionId
                            observedEpoch
                            blog
                            xTrace
                            projection)
            }
