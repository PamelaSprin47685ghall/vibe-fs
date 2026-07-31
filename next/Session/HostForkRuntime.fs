namespace Wanxiangshu.Next.Session

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.FSharp.Control
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session.AgentRoleIdentity

/// Bridges real child sessions to the existing completion mailbox.
/// Fork / Reuse / Pty operations live in extension files (semantic split).
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        ?journal: AgentJournal,
        ?onChildCreated: string -> AgentRole -> SessionId -> unit,
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?ptyPort: PtyPort,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> AgentRole -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> string option,
        ?childWorkRecordFor: SessionId -> string option,
        ?sessionSnapshot: ISessionSnapshotPort,
        ?cancelSignals: SessionId seq -> unit,
        ?publishToMailbox: bool
    ) as this =
    let runtime = ForkRuntime(publishToMailbox = defaultArg publishToMailbox true)
    let children = Dictionary<string, SessionId>()
    let pendingRuns = Dictionary<string, PendingHostRun>()
    let ptyRuns = HashSet<string>()
    let gate = obj ()

    let directoryOf = defaultArg directoryFor (fun _ -> None)
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let childCreatedDir = defaultArg onChildCreatedDir (fun _ _ _ -> ())
    let runStarted = defaultArg onRunStarted (fun _ _ _ -> ())
    let parentWorkRecordOf = defaultArg parentWorkRecordFor (fun _ -> None)
    let childWorkRecordOf = defaultArg childWorkRecordFor (fun _ -> None)
    let cancelSignals = defaultArg cancelSignals (fun _ -> ())

    let ptyPortInstance = defaultArg ptyPort (PtyBackend.createPort ())
    let parentKey = SessionId.value parentId

    let sendChildPrompt =
        HostForkRunLifecycle.childPromptSender sessions parentId journal directoryOf

    let sendBusyNudge = HostForkBusyNudge.sender sessions parentId journal directoryOf

    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    do
        ptyPortInstance.AddMailboxSender(fun completion ->
            let owned = lock gate (fun () -> ptyRuns.Contains completion.RunId)

            if owned then
                // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                // must not turn another runtime's exit into this runtime's join.
                runtime.PublishCompletion completion
                runtime.UnregisterPty completion.RunId)

    let completedTask () : Task = task { return () } :> Task
    let mutable recoveryTask: Task = completedTask ()

    let restoreChildren () =
        match journal with
        | None -> completedTask ()
        | Some j ->
            HostForkRestart.restoreLinkedChildren
                runtime
                sessionSnapshot
                j
                parentId
                children
                childCreatedDir
                directoryOf

    do recoveryTask <- restoreChildren ()

    member internal _.Runtime = runtime
    member internal _.Children = children
    member internal _.PendingRuns = pendingRuns
    member internal _.PtyRuns = ptyRuns
    member internal _.Gate = gate
    member internal _.Sessions = sessions
    member internal _.Journal = journal
    member internal _.SessionSnapshot = sessionSnapshot
    member internal _.ParentId = parentId
    member internal _.ParentKey = parentKey
    member internal _.PtyPort = ptyPortInstance
    member internal _.DirectoryOf = directoryOf
    member internal _.RunStarted = runStarted
    member internal _.ChildCreated = childCreated
    member internal _.ChildCreatedDir = childCreatedDir
    member internal _.ParentWorkRecordOf = parentWorkRecordOf
    member internal _.ChildWorkRecordOf = childWorkRecordOf
    member internal _.SendChildPrompt = sendChildPrompt
    member internal _.SendBusyNudge = sendBusyNudge
    member internal _.ParentAbortToken = parentAbortToken

    member _.IsRetiredHandle(agentId: string) =
        journal
        |> Option.map (fun durable ->
            HandleProjection.isRetired
                (HandleController.agentHandle agentId)
                (AgentJournal.handleProjection durable parentId))

    member this.AwaitRecovery() =
        task {
            try
                do! recoveryTask
            with _ ->
                ()
        }

    member this.Complete(run: PendingHostRun, outcome: TerminalOutcome) =
        let workRecord =
            match outcome with
            | TerminalOutcome.Completed _ -> AgentCompletion.snapshotOption (childWorkRecordOf run.ChildId)
            | _ -> None

        let completionKind =
            match outcome with
            | TerminalOutcome.Completed _ -> HandleCompletionKind.Terminal
            | TerminalOutcome.Aborted _
            | TerminalOutcome.Failed _ -> HandleCompletionKind.SendFailure

        // Persist the single-assignment handle completion before consuming the
        // pending run.  The Host lifecycle must still release its subscription
        // and source when persistence reports an error, so surface the journal
        // failure only after cleanup rather than silently claiming success.
        let completionResult =
            HandleController.recordCompletion journal parentId run.AgentId completionKind

        HostForkRunLifecycle.complete gate pendingRuns sessions run outcome workRecord

        match completionResult with
        | Ok() -> ()
        | Error error ->
            failwith (sprintf "EXEC-009/PERSIST-002 HandleCompleted append failed: %s" error)

    member this.InstallRun(agentId: string, childId: SessionId, role: AgentRole) =
        let run =
            HostForkRunLifecycle.installRun gate pendingRuns sessions childWorkRecordOf agentId childId role

        runtime.BindChildSession(agentId, childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) =
        HostForkRunLifecycle.failRun gate pendingRuns sessions run error

    member this.MarkReady(run: PendingHostRun) = HostForkRunLifecycle.markReady gate run

    member this.Cancel() : unit =
        Async.StartImmediate(
            HostForkChildDispatch.cancelParent
                cancelSignals
                (fun () -> this.AwaitRecovery())
                runtime
                ptyPortInstance
                parentKey
                parentAbortToken
                gate
                pendingRuns
                children
                sessions
                journal
                (journal |> Option.map (fun durable -> AgentJournal.handleProjection durable parentId))
                parentId
                (fun run outcome -> this.Complete(run, outcome))
        )

    /// EXEC-004: consume any available completion, then retire its handle.
    ///
    /// Retirement is part of consuming, not a later cleanup step. The durable
    /// projection keeps a consumed handle in `CompletedAwaitingJoin` until the
    /// tombstone lands, so a restart between the two would restore it as joinable
    /// and deliver the same completion twice.
    ///
    /// A PTY completion is skipped: `PtyPort` owns that lifecycle (EXEC-015), and
    /// this runtime holds no agent handle for it.
    member this.Join() : Task<Result<RunCompletion, ForkError>> =
        task {
            do! this.AwaitRecovery()
            let! joined = runtime.Join()

            match joined with
            | Ok completion when not (lock gate (fun () -> ptyRuns.Contains completion.RunId)) ->
                match HandleController.retire journal parentId completion.AgentId with
                | Ok() -> return joined
                // A completion that cannot be retired must not be handed out: the
                // caller would treat the work as consumed while the journal still
                // offers it. Reported rather than raised, because `join` is a tool
                // call and its failure belongs in the tool result.
                | Error err -> return Error(ForkError.NotFound(sprintf "join could not retire handle: %s" err))
            | _ -> return joined
        }

    member this.AwaitAgent(agentId: string) : Task<Result<RunCompletion, string>> =
        task {
            do! this.AwaitRecovery()
            return! runtime.AwaitAgent agentId
        }

    member _.List() = runtime.List()

    member _.TryFindAgent(agentId: string) =
        runtime.List() |> fst |> List.tryFind (fun a -> a.AgentId = agentId)

    /// The Host child session a forked agent id drives.
    ///
    /// ORCH-006 needs it right after a fork, to record `ManagerJobCreated`. The map is
    /// the same one restart recovery repopulates from `HandleLinked.ChildSessionId`, so
    /// a resumed job reads the session the Host actually issued rather than one derived
    /// from the agent id.
    member _.TryChildSession(agentId: string) : SessionId option =
        lock gate (fun () ->
            match children.TryGetValue agentId with
            | true, childId -> Some childId
            | false, _ -> None)

    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount
    member _.IsCancelled = runtime.IsCancelled
