namespace Wanxiangshu.Session

open System
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Kernel
open Wanxiangshu.Tools
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Domain
open Wanxiangshu.Domain.ProviderProjection
open Wanxiangshu.Recovery

type CompanionHost
    (
        primaryId: SessionId,
        sessions: ISessionHostPort,
        ?durable: ICompanionDurablePort,
        ?onBloggerCreated: SessionId -> unit,
        ?restoredBloggerId: string,
        ?journal: AgentJournal,
        ?bloggerDirectory: string,
        ?satelliteRuntime: SatelliteRuntime
    ) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let bloggerCreated = defaultArg onBloggerCreated (fun _ -> ())

    let bloggerEffectiveAgent = ManagedAgent.nameOf AgentTier.Fast Role.Blogger

    // DSL-MUTABLE: single-flight — memoized blogger create task
    let mutable bloggerCreateTask: Task<SessionId> option = None
    // DSL-MUTABLE: resource — resolved blogger session id
    let mutable bloggerId: SessionId option = None
    // DSL-MUTABLE: resource — create-failure latch, cleared on retry
    let mutable bloggerCreateFailed = false
    // DSL-MUTABLE: resource — one-shot restored-blogger id consumption
    let mutable restoredBloggerIdOpt = restoredBloggerId

    let ensureBlogger () =
        lock gate (fun () ->
            match bloggerCreateTask with
            | Some _ when bloggerCreateFailed ->
                bloggerCreateTask <- None
                bloggerId <- None
                bloggerCreateFailed <- false
            | _ -> ()

            match bloggerCreateTask with
            | Some task -> task
            | None ->
                let task =
                    match satelliteRuntime with
                    | Some runtime ->
                        task {
                            let spec =
                                { Kind = SatelliteKind.Companion
                                  Agent = bloggerEffectiveAgent
                                  Title = bloggerEffectiveAgent
                                  Directory = bloggerDirectory
                                  RestoredSessionId = restoredBloggerIdOpt |> Option.map SessionId.create
                                  Link =
                                    fun owner id agent ->
                                        task {
                                            match durable with
                                            | None ->
                                                companion.RecordBloggerLinked id
                                                return Ok()
                                            | Some port ->
                                                match! port.LinkBlogger(owner, id, agent) with
                                                | Ok() ->
                                                    companion.RecordBloggerLinked id
                                                    return Ok()
                                                | Error error -> return Error error
                                        }
                                  Close =
                                    fun owner ->
                                        match durable with
                                        | None -> Task.FromResult(Ok())
                                        | Some port -> port.CloseBlogger owner }

                            match! runtime.Ensure(primaryId, spec) with
                            | Error error ->
                                bloggerCreateFailed <- true
                                bloggerId <- None
                                return raise (InvalidOperationException error)
                            | Ok lease ->
                                bloggerId <- Some lease.SessionId
                                bloggerCreateFailed <- false
                                restoredBloggerIdOpt <- None
                                bloggerCreated lease.SessionId
                                return lease.SessionId
                        }
                    | None ->
                        task {
                            try
                                match restoredBloggerIdOpt, bloggerId with
                                | Some id, None ->
                                    let sid = SessionId.create id
                                    bloggerId <- Some sid
                                    bloggerCreateFailed <- false
                                    bloggerCreated sid
                                    restoredBloggerIdOpt <- None
                                    return sid
                                | _ ->
                                    let! created =
                                        sessions.CreateChildSession(
                                            primaryId,
                                            { Title = Some bloggerEffectiveAgent
                                              Agent = Some bloggerEffectiveAgent
                                              Directory = bloggerDirectory }
                                        )

                                    match created with
                                    | Ok id ->
                                        bloggerId <- Some id
                                        bloggerCreateFailed <- false
                                        bloggerCreated id

                                        match durable with
                                        | None -> companion.RecordBloggerLinked id
                                        | Some port ->
                                            match! port.LinkBlogger(primaryId, id, bloggerEffectiveAgent) with
                                            | Ok() -> companion.RecordBloggerLinked id
                                            | Error error -> raise (InvalidOperationException error)

                                        return id
                                    | Error error -> return raise (InvalidOperationException error)
                            with ex ->
                                bloggerCreateFailed <- true
                                bloggerId <- None
                                return raise ex
                        }

                bloggerCreateTask <- Some task
                task)

    member private this.BloggerDeps: CompanionHostBlogger.BloggerDeps =
        { Sessions = sessions
          PrimaryId = primaryId
          Durable = durable
          EnsureBlogger = ensureBlogger
          Gate = gate
          Companion = companion
          Journal = journal
          EffectiveAgent = bloggerEffectiveAgent
          RecordSquashPlan = fun bloggerId providerRun -> this.RecordSquashPlan bloggerId providerRun
          StageBloggerContext = fun bloggerId ctx -> this.StageBloggerContext bloggerId ctx }

    /// CTX-006 step 5: the squash attempt's plan hook on the Y chain.
    ///
    /// The default here is a no-op because `Plugin.fs` is the only owner of the
    /// `PluginRuntimeScope`; the composition root rebinds this when it constructs
    /// the CompanionHost so the squash attempt lands in `scope.Recovery.AttemptPlans` like
    /// any X attempt. A no-op is correct for a scope-less CompanionHost (tests,
    /// tools), which has no reconcile pass that could consult a plan.
    member val RecordSquashPlan: SessionId -> ProviderRunIdentity -> unit = fun _ _ -> () with get, set

    /// ENFORCER-045: optional stage hook kept for tests; production freezes
    /// CurrentRequest via BloggerCoordinator before send (not this callback).
    member val StageBloggerContext: SessionId -> BloggerRequestContext -> unit = fun _ _ -> () with get, set

    /// Ensure the Blogger child exists (create or restore). Key for runtime cell.
    member this.EnsureBloggerAsync() : Task<SessionId> = ensureBlogger ()

    member this.StartFromContext(ctx: BloggerRequestContext) : Task<Result<PromptKey, string>> =
        CompanionHostBlogger.startFromContext this.BloggerDeps ctx

    /// C4: send failure / dead child must drop cached SessionId and Ensure task.
    /// Next material creates a fresh Blogger; never keep Parked after fail.
    member this.InvalidateBloggerCache() : unit =
        lock gate (fun () ->
            satelliteRuntime
            |> Option.iter (fun runtime -> runtime.Invalidate(primaryId, SatelliteKind.Companion))

            bloggerCreateTask <- None
            bloggerId <- None
            bloggerCreateFailed <- true
            companion.RecordBloggerClosed())

    /// CTX-006 / FALLBACK-012: open a one-shot recovery opportunity (physical waiter).
    member this.StartRecoveryOpportunity() : Task = companion.StartRecoveryOpportunity()

    /// Material boundary: offer main material to a pending recovery waiter.
    /// True when a waiter was taken (recovery path may consume this material).
    member this.OfferRecoveryMaterial() : bool = companion.OfferRecoveryMaterial()

    /// CTX-006: primary session fallback cursor Offset (durable, not cached).
    /// FALLBACK-002: the offset leaves this boundary as the closed DU.
    member this.BloggerCursorOffset() : AgentPairCursor.FallbackOffset =
        match journal with
        | Some j ->
            match FallbackEvidence.tryCurrentState primaryId (AgentJournal.snapshot j) with
            | Some current -> current.Cursor.Offset
            | None -> AgentPairCursor.FallbackOffset.Fork0
        | None -> AgentPairCursor.FallbackOffset.Fork0

    /// Exposes the canonical CompanionFlow calculation for adapters and tests.
    member _.PreviewDelta(projection: ProviderSemanticProjection) =
        let memory = companion.Memory

        CompanionProgram.runCompanionFlow
            { SessionId = SessionId.value primaryId }
            System.Threading.CancellationToken.None
            (CompanionProgram.buildDelta
                (XTraceProjection.semanticCursorFor memory.Blog.Coverage.IngestedThroughSequence memory.XTrace)
                memory.Blog.Coverage.CoverableTurnCutoffExclusive
                projection)

    member _.Memory = companion.Memory

    /// COMPANION-007: forward the transform-boundary XTrace refresh to the
    /// companion's in-memory mirror.
    member _.RefreshXTrace(state: XTraceProjectionState) = companion.RefreshXTrace(state)

    member _.WaitInFlightAsync() = companion.WaitInFlightAsync()

    /// COMPANION-005 / CTX-002: return Host messages unchanged.
    ///
    /// Blogger material decisions are NOT made here. BloggerCoordinator.OnMainMaterial
    /// is the sole entry (C1). Prefix replacement stays behind a failed attempt
    /// (CTX-012), which this hook cannot see.
    member _.TransformRaw(messages: obj list) : obj list = messages

    member _.BloggerSession = lock gate (fun () -> bloggerId)

    member _.PrimarySessionId = primaryId

    /// Tear down the Blogger child and record the durable unlink on the same
    /// session stream so a restart never mistakes a dead child for a live link.
    member this.CloseBloggerAsync() : Task =
        task {
            let taskOpt = lock gate (fun () -> bloggerCreateTask)

            match taskOpt with
            | Some task ->
                let! childId = task

                match satelliteRuntime with
                | Some runtime ->
                    let spec =
                        { Kind = SatelliteKind.Companion
                          Agent = bloggerEffectiveAgent
                          Title = bloggerEffectiveAgent
                          Directory = bloggerDirectory
                          RestoredSessionId = Some childId
                          Link = fun _ _ _ -> Task.FromResult(Ok())
                          Close =
                            fun owner ->
                                match durable with
                                | None -> Task.FromResult(Ok())
                                | Some port -> port.CloseBlogger owner }

                    match! runtime.Retire(primaryId, spec) with
                    | Ok() -> ()
                    | Error error -> raise (InvalidOperationException error)
                | None ->
                    let! aborted = sessions.AbortSession(childId)

                    match aborted with
                    | Ok() -> ()
                    | Error error -> raise (InvalidOperationException error)

                    match durable with
                    | None -> ()
                    | Some port ->
                        match! port.CloseBlogger primaryId with
                        | Ok() -> ()
                        | Error error -> raise (InvalidOperationException error)

                companion.RecordBloggerClosed()
            | None -> ()
        }

    interface IDisposable with
        member this.Dispose() =
            // Plugin disposal is not owner deletion: preserve the durable link
            // and Host child so the next plugin instance can prove and reuse it.
            // IDisposable is synchronous, so it must not launch an unobserved
            // retire task that can outlive the journal writer.
            this.InvalidateBloggerCache()
