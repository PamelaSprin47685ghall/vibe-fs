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
open Wanxiangshu.Next.Session.AgentRoleHelpers

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
        ?cancelFallbackRetries: SessionId seq -> unit,
        ?cancelSignals: SessionId seq -> unit
    ) as this =
    let runtime = ForkRuntime()
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
    let cancelFallback = defaultArg cancelFallbackRetries (fun _ -> ())
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

        HostForkRunLifecycle.complete gate pendingRuns sessions run outcome workRecord

    member this.InstallRun(agentId: string, childId: SessionId, role: AgentRole) =
        let run =
            HostForkRunLifecycle.installRun gate pendingRuns sessions agentId childId role

        runtime.BindChildSession(agentId, SessionId.value childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) =
        HostForkRunLifecycle.failRun gate pendingRuns sessions run error

    member this.MarkReady(run: PendingHostRun) = HostForkRunLifecycle.markReady gate run

    member this.Cancel() : unit =
        Async.StartImmediate(
            HostForkChildDispatch.cancelParent
                cancelFallback
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
                parentId
                (fun run outcome -> this.Complete(run, outcome))
        )

    member this.Join() : Task<Result<RunCompletion, ForkError>> =
        task {
            do! this.AwaitRecovery()
            return! runtime.Join()
        }

    member _.List() = runtime.List()

    member _.TryFindAgent(agentId: string) =
        runtime.List() |> fst |> List.tryFind (fun a -> a.AgentId = agentId)

    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount
    member _.IsCancelled = runtime.IsCancelled
