namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Text
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module HostForkPtySurface =
    let private createOwnershipProbe (commonDir: string option) (sessionId: string) (port: PtyPort) : obj =
        let runtime =
            HostForkRuntime(
                SessionId.create sessionId,
                Unchecked.defaultof<ISessionHostPort>,
                (fun _ _ _ -> Task.FromResult None),
                CompletionMailboxRuntime.create,
                NodeTiming.nodeClockPort (),
                NodeTiming.raceExit,
                ptyPort = port,
                ?ownershipCommonDir = commonDir
            )

        createObj
            [ "fork"
              ==> (fun command ->
                  task {
                      match! runtime.ForkPty(command, ManagedAgent.make Role.DevOps) with
                      | Ok id -> return createObj [ "ok" ==> true; "id" ==> id.Value ]
                      | Error error -> return createObj [ "ok" ==> false; "error" ==> error ]
                  })
              "close"
              ==> (fun (id: string, grace: int) -> runtime.ClosePty(PtyId.Create id, graceMs = grace))
              "closeAll" ==> (fun grace -> runtime.CloseOwnedPtys(graceMs = grace))
              "detach" ==> (fun () -> runtime.DetachAndDrain())
              "cancel" ==> (fun () -> runtime.CancelAndDrain())
              "outstanding"
              ==> (fun () -> runtime.SnapshotOutstandingPtyRuns() |> List.toArray)
              "owned" ==> (fun id -> runtime.OwnsPty(PtyId.Create id))
              "bind"
              ==> (fun (name, id) -> runtime.TryBindTerminalName(name, PtyId.Create id) |> Result.isOk)
              "complete" ==> (fun id -> port.Complete(PtyId.Create id))
              "registerExit"
              ==> (fun (id, waiting: Task) -> port.RegisterExitTask(PtyId.Create id, waiting))
              "failObservation"
              ==> (fun (id, reason) -> port.FailExitObservation(PtyId.Create id, reason))
              "exists" ==> (fun id -> port.Exists(PtyId.Create id))
              "exitListeners" ==> (fun () -> port.ExitListenerCount)
              "completions" ==> (fun () -> runtime.PendingCompletionCount) ]

    let private commandKind =
        function
        | PtyCommand.Spawn _ -> "Spawn"
        | PtyCommand.Write _ -> "Write"
        | PtyCommand.Read -> "Read"
        | PtyCommand.Signal _ -> "Signal"
        | PtyCommand.Resize _ -> "Resize"

    let private controlledPort (onCommand: obj) =
        let handler (id: PtyId) command =
            emitJsExpr (onCommand, id.Value, commandKind command) "$0($1, $2)"
            Task.FromResult(Ok())

        PtyPort(handler = handler)

    let ownershipProbe (commonDir: string option) (sessionId: string) (onCommand: obj) : obj =
        createOwnershipProbe commonDir sessionId (controlledPort onCommand)

    let realOwnershipProbe (commonDir: string option) (sessionId: string) : obj =
        createOwnershipProbe commonDir sessionId (PtyBackend.createPort ())

    let private ownershipPair commonDir sessionId port =
        createObj
            [ "first" ==> createOwnershipProbe commonDir sessionId port
              "second" ==> createOwnershipProbe commonDir sessionId port ]

    let sharedOwnershipProbe (commonDir: string option) (sessionId: string) (onCommand: obj) : obj =
        ownershipPair commonDir sessionId (controlledPort onCommand)

    let realSharedOwnershipProbe (commonDir: string option) (sessionId: string) : obj =
        ownershipPair commonDir sessionId (PtyBackend.createPort ())

    let private callView command =
        match command with
        | PtyCommand.Write bytes ->
            Some(
                box
                    {| kind = "write"
                       text = Encoding.UTF8.GetString bytes |}
            )
        | PtyCommand.Signal signal ->
            Some(
                box
                    {| kind = "signal"
                       signal = PtySupervisor.signalName signal |}
            )
        | _ -> None

    let private outcomeView calls owned known =
        function
        | Ok read ->
            box
                {| ok = true
                   id = read.Id.Value
                   output = read.Output
                   closed = read.Closed
                   owned = owned
                   known = known
                   calls = calls |}
        | Error error ->
            box
                {| ok = false
                   error = error
                   owned = owned
                   known = known
                   calls = calls |}

    let private forkView (calls: obj array) (runtime: HostForkRuntime) (port: PtyPort) =
        function
        | Ok(id: PtyId) ->
            box
                {| ok = true
                   id = id.Value
                   owned = runtime.OwnsPty id
                   known = port.Known id
                   calls = calls |}
        | Error error ->
            box
                {| ok = false
                   error = error
                   owned = runtime.SnapshotOutstandingPtyRuns() |> List.isEmpty |> not
                   calls = calls |}

    let private devopsRoot (journal: AgentJournal) child =
        (AgentJournal.snapshot journal).AgentProjections.Sessions
        |> Map.tryFind child
        |> Option.bind (fun session -> session.PromptAuthority)
        |> Option.bind (fun authority -> authority.ActiveLogicalRun)
        |> Option.filter (fun profile ->
            profile.SessionId = child
            && profile.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
            && profile.CanonicalRole = Role.DevOps)
        |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
        |> Option.defaultWith (fun () -> invalidOp "DevOps work requires exact canonical child authority")

    let scenario (action: string) (input: string) (failure: string) (journal: JournalHandle option) : Task<obj> =
        task {
            let calls = ResizeArray<obj>()
            // DSL-MUTABLE: resource — controlled backend read-completion port
            let mutable portRef: PtyPort option = None

            let handler id command =
                if action = "fork-error" then
                    match command with
                    | PtyCommand.Spawn _ -> raise (InvalidOperationException failure)
                    | _ -> ()

                callView command |> Option.iter calls.Add

                match command, portRef with
                | PtyCommand.Read, Some port -> port.ReadResult(id, "terminal text", true)
                | PtyCommand.Spawn _, Some port when action = "synchronous-exit" -> port.Complete id
                | _ -> ()

                if String.IsNullOrEmpty failure then
                    Task.FromResult(Ok())
                else
                    Task.FromResult(Error failure)

            let port = PtyPort(handler = handler)
            portRef <- Some port

            let completingPort () =
                let mutable localPort: PtyPort option = None

                let completeOnTerminate id command =
                    let result = handler id command

                    match command, localPort with
                    | PtyCommand.Signal PtySignal.Terminate, Some ownedPort -> ownedPort.Complete id
                    | _ -> ()

                    result

                let ownedPort = PtyPort(handler = completeOnTerminate)
                localPort <- Some ownedPort
                ownedPort

            let runtime =
                HostForkRuntime(
                    SessionId.create "pty-owner",
                    Unchecked.defaultof<ISessionHostPort>,
                    (fun _ _ _ -> Task.FromResult None),
                    CompletionMailboxRuntime.create,
                    NodeTiming.nodeClockPort (),
                    NodeTiming.raceExit,
                    ptyPort = port
                )

            let agent = ManagedAgent.make Role.DevOps

            match action with
            | "missing-durable-owner" ->
                let production =
                    HostForkRuntime(
                        SessionId.create "unowned-pty",
                        Unchecked.defaultof<ISessionHostPort>,
                        (fun _ _ _ -> Task.FromResult None),
                        CompletionMailboxRuntime.create,
                        NodeTiming.nodeClockPort (),
                        NodeTiming.raceExit
                    )

                let! outcome = production.ForkPty(input, agent)
                return forkView (calls.ToArray()) production port outcome
            | "lookup-unknown" ->
                return
                    box
                        {| ok = true
                           known = runtime.TryPty(input).IsSome
                           calls = calls.ToArray() |}
            | "send-unowned" ->
                let id = PtyId.Create "foreign"
                let! outcome = runtime.SendPty(id, input, None)
                return outcomeView (calls.ToArray()) (runtime.OwnsPty id) (port.Known id) outcome
            | "synchronous-exit"
            | "close-without-exit" ->
                let! opened = runtime.ForkPty("shell", agent)
                let id = opened |> Result.defaultWith failwith

                let! error =
                    task {
                        if action = "close-without-exit" then
                            try
                                do! runtime.CloseOwnedPtys(graceMs = 0)
                                return ""
                            with ex ->
                                return ex.Message
                        else
                            return ""
                    }

                let _, snapshots = runtime.List()

                return
                    box
                        {| owned = runtime.OwnsPty id
                           live = port.Exists id
                           completions = runtime.PendingCompletionCount
                           snapshots = snapshots.Length
                           bindAfter = runtime.TryBindTerminalName("instant", id) |> Result.isOk
                           error = error |}
            | "fork-complete" ->
                let! opened = runtime.ForkPty("shell", agent)
                let id = opened |> Result.defaultWith failwith
                let ownedBefore = runtime.OwnsPty id
                port.Complete id

                return
                    box
                        {| ok = true
                           ownedBefore = ownedBefore
                           ownedAfter = runtime.OwnsPty id
                           calls = calls.ToArray() |}
            | "exit-cleanup" ->
                match! runtime.ForkPty("shell", agent) with
                | Error error ->
                    return
                        box
                            {| ok = false
                               error = error
                               calls = calls.ToArray() |}
                | Ok id ->
                    let termName = "term1"
                    let bindResult = runtime.TryBindTerminalName(termName, id)
                    let outstandingBefore = runtime.SnapshotOutstandingPtyRuns() |> List.toArray
                    let ownedBefore = runtime.OwnsPty id
                    let byNameBefore = runtime.TryPtyByName(termName)

                    // Deliver physical backend exit
                    port.Complete id

                    let outstandingAfter = runtime.SnapshotOutstandingPtyRuns() |> List.toArray
                    let ownedAfter = runtime.OwnsPty id
                    let byNameAfter = runtime.TryPtyByName(termName)

                    return
                        box
                            {| ok = true
                               id = id.Value
                               bindOk = Result.isOk bindResult
                               outstandingBefore = outstandingBefore
                               ownedBefore = ownedBefore
                               byNameBefore = byNameBefore |> Option.map (fun (p: PtyId) -> p.Value)
                               outstandingAfter = outstandingAfter
                               ownedAfter = ownedAfter
                               byNameAfter = byNameAfter |> Option.map (fun (p: PtyId) -> p.Value)
                               calls = calls.ToArray() |}
            | "devops-return-drain" ->
                let durable =
                    journal
                    |> Option.map (fun handle -> handle.Journal)
                    |> Option.defaultWith (fun () -> invalidOp "DevOps settlement requires a canonical journal")
                // Setup child runtime representing DevOps session
                let devopsSessionId = SessionId.create "devops-session"
                let authorityRoot = devopsRoot durable devopsSessionId
                let devopsPort = completingPort ()

                let devopsRuntime =
                    HostForkRuntime(
                        devopsSessionId,
                        Unchecked.defaultof<ISessionHostPort>,
                        (fun _ _ _ -> Task.FromResult None),
                        CompletionMailboxRuntime.create,
                        NodeTiming.nodeClockPort (),
                        NodeTiming.raceExit,
                        ptyPort = devopsPort
                    )
                // DevOps opens a PTY in its own session
                let! devopsPtyResult = devopsRuntime.ForkPty("long-running-devops-task", agent)

                let devopsPtyId =
                    match devopsPtyResult with
                    | Ok id -> id
                    | Error e -> failwith e

                // Another session (e.g. engineer session) opens its own PTY
                let engineerSessionId = SessionId.create "engineer-session"
                let engineerPort = completingPort ()

                let engineerRuntime =
                    HostForkRuntime(
                        engineerSessionId,
                        Unchecked.defaultof<ISessionHostPort>,
                        (fun _ _ _ -> Task.FromResult None),
                        CompletionMailboxRuntime.create,
                        NodeTiming.nodeClockPort (),
                        NodeTiming.raceExit,
                        ptyPort = engineerPort
                    )

                let! engineerPtyResult = engineerRuntime.ForkPty("engineer-task", ManagedAgent.make Role.Engineer)

                let engineerPtyId =
                    match engineerPtyResult with
                    | Ok id -> id
                    | Error e -> failwith e

                // Manager runtime configured with drainChildPtys capability
                let drainChildPtys (sid: SessionId) =
                    task {
                        if sid = devopsSessionId then
                            do! devopsRuntime.CloseOwnedPtys()
                    }

                let dummySessions =
                    { new ISessionHostPort with
                        member _.SubscribeTerminal(_, _) =
                            { new IDisposable with
                                member _.Dispose() = () }

                        member _.SubscribeFutureTerminal(_, _) =
                            { new IDisposable with
                                member _.Dispose() = () }

                        member _.SendPrompt(_, _, _) =
                            Task.FromResult(Outcome.SendOutcome.AcceptanceUnknown "dummy")

                        member _.AbortSession _ = Task.FromResult(Ok())
                        member _.InterruptAttempt _ = Task.FromResult(Ok())
                        member _.IsManagedChild _ = true
                        member _.AbortChildren _ = Task.FromResult(()) :> Task
                        member _.CreateSiblingSession(_, _, _) = Task.FromResult(Error "dummy")
                        member _.TryGetParentSession _ = Task.FromResult(Ok None)
                        member _.CreateChildSession(_, _) = Task.FromResult(Error "dummy")
                        member _.ListChildren _ = Task.FromResult(Ok [])
                        member _.FamilyRootOf sessionId = sessionId }

                let managerRuntime =
                    HostForkRuntime(
                        SessionId.create "manager-session",
                        dummySessions,
                        (fun _ _ _ -> Task.FromResult None),
                        CompletionMailboxRuntime.create,
                        NodeTiming.nodeClockPort (),
                        NodeTiming.raceExit,
                        drainChildPtys = drainChildPtys,
                        journal = durable
                    )

                let run =
                    managerRuntime.InstallRun("devops", devopsSessionId, Role.DevOps, authorityRoot)

                let devopsPtysBefore = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray

                let engineerPtysBefore =
                    engineerRuntime.SnapshotOutstandingPtyRuns() |> List.toArray

                // Complete DevOps run (settlement upon return)
                let outcome =
                    TerminalOutcome.Completed
                        { SessionId = devopsSessionId
                          Role = Role.DevOps
                          ProviderRun = ProviderRunIdentity.create "prov-run"
                          AuthorityRootUserMessageId = authorityRoot
                          Directory = None
                          TerminalText = "devops finished"
                          TurnFormalText = "devops finished" }

                managerRuntime.Complete(run, outcome)
                // Drain owned background work in manager runtime to ensure drainChildPtys has finished
                do! managerRuntime.DrainOwnedWork()

                let devopsPtysAfter = devopsRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
                let engineerPtysAfter = engineerRuntime.SnapshotOutstandingPtyRuns() |> List.toArray
                let observedCalls = calls.ToArray()
                do! managerRuntime.DetachAndDrain()
                do! devopsRuntime.DetachAndDrain()
                do! engineerRuntime.DetachAndDrain()
                do! runtime.DetachAndDrain()

                return
                    box
                        {| ok = true
                           devopsPtyId = devopsPtyId.Value
                           engineerPtyId = engineerPtyId.Value
                           devopsBefore = devopsPtysBefore
                           devopsAfter = devopsPtysAfter
                           engineerBefore = engineerPtysBefore
                           engineerAfter = engineerPtysAfter
                           calls = observedCalls |}
            | "blank-fork"
            | "fork"
            | "fork-error" ->
                let! outcome = runtime.ForkPty(input, agent)
                return forkView (calls.ToArray()) runtime port outcome
            | _ ->
                match! runtime.ForkPty("shell", agent) with
                | Error error ->
                    return
                        box
                            {| ok = false
                               error = error
                               calls = calls.ToArray() |}
                | Ok id ->
                    if action = "send-closed" then
                        port.Complete id

                    let signal =
                        if action = "signal" then
                            PtySignal.tryParse input |> Result.toOption
                        else
                            None

                    let prompt =
                        if action = "write" || action = "send-closed" then
                            input
                        else
                            ""

                    let! outcome = runtime.SendPty(id, prompt, signal)
                    return outcomeView (calls.ToArray()) (runtime.OwnsPty id) (port.Known id) outcome
        }
