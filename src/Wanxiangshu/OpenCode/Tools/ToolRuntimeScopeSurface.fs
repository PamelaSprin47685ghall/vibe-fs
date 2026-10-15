namespace Wanxiangshu.OpenCode.Tools

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Process

type private DummySessionHostPort() =
    interface ISessionHostPort with
        member _.SubscribeTerminal(_, _) =
            { new IDisposable with
                member _.Dispose() = () }

        member _.SubscribeFutureTerminal(_, _) =
            { new IDisposable with
                member _.Dispose() = () }

        member _.SendPrompt(_, _, _) =
            Task.FromResult(SendOutcome.Fatal "dummy")

        member _.AbortSession _ = Task.FromResult(Ok())
        member _.InterruptAttempt _ = Task.FromResult(Ok())
        member _.IsManagedChild _ = true
        member _.AbortChildren _ = Task.FromResult()
        member _.CreateSiblingSession(_, _, _) = Task.FromResult(Error "dummy")
        member _.TryGetParentSession _ = Task.FromResult(Ok None)
        member _.CreateChildSession(_, _) = Task.FromResult(Error "dummy")
        member _.ListChildren _ = Task.FromResult(Ok [])
        member _.FamilyRootOf id = id

type private DummyObserver() =
    interface IWaitObserver with
        member _.Enter _ =
            { new IWaitLease with
                member _.MarkExit _ = ()
                member _.Dispose() = () }

type private ControlledPtyPorts() =
    let sessions = Dictionary<string, PtyPort * PtyId>()

    member _.Create() =
        let mutable portRef: PtyPort option = None

        let handler id command =
            let port =
                portRef
                |> Option.defaultWith (fun () -> invalidOp "PTY fixture is not initialized")

            match command with
            | PtyCommand.Spawn(label, _) -> sessions.[label] <- (port, id)
            | PtyCommand.Signal PtySignal.Terminate -> port.Complete id
            | _ -> ()

            Task.FromResult(Ok())

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port

    member _.Complete(label: string) =
        let port, id = sessions.[label]
        port.Complete id

    member _.Id(label: string) =
        let _, id = sessions.[label]
        id.Value

