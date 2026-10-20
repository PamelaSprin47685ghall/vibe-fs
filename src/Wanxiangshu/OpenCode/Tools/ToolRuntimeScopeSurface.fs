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
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Process
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Participant.Provider.Attempt

type private DummySessionHostPort() =
    let terminals =
        Dictionary<SessionId, ResizeArray<SessionId -> TerminalOutcome -> unit>>()

    let subscribe childId listener =
        if not (terminals.ContainsKey childId) then
            terminals.[childId] <- ResizeArray()

        terminals.[childId].Add listener

        { new IDisposable with
            member _.Dispose() =
                terminals.[childId].Remove listener |> ignore }

    member _.CaptureTerminal(childId: SessionId) =
        let listeners = terminals.[childId] |> Seq.toArray

        fun outcome ->
            for listener in listeners do
                listener childId outcome

    interface ISessionHostPort with
        member _.SubscribeTerminal(childId, listener) = subscribe childId listener

        member _.SubscribeFutureTerminal(childId, listener) = subscribe childId listener

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
    // DSL-MUTABLE: resource — count controlled backend termination receipts
    let mutable terminations = 0
    // DSL-MUTABLE: resource — count controlled backend spawn receipts
    let mutable spawns = 0

    member _.Terminations = terminations
    member _.Spawns = spawns

    member _.Create() =
        // DSL-MUTABLE: resource — controlled backend completion port
        let mutable portRef: PtyPort option = None

        let handler id command =
            let port =
                portRef
                |> Option.defaultWith (fun () -> invalidOp "PTY fixture is not initialized")

            match command with
            | PtyCommand.Spawn(label, _, _) ->
                spawns <- spawns + 1
                sessions.[label] <- (port, id)
            | PtyCommand.Signal PtySignal.Terminate ->
                terminations <- terminations + 1
                port.Complete id
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
    static member private recordChildDelivery(child: SessionId, provider: string, text: string) =
        Wanxiangshu.Repository.Programming.Js.JsDeliveredValues.recordFencedView
            (SessionId.value child)
            (ProviderRunIdentity.create provider)
            (ToolCallId.create (provider + "-delivery"))
            (Some(createObj [ "prefixUntil" ==> 0; "thenMap" ==> createObj [ "0" ==> text ] ]))

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
            let! stateDirectory = RuntimePath.stateRoot workspace

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
                    ownershipStateRoot = stateDirectory
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

            for session in [ devopsChildSessionId; engineerChildSessionId ] do
                Wanxiangshu.Repository.Programming.Js.JsDeliveredValues.clear session
                Wanxiangshu.Repository.Programming.Js.JsDeliveredValues.clearReturned session

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

            let sessionHost = DummySessionHostPort()
            let dummySessions = sessionHost :> ISessionHostPort
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

            let result =
                { SessionId = SessionId.create devopsChildSessionId
                  Role = Role.DevOps
                  ProviderRun = ProviderRunIdentity.create "prov-run"
                  AuthorityRootUserMessageId = authRootDevops
                  Directory = None
                  TerminalText = "devops finished"
                  TurnFormalText = "devops finished" }

            let terminalKind =
                ToolRuntimeScopeSurface.tryGetProperty<string> (scenario, "terminalKind")
                |> Option.defaultValue "Completed"

            let outcome =
                match terminalKind with
                | "Aborted" -> TerminalOutcome.Aborted(TerminalStop.session "retry")
                | "Missing" -> TerminalOutcome.Failed(TerminalStop.session "MISSING_FINAL_REPORT")
                | "Failed" -> TerminalOutcome.Failed(TerminalStop.session "failed")
                | "Empty" ->
                    TerminalOutcome.Completed
                        { result with
                            TerminalText = ""
                            TurnFormalText = "" }
                | "ForeignRoot" ->
                    TerminalOutcome.Completed
                        { result with
                            AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "old-root" }
                | "ForeignChild" ->
                    TerminalOutcome.Completed
                        { result with
                            SessionId = SessionId.create "other-child" }
                | _ -> TerminalOutcome.Completed result

            let emitDevops = sessionHost.CaptureTerminal devopsRun.ChildId

            if completeRole = "devops" then
                if terminalKind = "Completed" then
                    ToolRuntimeScopeSurface.recordChildDelivery (devopsRun.ChildId, "prov-run", "devops finished")

                emitDevops outcome
                emitDevops outcome
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

                ToolRuntimeScopeSurface.recordChildDelivery (engRun.ChildId, "prov-run-eng", "eng finished")
                sessionHost.CaptureTerminal engRun.ChildId engOutcome

            do! mgrRuntime.AwaitObservedWork()

            let devopsAfter = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
            let engineerAfter = engRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
            let finishedAfterFirst = devopsRun.Finished
            let terminationsAfterFirst = ptys.Terminations

            let followWithValid =
                ToolRuntimeScopeSurface.tryGetProperty<bool> (scenario, "followWithValid")
                |> Option.defaultValue false

            if followWithValid then
                ToolRuntimeScopeSurface.recordChildDelivery (devopsRun.ChildId, "prov-run", "devops finished")
                emitDevops (TerminalOutcome.Completed result)
                do! mgrRuntime.AwaitObservedWork()

            let lateTerminal =
                ToolRuntimeScopeSurface.tryGetProperty<bool> (scenario, "lateTerminal")
                |> Option.defaultValue false

            let oldCapability =
                devopsRuntime.PtyCapabilityForRun(
                    AdmittedWork.logicalRunId devopsRun.Work.Value,
                    ProviderRunIdentity.create "old-provider",
                    ToolCallId.create "late-old-call"
                )

            let! lateProof =
                if lateTerminal then
                    task {
                        let admitNext =
                            ToolRuntimeScopeSurface.tryGetProperty<unit -> Task<unit>> (scenario, "admitNextRound")
                            |> Option.defaultWith (fun () -> invalidOp "Next-round proof requires canonical admission")

                        do! admitNext ()
                        let! opened = devopsRuntime.ForkPty("next-run-pty", ManagedAgent.make Role.DevOps)
                        let id = opened |> Result.defaultWith failwith

                        devopsRuntime.TryBindTerminalName("next-round", id)
                        |> Result.defaultWith failwith

                        let! lateOpen = oldCapability.ForkPty("late-old-round", ManagedAgent.make Role.DevOps, None)
                        let! lateSend = oldCapability.SendPty(id, "must-not-write", None)
                        emitDevops (TerminalOutcome.Completed result)
                        do! mgrRuntime.AwaitObservedWork()

                        return
                            Some
                                {| forked = Result.isOk lateOpen
                                   sent = Result.isOk lateSend
                                   seesNext = oldCapability.TryPty(id.Value).IsSome
                                   seesNextName = oldCapability.TryPtyByName("next-round").IsSome |}
                    }
                else
                    Task.FromResult None

            let devopsAfterRetry = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
            let terminationsAfterRetry = ptys.Terminations
            let finishedAfterRetry = devopsRun.Finished
            do! scope.DisposeAsync()

            return
                box
                    {| devopsBefore = devopsBefore |> List.toArray
                       devopsAfter = devopsAfter
                       finishedAfterFirst = finishedAfterFirst
                       finishedAfterRetry = finishedAfterRetry
                       terminationsAfterFirst = terminationsAfterFirst
                       terminationsAfterRetry = terminationsAfterRetry
                       devopsAfterRetry = devopsAfterRetry
                       lateProof = lateProof
                       engineerBefore = engineerBefore |> List.toArray
                       engineerAfter = engineerAfter |}
        }

    [<CompiledName("startPtyExecution")>]
    static member startPtyExecution(journal: JournalHandle, child: string, physical: string, provider: string) =
        task {
            let childId = SessionId.create child

            let profile =
                PromptAuthorityProjectionQueries.activeProfile
                    childId
                    (AgentJournal.snapshot journal.Journal).AgentProjections
                |> Option.defaultWith (fun () -> invalidOp "PTY fixture requires canonical authority")

            let evidence =
                ManagedChatAcceptance.evidenceFromIntent
                    profile
                    (PhysicalUserMessageId.create physical)
                    (PromptOrigin.AuthorityRoot profile.AuthorityKind)

            let key: ChatExecutionKey =
                { SessionId = childId
                  PhysicalUserMessageId = evidence.PhysicalUserMessageId }

            let! accepted = Wanxiangshu.Composition.Durable.ManagedChatAcceptance.accept journal.Journal key evidence
            accepted |> Result.defaultWith (fun error -> failwithf "%A" error) |> ignore

            let! started =
                Wanxiangshu.Composition.Durable.ManagedChatProviderLifecycle.providerStarted
                    journal.Journal
                    key
                    evidence
                    (ProviderRunIdentity.create provider)
                    ProviderRequestKind.WorkMain

            started |> Result.defaultWith (fun error -> failwithf "%A" error) |> ignore

            ModelRouting.rememberProviderStepIdentity
                childId
                evidence.PhysicalUserMessageId
                (ProviderRunIdentity.create provider)
        }

    [<CompiledName("ptyAdmissionProbe")>]
    static member ptyAdmissionProbe
        (journal: JournalHandle, workspace: string, child: string, realBackend: bool, stateDirectory: string)
        : obj =
        let childId = SessionId.create child

        let profile =
            AgentJournal.snapshot journal.Journal
            |> fun snapshot -> Map.find childId snapshot.AgentProjections.Sessions
            |> fun session -> session.PromptAuthority.Value.ActiveLogicalRun.Value

        let owner, _, _ = PromptIdentitySeed.owner profile.IdentitySeed |> Option.get
        let sessions = DummySessionHostPort()
        let ptys = ControlledPtyPorts()

        let scope =
            new ToolRuntimeScope(
                sessions,
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
                ownershipStateRoot = stateDirectory,
                ptyPortFactory = (if realBackend then PtyBackend.createPort else ptys.Create)
            )

        let context sid =
            { SessionId = sid
              Agent = None
              ToolCallId = None
              ProviderRunId = None
              PromptText = None
              AttachAbort = fun _ -> fun () -> () }

        let childRuntime = scope.RuntimeFor(context child) |> Result.defaultWith failwith

        let manager =
            scope.RuntimeFor(context (SessionId.value owner)) |> Result.defaultWith failwith

        let binding =
            (AgentJournal.snapshot journal.Journal)
                .AgentProjections.HandleByChildSession.[childId]

        let run =
            manager.InstallRun(
                binding.Handle |> HandleId.tryAgent |> Option.get |> AgentHandleId.value,
                childId,
                Role.DevOps,
                profile.AuthorityRootUserMessageId
            )

        let capabilityFor provider call =
            let context =
                { context child with
                    ProviderRunId = Some(ProviderRunIdentity.create provider)
                    ToolCallId = Some(ToolCallId.create call) }

            if realBackend then
                Ok(
                    childRuntime.PtyCapabilityForRun(
                        profile.LogicalRunId,
                        context.ProviderRunId.Value,
                        context.ToolCallId.Value
                    )
                )
            else
                scope.PtyCapabilityFor context

        let forkPty capability command =
            task {
                let! result =
                    match capability with
                    | Ok capability -> capability.ForkPty(command, ManagedAgent.make Role.DevOps, None)
                    | Error error -> Task.FromResult(Error error)

                return
                    match result with
                    | Ok id ->
                        box
                            {| ok = true
                               id = id.Value
                               error = "" |}
                    | Error error -> box {| ok = false; id = ""; error = error |}
            }

        let openPty provider command =
            forkPty (capabilityFor provider command) command

        let capturedCapability = capabilityFor "pty-admission-provider" "captured-tool-call"

        let stop () =
            task {
                ToolRuntimeScopeSurface.recordChildDelivery (childId, "pty-admission-provider", "finished")

                sessions.CaptureTerminal
                    childId
                    (TerminalOutcome.Completed
                        { SessionId = childId
                          Role = Role.DevOps
                          ProviderRun = ProviderRunIdentity.create "pty-admission-provider"
                          AuthorityRootUserMessageId = profile.AuthorityRootUserMessageId
                          Directory = None
                          TerminalText = "finished"
                          TurnFormalText = "finished" })

                let! failure =
                    task {
                        try
                            do! manager.AwaitObservedWork()
                            return None
                        with error ->
                            return Some error.Message
                    }

                return
                    box
                        {| finished = run.Finished
                           failure = failure |}
            }

        createObj
            [ "open", box (openPty "pty-admission-provider")
              "openCaptured", box (forkPty capturedCapability)
              "openUnknown", box (openPty "unobserved-provider")
              "openLater",
              box (fun command ->
                  let session, physical =
                      ModelRouting.tryProviderStepIdentity (ProviderRunIdentity.create "pty-admission-provider")
                      |> Option.get

                  ModelRouting.rememberProviderStepIdentity
                      session
                      physical
                      (ProviderRunIdentity.create "pty-later-provider")

                  openPty "pty-later-provider" command)
              "stop", box stop
              "complete", box ptys.Complete
              "close", box (fun id -> childRuntime.ClosePty(PtyId.Create id))
              "detachChild", box (fun () -> childRuntime.DetachAndDrain())
              "spawns", box (fun () -> ptys.Spawns)
              "blockers", box (fun () -> childRuntime.SnapshotOutstandingPtyRuns() |> List.toArray)
              "dispose", box (fun () -> scope.DisposeAsync()) ]

    [<CompiledName("undrainedDevOpsReturn")>]
    static member undrainedDevOpsReturn(journal: JournalHandle) =
        let observeFailure (work: Task) =
            task {
                try
                    do! work
                    return None
                with error ->
                    return Some error.Message
            }

        task {
            let child = SessionId.create "devops-session-1"
            let projection = (AgentJournal.snapshot journal.Journal).AgentProjections

            let profile =
                PromptAuthorityProjectionQueries.activeProfile child projection |> Option.get

            let parent, _, _ = PromptIdentitySeed.owner profile.IdentitySeed |> Option.get
            let binding = projection.HandleByChildSession.[child]
            let sessions = DummySessionHostPort()

            let runtime =
                HostForkRuntime(
                    parent,
                    sessions,
                    CompletionMailboxRuntime.create,
                    NodeTiming.nodeClockPort (),
                    NodeTiming.raceExit,
                    ptyPort = None,
                    recoverPty = PtyBackend.recover,
                    journal = journal.Journal
                )

            let run =
                runtime.InstallRun(
                    binding.Handle |> HandleId.tryAgent |> Option.get |> AgentHandleId.value,
                    child,
                    Role.DevOps,
                    profile.AuthorityRootUserMessageId
                )

            sessions.CaptureTerminal
                child
                (TerminalOutcome.Completed
                    { SessionId = child
                      Role = Role.DevOps
                      ProviderRun = ProviderRunIdentity.create "undrained-provider"
                      AuthorityRootUserMessageId = profile.AuthorityRootUserMessageId
                      Directory = None
                      TerminalText = "finished"
                      TurnFormalText = "finished" })

            let! failure = observeFailure (runtime.AwaitObservedWork())
            let finished = run.Finished
            let! _ = observeFailure (runtime.DetachAndDrain())

            return
                box
                    {| finished = finished
                       failure = failure |}
        }

    [<CompiledName("coldDevOpsReturn")>]
    static member coldDevOpsReturn(journal: JournalHandle, workspace: string) : Task<obj> =
        let observeFailure (work: Task) =
            task {
                try
                    do! work
                    return None
                with error ->
                    return Some error.Message
            }

        task {
            let! stateDirectory = RuntimePath.stateRoot workspace
            let sessions = DummySessionHostPort()

            let scope =
                new ToolRuntimeScope(
                    sessions,
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
                    ownershipStateRoot = stateDirectory
                )

            let manager =
                scope.RuntimeFor
                    { SessionId = "manager-road-1"
                      Agent = None
                      ToolCallId = None
                      ProviderRunId = None
                      PromptText = None
                      AttachAbort = fun _ -> fun () -> () }
                |> Result.defaultWith failwith

            let root =
                ToolRuntimeScopeSurface.childAuthorityRoot (journal, "devops-session-1", Role.DevOps)

            let childId = SessionId.create "devops-session-1"
            let run = manager.InstallRun("devops", childId, Role.DevOps, root)
            // DSL-MUTABLE: subscription — observe the controlled terminal callback
            let mutable delivered = false

            task {
                let! _ = run.Source.Task
                delivered <- true
            }
            |> ignore

            ToolRuntimeScopeSurface.recordChildDelivery (childId, "cold-provider", "finished")

            sessions.CaptureTerminal
                childId
                (TerminalOutcome.Completed
                    { SessionId = childId
                      AuthorityRootUserMessageId = root
                      ProviderRun = ProviderRunIdentity.create "cold-provider"
                      Role = Role.DevOps
                      Directory = None
                      TerminalText = "finished"
                      TurnFormalText = "finished" })

            let! failure = observeFailure (manager.AwaitObservedWork())
            let finished = run.Finished
            let deliveredBeforeDispose = delivered
            let! _ = observeFailure (scope.DisposeAsync())

            return
                box
                    {| failure = failure
                       finished = finished
                       delivered = deliveredBeforeDispose |}
        }

    [<CompiledName("verifyUnknownProcessDrain")>]
    static member verifyUnknownProcessDrain() : Task<obj> =
        task {
            let scope =
                new ToolRuntimeScope(
                    DummySessionHostPort(),
                    DummyObserver(),
                    { new IRootWorkspaceReader with
                        member _.TryRead() = None },
                    None,
                    None,
                    Dictionary<string, string>(),
                    (fun _ -> None),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None
                )

            let physicalClose = TaskCompletionSource<unit>()
            let programSettled = TaskCompletionSource<unit>()
            // DSL-MUTABLE: cancellation — record the owned program's abort callback.
            let mutable cancelled = false

            let detach =
                scope.AttachProcessAbort(
                    "unknown-pipe",
                    (fun () ->
                        cancelled <- true
                        programSettled.SetResult()),
                    programSettled.Task :> Task
                )

            let admitted =
                scope.RunOwnedProcess("unknown-pipe", (fun () -> physicalClose.Task :> Task))

            let before = scope.RetirementBlockersFor "unknown-pipe" |> List.toArray

            let! failure =
                task {
                    try
                        do! scope.DisposeAsync()
                        return None
                    with error ->
                        return Some error.Message
                }

            let afterUnknown = scope.RetirementBlockersFor "unknown-pipe" |> List.toArray
            // DSL-MUTABLE: resource — count attempted starts after scope closure.
            let mutable lateStarts = 0

            let lateAdmitted =
                scope.RunOwnedProcess(
                    "unknown-pipe",
                    fun () ->
                        lateStarts <- lateStarts + 1
                        Task.FromResult() :> Task
                )

            physicalClose.SetResult()
            do! emitJsExpr () "new Promise(resolve => setImmediate(resolve))"
            let afterClose = scope.RetirementBlockersFor "unknown-pipe" |> List.toArray
            detach ()

            return
                createObj
                    [ "admitted" ==> admitted
                      "cancelled" ==> cancelled
                      "failure" ==> (failure |> Option.map box |> Option.defaultValue null)
                      "before" ==> before
                      "afterUnknown" ==> afterUnknown
                      "afterClose" ==> afterClose
                      "lateAdmitted" ==> lateAdmitted
                      "lateStarts" ==> lateStarts ]
        }

    [<CompiledName("verifyProcessRetirementContract")>]
    static member verifyProcessRetirementContract() : Task<obj> =
        task {
            let parents = Dictionary<string, string>()
            parents.Add("process-devops", "process-manager")
            parents.Add("process-engineer", "process-manager")

            let scope =
                new ToolRuntimeScope(
                    DummySessionHostPort(),
                    DummyObserver(),
                    { new IRootWorkspaceReader with
                        member _.TryRead() = None },
                    None,
                    None,
                    parents,
                    (fun _ -> None),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None
                )

            let context: HostToolContext =
                { SessionId = "process-manager"
                  Agent = None
                  ToolCallId = None
                  ProviderRunId = None
                  PromptText = None
                  AttachAbort = fun _ -> ignore }

            let manager = scope.RuntimeFor context |> Result.defaultWith failwith
            manager.AdoptChild("devops", SessionId.create "process-devops")
            let engineer = TaskCompletionSource<unit>()
            let devops = TaskCompletionSource<unit>()

            let admittedEngineer =
                scope.RunOwnedProcess("process-engineer", (fun () -> engineer.Task :> Task))

            let admittedDevOps =
                scope.RunOwnedProcess("process-devops", (fun () -> devops.Task :> Task))

            let before = scope.RetirementBlockersFor "process-manager" |> List.toArray
            engineer.SetResult()
            do! emitJsExpr () "new Promise(resolve => setImmediate(resolve))"
            let afterEngineer = scope.RetirementBlockersFor "process-manager" |> List.toArray
            let devopsOwn = scope.RetirementBlockersFor "process-devops" |> List.toArray
            devops.SetResult()
            do! emitJsExpr () "new Promise(resolve => setImmediate(resolve))"
            do! scope.DisposeAsync()

            return
                createObj
                    [ "admittedEngineer" ==> admittedEngineer
                      "admittedDevOps" ==> admittedDevOps
                      "before" ==> before
                      "afterEngineer" ==> afterEngineer
                      "devopsOwn" ==> devopsOwn ]
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
            let! frozenWhileDraining = scope.IsRetirementFrozen managerSessionId
            scope.UnfreezeRetirement managerSessionId
            let! frozenAfterUnfreeze = scope.IsRetirementFrozen managerSessionId

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
