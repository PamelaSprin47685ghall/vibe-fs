namespace Wanxiangshu.Next.Session

open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection
open Wanxiangshu.Next.Journal
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

    let private startFrozen
        (scope: IParkedTransformHost)
        (host: CompanionHost)
        (key: string)
        (cell: BloggerRuntimeCell)
        (ctx: BloggerRequestContext)
        : Task<DecisionEffect> =
        task {
            scope.SetBloggerRuntime(key, cell)
            scope.SetCurrentRequest(key, ctx)

            let! sent = host.StartFromContext(ctx)

            match sent with
            | Ok _ ->
                match ctx with
                | BloggerRequestContext.Squash _ -> return DecisionEffect.StartedSquash
                | BloggerRequestContext.Main _ -> return DecisionEffect.Started
            | Error reason ->
                match BloggerRuntime.onFail (scope.GetBloggerRuntime key) with
                | Ok failed -> scope.SetBloggerRuntime(key, failed)
                | Error _ -> ()

                scope.ClearCurrentRequest key
                return DecisionEffect.StartFailed reason
        }

    let private tryStartSquash
        (scope: IParkedTransformHost)
        (host: CompanionHost)
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
                match CompanionHostBlogger.tryBuildSquashContext blog with
                | None -> return None
                | Some squashCtx ->
                    match BloggerRuntime.onMaterial cell squashCtx with
                    | Ok(_, BloggerRuntime.Decision.Start startCtx)
                    | Ok(_, BloggerRuntime.Decision.Offer startCtx) ->
                        host.DisarmRecoverySlot()

                        let inflight =
                            { State = BloggerRuntimeState.InFlight startCtx
                              PendingOffer = None }

                        let! effect = startFrozen scope host key inflight startCtx
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
                let blog, xTrace = loadProjections journal mainSessionId host

                let! squashEffect = tryStartSquash scope host key cell blog

                match squashEffect with
                | Some effect -> return effect
                | None ->
                    match nextMainContext blog xTrace projection with
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
                                return! startFrozen scope host key nextCell startCtx
                            | BloggerRuntime.Decision.Ignore ->
                                scope.SetBloggerRuntime(key, nextCell)
                                return DecisionEffect.NoMaterial
        }
