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
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
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
                // SatelliteRuntime caches successful leases, but a failed ensure
                // must never become a permanent poisoned single-flight. The next
                // material/retry is allowed to re-observe Host + durable state.
                runtime.Invalidate(primaryId, SatelliteKind.Companion)
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

    let bloggerEffectiveAgent = ManagedAgent.nameOf Role.Blogger

    // DSL-MUTABLE: single-flight — memoized blogger create task
    let mutable bloggerCreateTask: Task<SessionId> option = None
    // DSL-MUTABLE: resource — resolved blogger session id
    let mutable bloggerId: SessionId option = None
    // DSL-MUTABLE: resource — create-failure latch, cleared on retry
    let mutable bloggerCreateFailed = false
    // Constructor seed is used only when no live durable projection is available.
    // Journal-backed ownership is re-read on every ensure; process cache
    // invalidation must never make an existing association disappear from recovery authority.
    // DSL-MUTABLE: resource — one-shot constructor fallback seed
    let mutable restoredBloggerIdOpt = restoredBloggerId

    let currentRestoredBloggerId () =
        match journal with
        | Some durable ->
            (AgentJournal.snapshot durable).AgentProjections.Sessions
            |> Map.tryFind primaryId
            |> Option.bind (fun session -> session.Companion)
            |> Option.bind (fun state -> state.BloggerSessionId)
            |> Option.map SessionId.value
            |> Option.orElse restoredBloggerIdOpt
        | None -> restoredBloggerIdOpt

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
                        currentRestoredBloggerId
                        (fun () -> bloggerId)
                        (fun sid ->
                            bloggerId <- Some sid
                            bloggerCreateFailed <- false
                            ModelRouting.bindCapacityCompanion primaryId sid
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
          EffectiveAgent = bloggerEffectiveAgent }

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

    member _.Memory = companion.Memory

    /// COMPANION-007: forward the transform-boundary XTrace refresh to the
    /// companion's in-memory mirror.
    member _.RefreshXTrace(state: XTraceProjectionState) = companion.RefreshXTrace(state)

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