[<AbstractClass; Sealed; AttachMembers>]
type ToolRuntimeScopeSurface =
    [<Emit("$0 === undefined")>]
    static member private isUndefined(v: obj) : bool = jsNative

    static member private isNullOrUndefined(v: obj) : bool =
        isNull v || ToolRuntimeScopeSurface.isUndefined v

    static member private tryGetProperty<'T>(target: obj, prop: string) : 'T option =
        if isNull target then
            None
        elif ToolRuntimeScopeSurface.isNullOrUndefined (target?(prop)) then
            None
        else
            Some(unbox<'T> (target?(prop)))

    static member private childAuthorityRoot(journal: JournalHandle, child: string, role: Role) =
        let childId = SessionId.create child

        (AgentJournal.snapshot journal.Journal).AgentProjections.Sessions
        |> Map.tryFind childId
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.bind (fun authority -> authority.ActiveLogicalRun)
        |> Option.filter (fun profile ->
            profile.SessionId = childId
            && profile.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
            && profile.CanonicalRole = role)
        |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
        |> Option.defaultWith (fun () -> invalidOp "work requires exact canonical child authority")

    [<CompiledName("evaluateRetirementBlockers")>]
    static member evaluateRetirementBlockers(journal: JournalHandle, scenario: obj) : Task<string array> =
        let managerSessionId =
            ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "managerSessionId")
            |> Option.defaultValue "manager-road-1"

        let devopsChildSessionId =
            ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "devopsChildSessionId")

        let engineerChildSessionId =
            ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "engineerChildSessionId")

        let devopsPtys =
            ToolRuntimeScopeSurface.tryGetProperty<string array> (scenario, "devopsPtys")
            |> Option.defaultValue [||]

        let engineerPtys =
            ToolRuntimeScopeSurface.tryGetProperty<string array> (scenario, "engineerPtys")
            |> Option.defaultValue [||]

        let managerHasDevopsAgent =
            ToolRuntimeScopeSurface.tryGetProperty<bool> (scenario, "managerHasDevopsAgent")
            |> Option.defaultValue false

        let managerHasEngineerAgent =
            ToolRuntimeScopeSurface.tryGetProperty<bool> (scenario, "managerHasEngineerAgent")
            |> Option.defaultValue false

        let sessionParents = Dictionary<string, string>()

        devopsChildSessionId
        |> Option.iter (fun devopsId -> sessionParents.[devopsId] <- managerSessionId)

        engineerChildSessionId
        |> Option.iter (fun engId -> sessionParents.[engId] <- managerSessionId)

        let dummySessions = DummySessionHostPort() :> ISessionHostPort
        let dummyObserver = DummyObserver() :> IWaitObserver
        let ptys = ControlledPtyPorts()

        let scope =
            new ToolRuntimeScope(
                dummySessions,
                dummyObserver,
                { new IRootWorkspaceReader with
                    member _.TryRead() = None },
                Some journal.Journal,
                None,
                sessionParents,
                (fun _ -> None),
                Dictionary<string, string>(),
                None,
                None,
                None,
                None,
                None,
                ptyPortFactory = ptys.Create
            )

        let emptyContext sid =
            { SessionId = sid
              Agent = None
              ToolCallId = None
              ProviderRunId = None
              PromptText = None
              AttachAbort = fun _ -> fun () -> () }

        let mgrRuntime =
            match scope.RuntimeFor(emptyContext managerSessionId) with
            | Ok r -> r
            | Error err -> failwith err

        devopsChildSessionId
        |> Option.iter (fun devopsId ->
            mgrRuntime.AdoptChild("devops", SessionId.create devopsId)

            if managerHasDevopsAgent then
                let authRoot =
                    ToolRuntimeScopeSurface.childAuthorityRoot (journal, devopsId, Role.DevOps)

                mgrRuntime.InstallRun("devops", SessionId.create devopsId, Role.DevOps, authRoot)
                |> ignore)

        engineerChildSessionId
        |> Option.iter (fun engId ->
            mgrRuntime.AdoptChild("engineer-1", SessionId.create engId)

            if managerHasEngineerAgent then
                let authRoot =
                    ToolRuntimeScopeSurface.childAuthorityRoot (journal, engId, Role.Engineer)

                mgrRuntime.InstallRun("engineer-1", SessionId.create engId, Role.Engineer, authRoot)
                |> ignore)

        task {
            for devopsId in Option.toList devopsChildSessionId do
                let devopsRuntime =
                    scope.RuntimeFor(emptyContext devopsId) |> Result.defaultWith failwith

                for command in devopsPtys do
                    let! opened = devopsRuntime.ForkPty(command, ManagedAgent.make Role.DevOps)
                    opened |> Result.defaultWith failwith |> ignore

            for engId in Option.toList engineerChildSessionId do
                let engRuntime = scope.RuntimeFor(emptyContext engId) |> Result.defaultWith failwith

                for command in engineerPtys do
                    let! opened = engRuntime.ForkPty(command, ManagedAgent.make Role.Engineer)
                    opened |> Result.defaultWith failwith |> ignore

            let blockers = scope.RetirementBlockersFor managerSessionId |> List.toArray
            do! scope.DisposeAsync()
            return blockers
        }

    [<CompiledName("coldRetirementBlockers")>]
    static member coldRetirementBlockers
        (journal: JournalHandle, workspace: string, managerSessionId: string)
        : Task<string array> =
        task {
            let scope =
                new ToolRuntimeScope(
                    DummySessionHostPort(),
                    DummyObserver(),
                    { new IRootWorkspaceReader with
                        member _.TryRead() = None },
                    Some journal.Journal,
                    Some workspace,
                    Dictionary<string, string>(),
                    (fun _ -> None),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None,
                    None
                )

            let blockers = scope.RetirementBlockersFor managerSessionId |> List.toArray
            do! scope.DisposeAsync()
            return blockers
        }

    [<CompiledName("verifyDevOpsReturnDrain")>]
    static member verifyDevOpsReturnDrain(journal: JournalHandle, scenario: obj) : Task<obj> =
        task {
            let managerSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "managerSessionId")
                |> Option.defaultValue "manager-road-1"

            let devopsChildSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "devopsChildSessionId")
                |> Option.defaultValue "devops-session-1"

            let engineerChildSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "engineerChildSessionId")
                |> Option.defaultValue "engineer-session-1"

            let devopsPtys =
                ToolRuntimeScopeSurface.tryGetProperty<string array> (scenario, "devopsPtys")
                |> Option.defaultValue [| "devops-pty-1" |]

            let engineerPtys =
                ToolRuntimeScopeSurface.tryGetProperty<string array> (scenario, "engineerPtys")
                |> Option.defaultValue [| "eng-pty-1" |]

            let completeRole =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "completeRole")
                |> Option.defaultValue "devops"

            let sessionParents = Dictionary<string, string>()
            sessionParents.[devopsChildSessionId] <- managerSessionId
            sessionParents.[engineerChildSessionId] <- managerSessionId

            let dummySessions = DummySessionHostPort() :> ISessionHostPort
            let dummyObserver = DummyObserver() :> IWaitObserver
            let ptys = ControlledPtyPorts()

            let scope =
                new ToolRuntimeScope(
                    dummySessions,
                    dummyObserver,
                    { new IRootWorkspaceReader with
                        member _.TryRead() = None },
                    Some journal.Journal,
                    None,
                    sessionParents,
                    (fun _ -> None),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None,
                    None,
                    ptyPortFactory = ptys.Create
                )

            let emptyContext sid =
                { SessionId = sid
                  Agent = None
                  ToolCallId = None
                  ProviderRunId = None
                  PromptText = None
                  AttachAbort = fun _ -> fun () -> () }

            let mgrRuntime =
                match scope.RuntimeFor(emptyContext managerSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            let devopsRuntime =
                match scope.RuntimeFor(emptyContext devopsChildSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            let engRuntime =
                match scope.RuntimeFor(emptyContext engineerChildSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            for pty in devopsPtys do
                let! opened = devopsRuntime.ForkPty(pty, ManagedAgent.make Role.DevOps)
                opened |> Result.defaultWith failwith |> ignore

            for pty in engineerPtys do
                let! opened = engRuntime.ForkPty(pty, ManagedAgent.make Role.Engineer)
                opened |> Result.defaultWith failwith |> ignore

            let devopsBefore = devopsRuntime.SnapshotOutstandingPtyRuns()
            let engineerBefore = engRuntime.SnapshotOutstandingPtyRuns()

            mgrRuntime.AdoptChild("devops", SessionId.create devopsChildSessionId)
            mgrRuntime.AdoptChild("engineer-1", SessionId.create engineerChildSessionId)

            let authRootDevops =
                ToolRuntimeScopeSurface.childAuthorityRoot (journal, devopsChildSessionId, Role.DevOps)

            let devopsRun =
                mgrRuntime.InstallRun("devops", SessionId.create devopsChildSessionId, Role.DevOps, authRootDevops)

            let authRootEng =
                ToolRuntimeScopeSurface.childAuthorityRoot (journal, engineerChildSessionId, Role.Engineer)

            let engRun =
                mgrRuntime.InstallRun("engineer-1", SessionId.create engineerChildSessionId, Role.Engineer, authRootEng)

            let outcome =
                TerminalOutcome.Completed
                    { SessionId = SessionId.create devopsChildSessionId
                      Role = Role.DevOps
                      ProviderRun = ProviderRunIdentity.create "prov-run"
                      AuthorityRootUserMessageId = authRootDevops
                      Directory = None
                      TerminalText = "devops finished"
                      TurnFormalText = "devops finished" }

            if completeRole = "devops" then
                mgrRuntime.Complete(devopsRun, outcome)
            elif completeRole = "engineer" then
                let engOutcome =
                    TerminalOutcome.Completed
                        { SessionId = SessionId.create engineerChildSessionId
                          Role = Role.Engineer
                          ProviderRun = ProviderRunIdentity.create "prov-run-eng"
                          AuthorityRootUserMessageId = authRootEng
                          Directory = None
                          TerminalText = "eng finished"
                          TurnFormalText = "eng finished" }

                mgrRuntime.Complete(engRun, engOutcome)

            do! mgrRuntime.DrainOwnedWork()

            let devopsAfter = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
            let engineerAfter = engRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
            do! scope.DisposeAsync()

            return
                box
                    {| devopsBefore = devopsBefore |> List.toArray
                       devopsAfter = devopsAfter
                       engineerBefore = engineerBefore |> List.toArray
                       engineerAfter = engineerAfter |}
        }

    [<CompiledName("verifyOwnershipRetirementContract")>]
    static member verifyOwnershipRetirementContract(scenario: obj) : Task<obj> =
        task {
            let managerSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "managerSessionId")
                |> Option.defaultValue "manager-road-1"

            let incumbentId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "incumbentId")
                |> Option.defaultValue "tenure-1"

            let devopsSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "devopsSessionId")
                |> Option.defaultValue "devops-session-1"

            let engineerSessionId =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "engineerSessionId")
                |> Option.defaultValue "engineer-session-1"

            let sessionParents = Dictionary<string, string>()
            sessionParents.[devopsSessionId] <- managerSessionId
            sessionParents.[engineerSessionId] <- managerSessionId

            let dummySessions = DummySessionHostPort() :> ISessionHostPort
            let dummyObserver = DummyObserver() :> IWaitObserver
            let ptys = ControlledPtyPorts()

            let scope =
                new ToolRuntimeScope(
                    dummySessions,
                    dummyObserver,
                    { new IRootWorkspaceReader with
                        member _.TryRead() = None },
                    None,
                    None,
                    sessionParents,
                    (fun _ -> None),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None,
                    None,
                    ptyPortFactory = ptys.Create
                )

            let emptyContext sid =
                { SessionId = sid
                  Agent = None
                  ToolCallId = None
                  ProviderRunId = None
                  PromptText = None
                  AttachAbort = fun _ -> fun () -> () }

            let mgrRuntime =
                match scope.RuntimeFor(emptyContext managerSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            let devopsRuntime =
                match scope.RuntimeFor(emptyContext devopsSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            let engRuntime =
                match scope.RuntimeFor(emptyContext engineerSessionId) with
                | Ok r -> r
                | Error err -> failwith err

            mgrRuntime.AdoptChild("devops", SessionId.create devopsSessionId)
            mgrRuntime.AdoptChild("engineer-1", SessionId.create engineerSessionId)

            let! devopsOpened = devopsRuntime.ForkPty("devops-pty-1", ManagedAgent.make Role.DevOps)
            devopsOpened |> Result.defaultWith failwith |> ignore
            let! engineerOpened = engRuntime.ForkPty("eng-pty-1", ManagedAgent.make Role.Engineer)
            engineerOpened |> Result.defaultWith failwith |> ignore

            let workWaiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            let admittedWork = scope.RunOwnedWork(fun () -> workWaiter.Task :> Task)

            let blockersWithWorkAndPty =
                scope.RetirementBlockersFor managerSessionId |> List.toArray

            // Cancellation alone does not complete the controlled backend.
            engRuntime.CancelOwnedPtys()

            let blockersAfterCancelRequest =
                scope.RetirementBlockersFor managerSessionId |> List.toArray

            // Freeze retirement via Ownership tenure Drain: frozen while active, cleared on Unfreeze.
            let frozeFirst = scope.TryFreezeRetirement(managerSessionId, incumbentId)
            let frozenWhileDraining = scope.IsRetirementFrozen managerSessionId
            scope.UnfreezeRetirement managerSessionId
            let frozenAfterUnfreeze = scope.IsRetirementFrozen managerSessionId

            // Settle the owned-work callback: its Ended removes the tool-owned-work blocker.
            workWaiter.SetResult(())
            do! emitJsExpr () "new Promise(resolve => setImmediate(resolve))"

            let blockersAfterWorkEnded =
                scope.RetirementBlockersFor managerSessionId |> List.toArray

            ptys.Complete "eng-pty-1"

            let blockersAfterEngineerPtyEnded =
                scope.RetirementBlockersFor managerSessionId |> List.toArray

            let devopsPtysSurviving = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray

            do! scope.DisposeAsync()

            return
                box
                    {| admittedWork = admittedWork
                       blockersWithWorkAndPty = blockersWithWorkAndPty
                       blockersAfterCancelRequest = blockersAfterCancelRequest
                       frozeFirst = frozeFirst
                       frozenWhileDraining = frozenWhileDraining
                       frozenAfterUnfreeze = frozenAfterUnfreeze
                       blockersAfterWorkEnded = blockersAfterWorkEnded
                       blockersAfterEngineerPtyEnded = blockersAfterEngineerPtyEnded
                       engineerPtyId = ptys.Id "eng-pty-1"
                       devopsPtyId = ptys.Id "devops-pty-1"
                       devopsPtysSurviving = devopsPtysSurviving |}
        }
