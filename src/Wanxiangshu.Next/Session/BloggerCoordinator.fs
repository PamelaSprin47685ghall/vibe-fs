namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode

/// ENFORCER-047/050: the ONE main-session decision entry for Blogger material.
///
/// Call once per main transform. Reads durable coverage, builds one typed context,
/// transitions BloggerRuntimeCell, and performs at most one external effect
/// (Start prompt or PendingOffer resume). Never Submit-then-Offer in one call.
module BloggerCoordinator =

    type DecisionEffect =
        | Started
        | SkippedInFlight
        | OfferedParked of resumed: bool
        | NoMaterial
        | Disposed
        | StartFailed of string

    let private loadProjections
        (journal: AgentJournal option)
        (mainSessionId: SessionId)
        (host: CompanionHost)
        : BlogProjectionState * XTraceProjectionState =
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

            blog, xTrace
        | None -> host.Memory.Blog, host.Memory.XTrace

    let private nextMainContext
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
        | Some chunk -> Some(EnforcerHost.mainContextFromChunk blog xTrace projection chunk)

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
            | BloggerRuntimeState.InFlight _ ->
                // Material stays in durable XTrace; CurrentRequest must not move.
                return DecisionEffect.SkippedInFlight
            | BloggerRuntimeState.Idle
            | BloggerRuntimeState.Parked ->
                let blog, xTrace = loadProjections journal mainSessionId host

                match nextMainContext blog xTrace projection with
                | None -> return DecisionEffect.NoMaterial
                | Some ctx ->
                    match BloggerRuntime.onMaterial cell ctx with
                    | Error BloggerRuntime.TransitionError.Disposed -> return DecisionEffect.Disposed
                    | Error _ -> return DecisionEffect.SkippedInFlight
                    | Ok(nextCell, decision) ->
                        match decision with
                        | BloggerRuntime.Decision.Skip ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return DecisionEffect.SkippedInFlight
                        | BloggerRuntime.Decision.Offer offerCtx ->
                            // Parked: PendingOffer only. Never touch CurrentRequest.
                            scope.SetBloggerRuntime(key, nextCell)
                            let resumed = scope.SetPendingOffer(key, offerCtx)
                            return DecisionEffect.OfferedParked resumed
                        | BloggerRuntime.Decision.Start startCtx ->
                            // Idle → InFlight: freeze CurrentRequest before physical send.
                            scope.SetBloggerRuntime(key, nextCell)
                            scope.SetCurrentRequest(key, startCtx)

                            let! sent = host.StartMainFromContext(startCtx)

                            match sent with
                            | Ok _ -> return DecisionEffect.Started
                            | Error reason ->
                                match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                                | Ok failed -> scope.SetBloggerRuntime(key, failed)
                                | Error _ -> ()

                                scope.ClearCurrentRequest key
                                return DecisionEffect.StartFailed reason
                        | BloggerRuntime.Decision.Ignore ->
                            scope.SetBloggerRuntime(key, nextCell)
                            return DecisionEffect.NoMaterial
        }
