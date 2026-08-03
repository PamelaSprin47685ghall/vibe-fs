namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Kernel.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Domain.ProviderProjection

type CompanionHost
    (
        primaryId: SessionId,
        sessions: ISessionHostPort,
        ?durable: ICompanionDurablePort,
        ?onBloggerCreated: SessionId -> unit,
        ?restoredBloggerId: string,
        ?journal: AgentJournal,
        ?bloggerDirectory: string
    ) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)
    let gate = obj ()
    let bloggerCreated = defaultArg onBloggerCreated (fun _ -> ())

    let bloggerEffectiveAgent = ManagedAgent.nameOf AgentTier.Fast Role.Blogger

    let mutable bloggerTask: Task<SessionId> option = None
    let mutable bloggerId: SessionId option = None
    let mutable bloggerFailed = false
    let bloggerRequestKind = ref ProviderRequestKind.BloggerMain
    let bloggerSquashFrameCount = ref None
    let mutable restoredBloggerIdOpt = restoredBloggerId

    let ensureBlogger () =
        lock gate (fun () ->
            match bloggerTask with
            | Some _ when bloggerFailed ->
                bloggerTask <- None
                bloggerId <- None
                bloggerFailed <- false
            | _ -> ()

            match bloggerTask with
            | Some task -> task
            | None ->
                // Restore is one-shot. Dead restored child fails send; next material
                // creates a new child. Request semantics always use durable frames +
                // X gap via typed context (no full-X reset replay).
                match restoredBloggerIdOpt, bloggerId with
                | Some id, None ->
                    let sid = SessionId.create id
                    bloggerId <- Some sid
                    bloggerFailed <- false
                    bloggerCreated sid
                    restoredBloggerIdOpt <- None
                    let t = Task.FromResult(sid)
                    bloggerTask <- Some t
                    t
                | _ ->
                    let task =
                        task {
                            try
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
                                    bloggerFailed <- false

                                    bloggerCreated id

                                    durable
                                    |> Option.iter (fun port ->
                                        port.LinkBlogger(primaryId, id, bloggerEffectiveAgent) |> ignore)

                                    companion.RecordBloggerLinked id

                                    return id
                                | Error error -> return raise (InvalidOperationException error)
                            with ex ->
                                bloggerFailed <- true
                                bloggerId <- None
                                return raise ex
                        }

                    bloggerTask <- Some task
                    task)

    member private this.BloggerDeps: CompanionHostBlogger.BloggerDeps =
        { Sessions = sessions
          PrimaryId = primaryId
          Durable = durable
          EnsureBlogger = ensureBlogger
          Gate = gate
          Companion = companion
          RequestKind = bloggerRequestKind
          SquashFrameCount = bloggerSquashFrameCount
          Journal = journal
          EffectiveAgent = bloggerEffectiveAgent
          RecordSquashPlan = fun bloggerId providerRun -> this.RecordSquashPlan bloggerId providerRun
          StageBloggerContext = fun bloggerId ctx -> this.StageBloggerContext bloggerId ctx }

    /// CTX-006 step 5: the squash attempt's plan hook on the Y chain.
    ///
    /// The default here is a no-op because `Plugin.fs` is the only owner of the
    /// `PluginRuntimeScope`; the composition root rebinds this when it constructs
    /// the CompanionHost so the squash attempt lands in `scope.AttemptPlans` like
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
            bloggerTask <- None
            bloggerId <- None
            bloggerFailed <- true
            companion.RecordBloggerClosed())

    /// CTX-006 / FALLBACK-012: arm this Companion's next recovery slot.
    member this.ArmRecoverySlot() = companion.ArmRecoverySlot()

    member this.IsRecoveryArmed: bool = companion.IsRecoveryArmed

    member this.DisarmRecoverySlot() = companion.DisarmRecoverySlot()

    /// CTX-006: primary session fallback cursor Offset (durable, not cached).
    member this.BloggerCursorOffset() : byte =
        match journal with
        | Some j ->
            match DurableFallback.tryCurrentState primaryId (AgentJournal.snapshot j) with
            | Some current -> current.Cursor.Offset
            | None -> 0uy
        | None -> 0uy

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
            let taskOpt = lock gate (fun () -> bloggerTask)

            match taskOpt with
            | Some task ->
                let! childId = task
                let! aborted = sessions.AbortSession(childId)

                match aborted with
                | Ok() -> ()
                | Error error -> raise (InvalidOperationException error)

                durable |> Option.iter (fun port -> port.CloseBlogger primaryId |> ignore)
                companion.RecordBloggerClosed()
            | None -> ()
        }

    interface IDisposable with
        member this.Dispose() =
            // C6: cancel in-memory child cache even if CloseBloggerAsync races.
            this.InvalidateBloggerCache()

            if bloggerTask.IsSome || bloggerId.IsSome then
                this.CloseBloggerAsync() |> ignore
