namespace Wanxiangshu.Context.Companion

open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Turn
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
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.OpenCode

module private CompanionHostDecisions =

    type EnsureSource =
        | Restore of string
        | CreateFresh

    let linkBlogger
        (durable: ICompanionDurablePort option)
        (recordLinked: SessionId -> unit)
        : SessionId -> SessionId -> string -> Task<Result<unit, string>> =
        fun owner id agent ->
            taskResult {
                match durable with
                | None ->
                    recordLinked id
                    return ()
                | Some port ->
                    do! port.LinkBlogger(owner, id, agent)
                    recordLinked id
            }

    let closeBlogger (durable: ICompanionDurablePort option) : SessionId -> Task<Result<unit, string>> =
        fun owner ->
            match durable with
            | None -> Task.FromResult(Ok())
            | Some port -> port.CloseBlogger owner

    let bloggerCursorOffset (primaryId: SessionId) (journal: AgentJournal option) : AgentPairCursor.FallbackOffset =
        journal
        |> Option.bind (fun j -> FallbackEvidence.tryCurrentState primaryId (AgentJournal.snapshot j))
        |> Option.map (fun current -> current.Cursor.Offset)
        |> Option.defaultValue AgentPairCursor.FallbackOffset.Fork0

    let decideEnsureSource (restoredOpt: string option) (bloggerIdOpt: SessionId option) : EnsureSource =
        match restoredOpt, bloggerIdOpt with
        | Some id, None -> Restore id
        | _ -> CreateFresh

    let executeEnsureSource
        (sessions: ISessionHostPort)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (source: EnsureSource)
        : Task<Result<SessionId, string>> =
        taskResult {
            match source with
            | Restore id -> return SessionId.create id
            | CreateFresh ->
                return!
                    sessions.CreateChildSession(
                        primaryId,
                        { Title = Some agent
                          Agent = Some agent
                          Directory = directory }
                    )
        }

    let finishCreateLink
        (durable: ICompanionDurablePort option)
        (recordLinked: SessionId -> unit)
        (primaryId: SessionId)
        (agent: string)
        (sid: SessionId)
        (onFailed: unit -> unit)
        : Task<SessionId> =
        task {
            let! outcome =
                task {
                    try
                        let! linked = linkBlogger durable recordLinked primaryId sid agent
                        return Result.mapError (fun msg -> InvalidOperationException msg :> exn) linked
                    with ex ->
                        return Error ex
                }

            match outcome with
            | Ok() -> return sid
            | Error ex ->
                onFailed ()
                return raise ex
        }

    let runEnsureSource
        (sessions: ISessionHostPort)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (source: EnsureSource)
        : Task<Result<SessionId, exn>> =
        task {
            try
                let! result = executeEnsureSource sessions primaryId agent directory source
                return Result.mapError (fun msg -> InvalidOperationException msg :> exn) result
            with ex ->
                return Error ex
        }

    let ensureViaSatellite
        (runtime: SatelliteRuntime)
        (durable: ICompanionDurablePort option)
        (recordLinked: SessionId -> unit)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (readRestored: unit -> string option)
        (onEnsured: SessionId -> unit)
        (clearRestoredId: unit -> unit)
        (onFailed: unit -> unit)
        : Task<SessionId> =
        task {
            let! outcome =
                taskResult {
                    let spec =
                        { Kind = SatelliteKind.Companion
                          Agent = agent
                          Title = agent
                          Directory = directory
                          RestoredSessionId = readRestored () |> Option.map SessionId.create
                          Link = linkBlogger durable recordLinked
                          Close = closeBlogger durable }

                    return! runtime.Ensure(primaryId, spec)
                }

            match outcome with
            | Error error ->
                onFailed ()
                return raise (InvalidOperationException error)
            | Ok lease ->
                onEnsured lease.SessionId
                clearRestoredId ()
                return lease.SessionId
        }

    let ensureViaSessions
        (sessions: ISessionHostPort)
        (durable: ICompanionDurablePort option)
        (recordLinked: SessionId -> unit)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (readRestored: unit -> string option)
        (readBloggerId: unit -> SessionId option)
        (onEnsured: SessionId -> unit)
        (clearRestoredId: unit -> unit)
        (onFailed: unit -> unit)
        : Task<SessionId> =
        task {
            let source = decideEnsureSource (readRestored ()) (readBloggerId ())
            let! outcome = runEnsureSource sessions primaryId agent directory source

            match source, outcome with
            | Restore _, Ok sid ->
                onEnsured sid
                clearRestoredId ()
                return sid
            | CreateFresh, Ok sid ->
                onEnsured sid
                return! finishCreateLink durable recordLinked primaryId agent sid onFailed
            | _, Error ex ->
                onFailed ()
                return raise ex
        }

    let startEnsureBlogger
        (satelliteRuntime: SatelliteRuntime option)
        (sessions: ISessionHostPort)
        (durable: ICompanionDurablePort option)
        (recordLinked: SessionId -> unit)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (readRestored: unit -> string option)
        (readBloggerId: unit -> SessionId option)
        (onEnsured: SessionId -> unit)
        (clearRestoredId: unit -> unit)
        (onFailed: unit -> unit)
        : Task<SessionId> =
        match satelliteRuntime with
        | Some runtime ->
            ensureViaSatellite
                runtime
                durable
                recordLinked
                primaryId
                agent
                directory
                readRestored
                onEnsured
                clearRestoredId
                onFailed
        | None ->
            ensureViaSessions
                sessions
                durable
                recordLinked
                primaryId
                agent
                directory
                readRestored
                readBloggerId
                onEnsured
                clearRestoredId
                onFailed

    let retireBlogger
        (satelliteRuntime: SatelliteRuntime option)
        (sessions: ISessionHostPort)
        (durable: ICompanionDurablePort option)
        (primaryId: SessionId)
        (agent: string)
        (directory: string option)
        (childId: SessionId)
        : Task<Result<unit, string>> =
        match satelliteRuntime with
        | Some runtime ->
            taskResult {
                let spec =
                    { Kind = SatelliteKind.Companion
                      Agent = agent
                      Title = agent
                      Directory = directory
                      RestoredSessionId = Some childId
                      Link = fun _ _ _ -> Task.FromResult(Ok())
                      Close = closeBlogger durable }

                do! runtime.Retire(primaryId, spec)
            }
        | None ->
            taskResult {
                do! sessions.AbortSession(childId)
                do! closeBlogger durable primaryId
            }

    let applyCloseResult (recordClosed: unit -> unit) (closed: Result<unit, string>) : unit =
        match closed with
        | Ok() -> recordClosed ()
        | Error error -> raise (InvalidOperationException error)

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
                    CompanionHostDecisions.startEnsureBlogger
                        satelliteRuntime
                        sessions
                        durable
                        companion.RecordBloggerLinked
                        primaryId
                        bloggerEffectiveAgent
                        bloggerDirectory
                        (fun () -> restoredBloggerIdOpt)
                        (fun () -> bloggerId)
                        (fun sid ->
                            bloggerId <- Some sid
                            bloggerCreateFailed <- false
                            bloggerCreated sid)
                        (fun () -> restoredBloggerIdOpt <- None)
                        (fun () ->
                            bloggerCreateFailed <- true
                            bloggerId <- None)

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
        CompanionHostDecisions.bloggerCursorOffset primaryId journal

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
            | None -> ()
            | Some pending ->
                let! childId = pending

                let! closed =
                    CompanionHostDecisions.retireBlogger
                        satelliteRuntime
                        sessions
                        durable
                        primaryId
                        bloggerEffectiveAgent
                        bloggerDirectory
                        childId

                CompanionHostDecisions.applyCloseResult companion.RecordBloggerClosed closed
        }

    interface IDisposable with
        member this.Dispose() =
            // Plugin disposal is not owner deletion: preserve the durable link
            // and Host child so the next plugin instance can prove and reuse it.
            // IDisposable is synchronous, so it must not launch an unobserved
            // retire task that can outlive the journal writer.
            this.InvalidateBloggerCache()
