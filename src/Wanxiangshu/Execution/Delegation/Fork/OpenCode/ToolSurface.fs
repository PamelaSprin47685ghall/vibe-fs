namespace Wanxiangshu.Execution.Delegation.Fork.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Session.OpenCode

/// Opaque JS-native harness for the real Manager fork tool path.
/// Production semantics stay in ForkTool/HostForkRuntime; this surface only
/// supplies a physical Host boundary for executable requirement proofs.
module ForkToolSurface =

    type private ForkSessionPort() =
        let children = ResizeArray<OpenCodeChildInfo>()
        let listeners = Dictionary<string, ResizeArray<TerminalCompletionListener>>()
        let prompts = Dictionary<string, ResizeArray<string>>()

        let promptWaiters =
            Dictionary<string, ResizeArray<int * TaskCompletionSource<unit>>>()

        let emittedWaiters = ResizeArray<int * TaskCompletionSource<unit>>()

        let pendingAcceptances =
            Dictionary<string, ResizeArray<TaskCompletionSource<SendOutcome>>>()

        let physicalRoots = Dictionary<string, ResizeArray<string>>()
        // DSL-MUTABLE: algorithm-scratch — exactly one next Host send outcome in the harness
        let mutable nextSendOutcome: SendOutcome option = None
        // DSL-MUTABLE: algorithm-scratch — Host AbortSession call count in the harness
        let mutable abortCount = 0
        // DSL-MUTABLE: algorithm-scratch — synthetic physical message id counter for the harness
        let physicalSequence = ref 0

        let historyOf (source: Dictionary<string, ResizeArray<string>>) key =
            match source.TryGetValue key with
            | true, values -> values
            | false, _ ->
                let values = ResizeArray<string>()
                source[key] <- values
                values

        let acceptancesOf key =
            match pendingAcceptances.TryGetValue key with
            | true, values -> values
            | false, _ ->
                let values = ResizeArray<TaskCompletionSource<SendOutcome>>()
                pendingAcceptances[key] <- values
                values

        let waitersOf key =
            match promptWaiters.TryGetValue key with
            | true, values -> values
            | false, _ ->
                let values = ResizeArray<int * TaskCompletionSource<unit>>()
                promptWaiters[key] <- values
                values

        let promptCountForKey key =
            match prompts.TryGetValue key with
            | true, values -> values.Count
            | false, _ -> 0

        let retainPromptWaiters key pending =
            match pending with
            | [] -> promptWaiters.Remove key |> ignore
            | values -> promptWaiters[key] <- ResizeArray(values)

        let releasePromptWaiters key =
            match promptWaiters.TryGetValue key with
            | false, _ -> ()
            | true, waiters ->
                let admitted = promptCountForKey key

                let ready, pending =
                    waiters |> Seq.toList |> List.partition (fun (target, _) -> admitted >= target)

                retainPromptWaiters key pending

                ready
                |> List.iter (fun (_, waiter) -> AsyncSupport.trySetResult waiter () |> ignore)

        let releaseEmittedWaiters () =
            let admitted =
                match children |> Seq.tryLast with
                | Some child -> promptCountForKey (SessionId.value child.SessionId)
                | None -> 0

            let ready =
                emittedWaiters
                |> Seq.filter (fun (target, _) -> admitted >= target)
                |> Seq.toList

            for registration in ready do
                emittedWaiters.Remove registration |> ignore

                registration
                |> snd
                |> fun waiter -> AsyncSupport.trySetResult waiter () |> ignore

        let subscribe sessionId listener =
            let key = SessionId.value sessionId

            let registrations =
                match listeners.TryGetValue key with
                | true, values -> values
                | false, _ ->
                    let values = ResizeArray<TerminalCompletionListener>()
                    listeners[key] <- values
                    values

            registrations.Add listener

            { new IDisposable with
                member _.Dispose() = registrations.Remove listener |> ignore }

        member _.LatestChild =
            children |> Seq.tryLast |> Option.map (fun child -> child.SessionId)

        member _.ChildCount = children.Count

        member _.PromptCount(sessionId: SessionId) =
            promptCountForKey (SessionId.value sessionId)

        member _.WaitForPromptCount(sessionId: SessionId, count: int) : Task =
            let key = SessionId.value sessionId

            if promptCountForKey key >= count then
                Task.FromResult(()) :> Task
            else
                let waiter =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                waitersOf key |> fun values -> values.Add(count, waiter)
                waiter.Task :> Task

        member _.WaitForEmittedPromptCount(count: int) : Task =
            let admitted =
                match children |> Seq.tryLast with
                | Some child -> promptCountForKey (SessionId.value child.SessionId)
                | None -> 0

            if admitted >= count then
                Task.FromResult(()) :> Task
            else
                let waiter =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                emittedWaiters.Add(count, waiter)
                waiter.Task :> Task

        member _.AcceptPrompt(sessionId: SessionId, index: int) =
            let key = SessionId.value sessionId

            match pendingAcceptances.TryGetValue key with
            | true, values when index >= 0 && index < values.Count ->
                physicalSequence.Value <- physicalSequence.Value + 1
                let physical = sprintf "fork-physical-%d" physicalSequence.Value

                if
                    AsyncSupport.trySetResult
                        values[index]
                        (SendOutcome.AdmittedWithPhysicalMessage(PhysicalUserMessageId.create physical))
                then
                    historyOf physicalRoots key |> fun roots -> roots.Add physical
                    true
                else
                    false
            | _ -> false

        member _.Prompt(sessionId: SessionId, index: int) =
            match prompts.TryGetValue(SessionId.value sessionId) with
            | true, values when index >= 0 && index < values.Count -> Some values[index]
            | _ -> None

        member _.LatestAuthorityRoot(sessionId: SessionId) =
            match physicalRoots.TryGetValue(SessionId.value sessionId) with
            | true, values when values.Count > 0 -> Some values[values.Count - 1]
            | _ -> None

        member _.SetNextSendOutcome(outcome: SendOutcome) = nextSendOutcome <- Some outcome
        member _.AbortCount = abortCount

        member _.Notify(sessionId: SessionId, outcome: TerminalOutcome) =
            match listeners.TryGetValue(SessionId.value sessionId) with
            | true, registrations ->
                for listener in registrations |> Seq.toList do
                    listener sessionId outcome
            | false, _ -> ()

        interface ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) = subscribe sessionId listener
            member _.SubscribeFutureTerminal(sessionId, listener) = subscribe sessionId listener

            member _.SendPrompt(sessionId, text, _) =
                let key = SessionId.value sessionId
                historyOf prompts key |> fun values -> values.Add text
                releasePromptWaiters key
                releaseEmittedWaiters ()

                match nextSendOutcome with
                | Some outcome ->
                    nextSendOutcome <- None
                    Task.FromResult outcome
                | None ->
                    let acceptance =
                        TaskCompletionSource<SendOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                    acceptancesOf key |> fun values -> values.Add acceptance
                    acceptance.Task

            member _.AbortSession _ =
                abortCount <- abortCount + 1
                Task.FromResult(Ok())

            member _.InterruptAttempt _ = Task.FromResult(Ok())
            member _.IsManagedChild _ = true
            member _.AbortChildren _ = Task.FromResult()

            member _.CreateSiblingSession(_, _, _) =
                Task.FromResult(Error "fork surface does not create fission siblings")

            member _.TryGetParentSession sessionId =
                children
                |> Seq.tryFind (fun child -> child.SessionId = sessionId)
                |> Option.bind (fun child -> child.ParentSessionId)
                |> Ok
                |> Task.FromResult

            member _.CreateChildSession(parent, options) =
                let childId =
                    SessionId.create (sprintf "%s-fork-child-%d" (SessionId.value parent) (children.Count + 1))

                children.Add
                    { SessionId = childId
                      ParentSessionId = Some parent
                      Agent = options.Agent
                      Title = options.Title }

                Task.FromResult(Ok childId)

            member _.ListChildren parent =
                children
                |> Seq.filter (fun child -> child.ParentSessionId = Some parent)
                |> Seq.toList
                |> Ok
                |> Task.FromResult

            member _.FamilyRootOf sessionId =
                children
                |> Seq.tryFind (fun child -> child.SessionId = sessionId)
                |> Option.bind (fun child -> child.ParentSessionId)
                |> Option.defaultValue sessionId

    type private ForkHarness
        (
            journal: AgentJournal,
            scope: ToolRuntimeScope,
            sessions: ForkSessionPort,
            ownerAgents: Dictionary<string, string>
        ) =
        member _.Journal = journal
        member _.Scope = scope
        member _.Sessions = sessions
        member _.OwnerSession(owner: string) = SessionId.create owner
        member _.OwnerAgent(owner: string) = ownerAgents[owner]

        member _.Dispose() =
            (scope :> IDisposable).Dispose()
            (journal :> IDisposable).Dispose()

    let private createJournal (directory: string) : Task<AgentJournal> =
        task {
            let store =
                EventStore.createLocal directory (Guid.NewGuid().ToString("N")) (CanonicalIntegrator.create ())

            match!
                EventStoreJournalWriter.resumeOrCreate (
                    RuntimeId.create (sprintf "fork-surface-%s" (ToolHostCodec.digest directory)),
                    1,
                    DateTimeOffset.UtcNow,
                    store
                )
            with
            | Ok(writer, _, projection) ->
                match AgentJournal.createFromProjection writer projection with
                | Ok journal -> return journal
                | Error rejection -> return failwithf "%s: %s" rejection.Fact rejection.Reason
            | Error rejection -> return failwithf "%s: %s" rejection.Fact rejection.Reason
        }

    [<Emit("$0 == null")>]
    let private isNullish (value: obj) : bool = jsNative

    let private requiredOwnerString (fieldName: string) (value: obj) : Result<string, string> =
        let isString: bool = emitJsExpr value "typeof $0 === 'string'"

        if not isString || String.IsNullOrWhiteSpace(unbox<string> value) then
            Error(sprintf "invalid fork owner descriptor: %s must be a non-empty string" fieldName)
        else
            Ok(unbox<string> value)

    let private ownerAdmission (descriptor: obj) =
        let isPlainObject: bool =
            not (isNullish descriptor)
            && emitJsExpr
                descriptor
                "typeof $0 === 'object' && !Array.isArray($0) && (Object.getPrototypeOf($0) === Object.prototype || Object.getPrototypeOf($0) === null)"

        if not isPlainObject then
            Error "invalid fork owner descriptor: descriptor must be a plain object"
        else
            match requiredOwnerString "sessionId" descriptor?sessionId with
            | Error error -> Error error
            | Ok sessionId ->
                match requiredOwnerString "agent" descriptor?agent with
                | Error error -> Error error
                | Ok agent ->
                    ParticipantIdentity.resolveAtRoot agent
                    |> Result.mapError (sprintf "invalid fork owner descriptor agent: %A")
                    |> Result.map (fun identity ->
                        SessionId.create sessionId,
                        PhysicalUserMessageId.create (sprintf "fork-owner-root:%s" sessionId),
                        PromptAuthority.IdentitySeed.RootSelection identity,
                        agent)

    let private ownerAdmissions (owners: obj) =
        let isArray: bool = emitJsExpr owners "Array.isArray($0)"

        if not isArray || (unbox<obj array> owners).Length = 0 then
            Error "invalid fork owner descriptors: expected a non-empty array"
        else
            let rec collect seen admissions remaining =
                match remaining with
                | [] -> Ok(List.rev admissions)
                | descriptor :: tail ->
                    match ownerAdmission descriptor with
                    | Error error -> Error error
                    | Ok((sessionId, _, _, _) as admission) ->
                        let session = SessionId.value sessionId

                        if Set.contains session seen then
                            Error(sprintf "invalid fork owner descriptors: duplicate sessionId '%s'" session)
                        else
                            collect (Set.add session seen) (admission :: admissions) tail

            unbox<obj array> owners |> Array.toList |> collect Set.empty []

    let rec private acceptOwnerRoots (dispatcher: PromptDispatcher.Runtime) admissions : Task<Result<unit, string>> =
        task {
            match admissions with
            | [] -> return Ok()
            | (sessionId, physicalMessageId, identitySeed, _) :: tail ->
                match! dispatcher.AcceptHumanRoot sessionId physicalMessageId (Some identitySeed) with
                | Ok _ -> return! acceptOwnerRoots dispatcher tail
                | Error error ->
                    return
                        Error(
                            sprintf
                                "fork owner '%s' root admission rejected: %s"
                                (SessionId.value sessionId)
                                (PromptDispatcher.describeHumanRootAcceptanceFailure error)
                        )
        }

    let createRuntime (directory: string) (owners: obj) : Task<obj> =
        task {
            let admissions =
                match ownerAdmissions owners with
                | Ok admissions -> admissions
                | Error error -> raise (ArgumentException error)

            let! journal = createJournal directory
            let dispatcher = PromptDispatcher.Runtime(journal)

            match! acceptOwnerRoots dispatcher admissions with
            | Error error ->
                (journal :> IDisposable).Dispose()
                raise (InvalidOperationException error)
            | Ok() -> ()

            let ownerAgents = Dictionary<string, string>()

            for (sessionId, _, _, agent) in admissions do
                ownerAgents.Add(SessionId.value sessionId, agent)

            let sessionPort = ForkSessionPort()
            let sessions = sessionPort :> ISessionHostPort

            let scope =
                new ToolRuntimeScope(
                    sessions,
                    Some journal,
                    None,
                    Some directory,
                    Dictionary<string, string>(),
                    (fun _ -> None),
                    HashSet<string>(),
                    Dictionary<string, string>(),
                    None,
                    None,
                    None,
                    None,
                    None
                )

            return box (ForkHarness(journal, scope, sessionPort, ownerAgents))
        }

    let private managerContext (harness: ForkHarness) owner =
        { SessionId = SessionId.value (harness.OwnerSession owner)
          Agent = Some(harness.OwnerAgent owner)
          ToolCallId = None
          ProviderRunId = None
          PromptText = None
          AttachAbort = fun _ -> fun () -> () }

    let executeManagerFork
        (value: obj)
        (toolModule: obj)
        (owner: string)
        (calling: string)
        (byname: string)
        (charge: string)
        : Task<string> =
        task {
            let harness = unbox<ForkHarness> value
            let spec = ForkTool.managerSpec (ToolHostCodec.factory toolModule) harness.Scope

            let args =
                HostToolArguments(
                    box
                        {| calling = if String.IsNullOrWhiteSpace calling then null else calling
                           name = byname
                           charge = charge
                           keywords = null
                           attach = null
                           expected_tool_calls = null |}
                )

            return! spec.Execute args (managerContext harness owner)
        }

    let captureOwnerOpening (value: obj) (owner: string) (text: string) : Task =
        let harness = unbox<ForkHarness> value
        XTraceCapture.captureOpening (Some harness.Journal) (harness.OwnerSession owner) text [] :> Task

    let private traceMessage (messageId: string) (text: string) : SessionMessage =
        { Id = messageId
          Role = "assistant"
          Agent = Some "fast-coder"
          Finish = Some "stop"
          ErrorName = None
          Model = None
          ParentId = None
          Completed = true
          IsCompaction = false
          PromptKey = None
          Parts = [| MessagePart.Text text |]
          PartIds = [| None |]
          ToolParts = [||] }

    let private captureTraceText journal sessionId messageId text : Task =
        task {
            match! XTraceCapture.captureSessionMessages (Some journal) sessionId [ traceMessage messageId text ] with
            | Ok() -> ()
            | Error error -> return raise (InvalidOperationException error)
        }
        :> Task

    let captureOwnerDeltaPart (value: obj) (owner: string) (text: string) (providerRun: string) : Task =
        let harness = unbox<ForkHarness> value
        captureTraceText harness.Journal (harness.OwnerSession owner) providerRun text

    let childCount (value: obj) =
        (unbox<ForkHarness> value).Sessions.ChildCount

    let abortCount (value: obj) =
        (unbox<ForkHarness> value).Sessions.AbortCount

    let child (value: obj) : obj =
        (unbox<ForkHarness> value).Sessions.LatestChild
        |> Option.map (SessionId.value >> box)
        |> Option.defaultValue null

    let promptCount (value: obj) =
        let harness = unbox<ForkHarness> value

        harness.Sessions.LatestChild
        |> Option.map harness.Sessions.PromptCount
        |> Option.defaultValue 0

    let awaitPromptCount (value: obj) (count: int) : Task =
        let harness = unbox<ForkHarness> value
        harness.Sessions.WaitForEmittedPromptCount count

    let acceptPrompt (value: obj) (index: int) : bool =
        let harness = unbox<ForkHarness> value

        match harness.Sessions.LatestChild with
        | Some childId -> harness.Sessions.AcceptPrompt(childId, index)
        | None -> false

    let prompt (value: obj) (index: int) : obj =
        let harness = unbox<ForkHarness> value

        harness.Sessions.LatestChild
        |> Option.bind (fun childId -> harness.Sessions.Prompt(childId, index))
        |> Option.map box
        |> Option.defaultValue null

    let nextPromptAcceptanceUnknown (value: obj) (reason: string) =
        let harness = unbox<ForkHarness> value
        harness.Sessions.SetNextSendOutcome(SendOutcome.AcceptanceUnknown reason)

    let nextPromptAdmittedWithReceipt (value: obj) (receipt: string) =
        let harness = unbox<ForkHarness> value
        harness.Sessions.SetNextSendOutcome(SendOutcome.AdmittedWithReceipt(TransportReceipt.create receipt))

    let cancelOwnerChildren (value: obj) (owner: string) : Task =
        let harness = unbox<ForkHarness> value
        harness.Scope.CancelSessionChildren(SessionId.value (harness.OwnerSession owner))

    let detachToolRuntime (value: obj) : Task =
        let harness = unbox<ForkHarness> value
        harness.Scope.DisposeAsync()

    let durableLifecycleByname (value: obj) (owner: string) (byname: string) : obj =
        let harness = unbox<ForkHarness> value

        AgentJournal.handleProjection harness.Journal (harness.OwnerSession owner)
        |> HandleProjection.tryFindByByname byname
        |> Option.map (fun record ->
            match record.Lifecycle with
            | HandleLifecycle.Active -> "Active"
            | HandleLifecycle.CompletedAwaitingJoin _ -> "CompletedAwaitingJoin"
            | HandleLifecycle.Abandoned _ -> "Abandoned"
            | HandleLifecycle.Retired -> "Retired")
        |> Option.map box
        |> Option.defaultValue null

    let executeHorizon (value: obj) (owner: string) : Task<string> =
        let harness = unbox<ForkHarness> value
        let spec = HorizonTool.spec harness.Scope
        spec.Execute (HostToolArguments(box {| |})) (managerContext harness owner)

    let settle (value: obj) (owner: string) (answer: string) (providerRun: string) : Task<bool> =
        task {
            let harness = unbox<ForkHarness> value

            match harness.Sessions.LatestChild with
            | None -> return false
            | Some childId ->
                match harness.Sessions.LatestAuthorityRoot childId with
                | None -> return false
                | Some root ->
                    do! captureTraceText harness.Journal childId providerRun answer

                    harness.Sessions.Notify(
                        childId,
                        TerminalOutcome.Completed
                            { SessionId = childId
                              AuthorityRootUserMessageId = AuthorityRootUserMessageId.create root
                              ProviderRun = ProviderRunIdentity.create providerRun
                              Role = Role.Coder
                              Directory = None
                              TerminalText = answer
                              TurnFormalText = answer }
                    )

                    match harness.Scope.RuntimeFor(managerContext harness owner) with
                    | Error _ -> return false
                    | Ok runtime ->
                        match runtime.List() |> fst |> List.tryHead with
                        | None -> return false
                        | Some agent ->
                            match! runtime.AwaitCurrentWorkRecord agent.AgentId with
                            | Ok _ -> return true
                            | Error _ -> return false
        }

    let disposeRuntime (value: obj) =
        unbox<ForkHarness> value |> fun harness -> harness.Dispose()
