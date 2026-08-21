namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// Delegation-owned opaque runtime harness. Host sessions, journal writers,
/// attached-session state and completion turns never cross into JS; callers
/// observe only invocation promises and child identities.
[<RequireQualifiedAccess>]
module SyncDelegateSurface =
    type private PromptReadiness() =
        let admitted = Dictionary<string, int>()
        let completed = Dictionary<string, int>()
        let waiters = Dictionary<string, TaskCompletionSource<unit>>()
        let prompts = Dictionary<string, ResizeArray<string>>()

        let count (source: Dictionary<string, int>) key =
            match source.TryGetValue key with
            | true, value -> value
            | false, _ -> 0

        member _.Mark(sessionId: SessionId, prompt: string) =
            let key = SessionId.value sessionId
            admitted[key] <- count admitted key + 1

            let history =
                match prompts.TryGetValue key with
                | true, current -> current
                | false, _ ->
                    let created = ResizeArray<string>()
                    prompts[key] <- created
                    created

            history.Add prompt

            match waiters.TryGetValue key with
            | true, waiter ->
                waiters.Remove key |> ignore
                AsyncSupport.trySetResult waiter () |> ignore
            | false, _ -> ()

        member _.Complete(sessionId: SessionId) =
            let key = SessionId.value sessionId
            completed[key] <- count completed key + 1

        member _.AdmittedCount(sessionId: SessionId) =
            count admitted (SessionId.value sessionId)

        member _.Prompt(sessionId: SessionId, index: int) =
            match prompts.TryGetValue(SessionId.value sessionId) with
            | true, history when index >= 0 && index < history.Count -> Some history[index]
            | _ -> None

        member _.Wait(sessionId: SessionId) : Task<unit> =
            let key = SessionId.value sessionId

            if count admitted key > count completed key then
                Task.FromResult()
            else
                match waiters.TryGetValue key with
                | true, waiter -> waiter.Task
                | false, _ ->
                    let waiter =
                        TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                    waiters.Add(key, waiter)
                    waiter.Task

    type private Harness
        (
            journal: AgentJournal,
            runtime: SyncDelegateRuntime,
            scope: ToolRuntimeScope,
            sessions: SessionPort,
            readiness: PromptReadiness,
            children: ResizeArray<SessionId>,
            ownerPrefix: string
        ) =
        member _.Journal = journal
        member _.Runtime = runtime
        member _.Scope = scope
        member _.Sessions = sessions
        member _.Readiness = readiness
        member _.Children = children
        member _.OwnerSession(owner: string) = SessionId.create (ownerPrefix + owner)

        member _.Dispose() =
            runtime.Dispose()
            (scope :> IDisposable).Dispose()
            (journal :> IDisposable).Dispose()

    and private SessionPort(children: ResizeArray<SessionId>, readiness: PromptReadiness) =
        // DSL-MUTABLE: algorithm-scratch — synthetic physical message id counter for the harness
        let physicalSequence = ref 0
        let listeners = Dictionary<string, ResizeArray<TerminalCompletionListener>>()

        let subscribe sessionId listener =
            let key = SessionId.value sessionId

            let registrations =
                match listeners.TryGetValue key with
                | true, current -> current
                | false, _ ->
                    let created = ResizeArray<TerminalCompletionListener>()
                    listeners[key] <- created
                    created

            registrations.Add listener

            { new IDisposable with
                member _.Dispose() = registrations.Remove listener |> ignore }

        member _.Notify(sessionId: SessionId, outcome: TerminalOutcome) =
            match listeners.TryGetValue(SessionId.value sessionId) with
            | true, registrations ->
                for listener in registrations |> Seq.toList do
                    listener sessionId outcome
            | false, _ -> ()

        interface ISessionHostPort with
            member _.SubscribeTerminal(sessionId, listener) = subscribe sessionId listener

            member _.SubscribeFutureTerminal(sessionId, listener) = subscribe sessionId listener

            member _.SendPrompt(sessionId, prompt, _) =
                readiness.Mark(sessionId, prompt)
                physicalSequence.Value <- physicalSequence.Value + 1

                Task.FromResult(
                    SendOutcome.AdmittedWithPhysicalMessage(
                        PhysicalUserMessageId.create (sprintf "msg-physical-%d" physicalSequence.Value)
                    )
                )

            member _.AbortSession _ = Task.FromResult(Ok())
            member _.InterruptAttempt _ = Task.FromResult(Ok())
            member _.IsManagedChild _ = true
            member _.AbortChildren _ = Task.FromResult()

            member _.CreateSiblingSession(_, _, _) =
                Task.FromResult(Error "sibling creation is outside a managed delegation")

            member _.TryGetParentSession _ = Task.FromResult(Ok None)

            member _.CreateChildSession(parent, _) =
                let child =
                    SessionId.create (sprintf "%s-child-%d" (SessionId.value parent) (children.Count + 1))

                children.Add child
                Task.FromResult(Ok child)

            member _.ListChildren parent =
                children
                |> Seq.filter (fun child ->
                    (SessionId.value child)
                        .StartsWith((SessionId.value parent) + "-child-", StringComparison.Ordinal))
                |> Seq.collect (fun child ->
                    [ { SessionId = child
                        ParentSessionId = Some parent
                        Title = Some "managed delegate"
                        Agent = Some "fast-inspector" }
                      { SessionId = child
                        ParentSessionId = Some parent
                        Title = Some "managed delegate"
                        Agent = Some "fast-coder" } ])
                |> Seq.toList
                |> Ok
                |> Task.FromResult

            member _.FamilyRootOf sessionId = sessionId

    let private waitForReadyCall
        (runtime: SyncDelegateRuntime)
        (readiness: PromptReadiness)
        (owner: SessionId)
        (role: SyncDelegateRole)
        : Task<SessionId option> =
        task {
            match runtime.TryFind(owner, role) with
            | None -> return None
            | Some child ->
                do! readiness.Wait child

                let! accepted = runtime.AwaitAssignmentReady child

                if accepted && runtime.HasOpeningCursor child then
                    return Some child
                else
                    return None
        }

    let private roleOf (value: string) : Result<SyncDelegateRole, string> =
        if String.IsNullOrWhiteSpace value then
            Error "role is required"
        elif value.Equals("Coder", StringComparison.OrdinalIgnoreCase) then
            Ok SyncDelegateRole.Coder
        elif value.Equals("Inspector", StringComparison.OrdinalIgnoreCase) then
            Ok SyncDelegateRole.Inspector
        else
            Error(sprintf "unknown role: %s" value)

    let private outcomeOf (value: string) : Result<ReconcileProgram.TurnOutcome, string> =
        match value with
        | "TurnCompleted" -> Ok(ReconcileProgram.TurnCompleted)
        | "TurnFailed" -> Ok(ReconcileProgram.TurnFailed "transient provider failure")
        | "TurnNeedsContinuation" -> Ok(ReconcileProgram.TurnNeedsContinuation "retry")
        | "TurnAborted" -> Ok(ReconcileProgram.TurnAborted "aborted")
        | _ -> Error(sprintf "unknown outcome: %s" value)

    let private roleValue =
        function
        | SyncDelegateRole.Coder -> Role.Coder
        | SyncDelegateRole.Inspector -> Role.Inspector

    let private createJournal (directory: string) : Task<AgentJournal> =
        task {
            let integrator = CanonicalIntegrator.create ()

            let store =
                EventStore.createLocal directory (Guid.NewGuid().ToString("N")) integrator

            let! result =
                EventStoreJournalWriter.resumeOrCreate (
                    RuntimeId.create (sprintf "sync-delegate-surface-%s" (ToolHostCodec.digest directory)),
                    1,
                    DateTimeOffset.UtcNow,
                    store
                )

            match result with
            | Ok(writer, _, projection) ->
                match AgentJournal.createFromProjection writer projection with
                | Ok journal -> return journal
                | Error rejection -> return failwithf "%s: %s" rejection.Fact rejection.Reason
            | Error rejection -> return failwithf "%s: %s" rejection.Fact rejection.Reason
        }

    /// Create a real SyncDelegateRuntime with an opaque journal and Host port.
    /// The supplied directory is the workspace capability owned by the caller.
    let create (directory: string) : Task<obj> =
        task {
            let! journal = createJournal directory
            // DSL-MUTABLE: resource — session id backing registry for SessionPort
            let children = ResizeArray<SessionId>()
            let readiness = PromptReadiness()
            let sessionPort = SessionPort(children, readiness)
            let sessions = sessionPort :> ISessionHostPort
            let dispatcher = PromptDispatcher.Runtime(journal)
            let attached = new AttachedSessionRuntime()
            let gate = new SessionQuiescenceGate()

            let workRecordFor
                (sessionId: SessionId)
                (range: MagicTodoLwr.BoundedRange)
                (providerRun: ProviderRunIdentity)
                =
                LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                    (Some journal)
                    sessionId
                    range
                    providerRun

            let runtime =
                new SyncDelegateRuntime(
                    sessions,
                    dispatcher,
                    journal,
                    attached,
                    (fun _ -> Some AgentTier.Fast),
                    (fun _ _ -> ()),
                    gate,
                    workRecordFor,
                    DelegationHandoffLedger.port journal,
                    workspaceDirectory = directory
                )

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

            let ownerPrefix =
                sprintf "sync-delegate-surface-%s-" (ToolHostCodec.digest directory)

            return box (Harness(journal, runtime, scope, sessionPort, readiness, children, ownerPrefix))
        }

    /// Execute the real InspectorTool specification against the opaque scope and
    /// SyncDelegate runtime. Tool arguments/context are translated here so the
    /// semantic caller never imports ToolHostCodec or InspectorTool internals.
    let executeInspector (value: obj) (toolModule: obj) (owner: string) (charge: string) : Task<string> =
        task {
            let harness = unbox<Harness> value
            let factory = ToolHostCodec.factory toolModule
            let spec = InspectorTool.spec factory harness.Scope (Some harness.Runtime)

            let args =
                HostToolArguments(
                    box
                        {| charge = charge
                           keywords = null
                           expected_tool_calls = null |}
                )

            let context =
                { SessionId = SessionId.value (harness.OwnerSession owner)
                  Agent = None
                  ToolCallId = None
                  ProviderRunId = None
                  PromptText = None
                  AttachAbort = fun _ -> fun () -> () }

            return! spec.Execute args context
        }

    /// Invoke one ordinary managed delegation. The returned promise remains
    /// pending until `settle` receives a reconciled provider turn.
    let invoke (value: obj) (owner: string) (role: string) (question: string) : Task<obj> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error error -> return box {| ok = false; error = error |}
            | Ok role ->
                let! result = harness.Runtime.Invoke(SessionId.value (harness.OwnerSession owner), role, question)

                return
                    match result with
                    | Ok workRecord -> box {| ok = true; value = workRecord |}
                    | Error error -> box {| ok = false; error = error |}
        }

    let private handleTurn (harness: Harness) (child: SessionId) (turn: ReconciledTurn) =
        task {
            let! handled = harness.Runtime.HandleTurn(turn, None)

            if handled then
                harness.Readiness.Complete child

            return handled
        }

    let private activeAuthorityRoot (harness: Harness) (child: SessionId) =
        PromptAuthorityLedger.activeProfile child (AgentJournal.snapshot harness.Journal).AgentProjections
        |> Option.map (fun profile -> AuthorityRootUserMessageId.value profile.AuthorityRootUserMessageId)

    let private settleReadyChild
        (harness: Harness)
        (role: SyncDelegateRole)
        (child: SessionId)
        (answer: string)
        (runId: string)
        (authorityRoot: string)
        =
        let parts =
            if String.IsNullOrWhiteSpace answer then
                [||]
            else
                [| MessagePart.Text answer |]

        handleTurn
            harness
            child
            { SessionId = child
              PhysicalUserMessageId = PhysicalUserMessageId.create authorityRoot
              AuthorityRootUserMessageId = AuthorityRootUserMessageId.create authorityRoot
              ProviderRun = ProviderRunIdentity.create runId
              Role = Some(roleValue role)
              Directory = None
              Parts = parts
              Finish = Some "stop"
              ErrorName = None
              Model = None
              Outcome = ReconcileProgram.TurnCompleted
              Observation = None }

    /// Settle the current managed child through the real HandleTurn path.
    let settleWithAuthorityRoot
        (value: obj)
        (owner: string)
        (role: string)
        (answer: string)
        (runId: string)
        (authorityRoot: string)
        : Task<bool> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error _ -> return false
            | Ok role ->
                match! waitForReadyCall harness.Runtime harness.Readiness (harness.OwnerSession owner) role with
                | None -> return false
                | Some child -> return! settleReadyChild harness role child answer runId authorityRoot
        }

    let settle (value: obj) (owner: string) (role: string) (answer: string) (runId: string) : Task<bool> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error _ -> return false
            | Ok role ->
                match! waitForReadyCall harness.Runtime harness.Readiness (harness.OwnerSession owner) role with
                | None -> return false
                | Some child ->
                    match activeAuthorityRoot harness child with
                    | None -> return false
                    | Some root -> return! settleReadyChild harness role child answer runId root
        }

    let failWithAuthorityRoot
        (value: obj)
        (owner: string)
        (role: string)
        (reason: string)
        (authorityRoot: string)
        : Task<string> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error _ -> return "Unavailable"
            | Ok role ->
                match harness.Runtime.TryFind(harness.OwnerSession owner, role) with
                | None -> return "Unavailable"
                | Some child ->
                    let! accepted = harness.Runtime.AwaitAssignmentReady child

                    if not accepted then
                        return "Unavailable"
                    else
                        harness.Sessions.Notify(
                            child,
                            TerminalOutcome.Failed(
                                TerminalStop.forAuthority (AuthorityRootUserMessageId.create authorityRoot) reason
                            )
                        )

                        return
                            if harness.Runtime.HasOpeningCursor child then
                                "Ignored"
                            else
                                "Claimed"
        }

    let observeTurn
        (value: obj)
        (owner: string)
        (role: string)
        (outcomeName: string)
        (answer: string)
        (runId: string)
        : Task<bool> =
        task {
            let harness = unbox<Harness> value

            match roleOf role, outcomeOf outcomeName with
            | Error _, _
            | _, Error _ -> return false
            | Ok role, Ok outcome ->
                match! waitForReadyCall harness.Runtime harness.Readiness (harness.OwnerSession owner) role with
                | None -> return false
                | Some child ->
                    match activeAuthorityRoot harness child with
                    | None -> return false
                    | Some root ->
                        let parts =
                            if outcomeName = "TurnCompleted" && not (String.IsNullOrWhiteSpace answer) then
                                [| MessagePart.Text answer |]
                            else
                                [||]

                        let turn =
                            { SessionId = child
                              PhysicalUserMessageId = PhysicalUserMessageId.create root
                              AuthorityRootUserMessageId = AuthorityRootUserMessageId.create root
                              ProviderRun = ProviderRunIdentity.create runId
                              Role = Some(roleValue role)
                              Directory = None
                              Parts = parts
                              Finish = Some "stop"
                              ErrorName = None
                              Model = None
                              Outcome = outcome
                              Observation = None }

                        return! handleTurn harness child turn
        }

    let child (value: obj) (owner: string) (role: string) : obj =
        let harness = unbox<Harness> value

        match roleOf role with
        | Error _ -> null
        | Ok role ->
            match harness.Runtime.TryFind(harness.OwnerSession owner, role) with
            | Some sessionId -> box (SessionId.value sessionId)
            | None -> null

    let vocabulary (roleName: string) (tierName: string) (scope: string) : obj =
        match roleOf roleName with
        | Error error -> box {| ok = false; error = error |}
        | Ok role ->
            let tier =
                if
                    not (isNull tierName)
                    && tierName.Equals("Deep", StringComparison.OrdinalIgnoreCase)
                then
                    AgentTier.Deep
                else
                    AgentTier.Fast

            let key = DedicatedDelegateKey.create (ReuseScopeId.create scope) role

            box
                {| tier = SyncDelegate.tierLabel (SyncDelegate.tierForOwner tier)
                   agent = SyncDelegate.agentNameFor role tier
                   scope = ReuseScopeId.value key.Scope
                   role = SyncDelegate.roleLabel key.Role |}

    let childCount (value: obj) : int =
        let harness = unbox<Harness> value
        harness.Children.Count

    let promptCount (value: obj) (owner: string) (role: string) : int =
        let harness = unbox<Harness> value

        match roleOf role with
        | Error _ -> 0
        | Ok role ->
            harness.Runtime.TryFind(harness.OwnerSession owner, role)
            |> Option.map harness.Readiness.AdmittedCount
            |> Option.defaultValue 0

    let prompt (value: obj) (owner: string) (role: string) (index: int) : obj =
        let harness = unbox<Harness> value

        match roleOf role with
        | Error _ -> null
        | Ok role ->
            harness.Runtime.TryFind(harness.OwnerSession owner, role)
            |> Option.bind (fun child -> harness.Readiness.Prompt(child, index))
            |> Option.map box
            |> Option.defaultValue null

    let captureOwnerOpening (value: obj) (owner: string) (text: string) : Task =
        let harness = unbox<Harness> value
        XTraceCapture.captureOpening (Some harness.Journal) (harness.OwnerSession owner) text [] :> Task

    let captureOwnerDeltaPart (value: obj) (owner: string) (text: string) (providerRun: string) : Task =
        task {
            let harness = unbox<Harness> value

            match! harness.Journal.WriteBlob text with
            | Error error -> return raise (InvalidOperationException error)
            | Ok blob ->
                do!
                    XTraceCapture.captureLastWords
                        (Some harness.Journal)
                        (harness.OwnerSession owner)
                        blob.BlobRef
                        blob.BlobDigest
                        (ProviderRunIdentity.create providerRun)
        }
        :> Task

    let handoffFrontier (value: obj) (owner: string) (role: string) : obj =
        let harness = unbox<Harness> value
        let parent = harness.OwnerSession owner

        match roleOf role with
        | Error _ -> null
        | Ok role ->
            let scope = ReuseScope.ofSession parent
            let route = DelegationHandoffRoute.syncRole scope role
            let key = DelegationHandoff.key parent route

            (AgentJournal.snapshot harness.Journal)
                .AgentProjections.DelegationCompletedHandoffs
            |> Map.tryFind key
            |> Option.map box
            |> Option.defaultValue null

    let batchOrder (roleName: string) (toolNames: string array) (currentCall: string) : obj =
        match roleOf roleName with
        | Error error -> box {| ok = false; error = error |}
        | Ok role ->
            let order =
                toolNames
                |> Array.choose (fun name ->
                    match SyncDelegate.tryRoleOfToolName name with
                    | Some matched when matched = role -> Some name
                    | _ -> None)

            box
                {| order = order
                   currentPresent = order |> Array.exists (fun name -> name = currentCall) |}

    let invokeBatch
        (value: obj)
        (owner: string)
        (role: string)
        (charge: string)
        (providerRun: string)
        (callId: string)
        (callOrder: string array)
        : Task<obj> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error error -> return box {| kind = "Error"; error = error |}
            | Ok role ->
                let batch =
                    { ProviderRun = ProviderRunIdentity.create providerRun
                      CallOrder = callOrder |> Array.toList |> List.map ToolCallId.create
                      CurrentCall = ToolCallId.create callId }

                let! result =
                    harness.Runtime.InvokeBatchPrepared(
                        SessionId.value (harness.OwnerSession owner),
                        role,
                        charge,
                        batch,
                        (fun () -> Task.FromResult charge)
                    )

                return
                    match result with
                    | Ok(SyncDelegateInvocationResult.WorkRecord record) ->
                        box
                            {| kind = "WorkRecord"
                               value = record |}
                    | Ok(SyncDelegateInvocationResult.MergedInto canonical) ->
                        box
                            {| kind = "MergedInto"
                               canonical = ToolCallId.value canonical |}
                    | Error error -> box {| kind = "Error"; error = error |}
        }


    let serializationDecision (firstScope: string) (secondScope: string) (sameProviderRun: bool) : obj =
        let sameScope =
            ReuseScopeId.equals (ReuseScopeId.create firstScope) (ReuseScopeId.create secondScope)

        if sameScope && not sameProviderRun then
            box
                {| accepted = false
                   reason = "same ReuseScope already has an active batch" |}
        else
            box
                {| accepted = true
                   reason = "independent provider batch" |}

    let evidenceBoundary (charge: string) (workRecord: string) : obj =
        box
            {| charge = charge
               workRecord = workRecord
               authorityTransferred = false |}

    let retryDisposition (outcomes: string array) : obj =
        if outcomes |> Array.exists ((=) "Completed") then
            box
                {| result = "WorkRecord"
                   childLocalFailures = 0
                   callerFailure = false |}
        elif outcomes |> Array.exists ((=) "RetryAvailable") then
            box
                {| result = "ChildLocalRetry"
                   childLocalFailures = outcomes |> Array.filter ((=) "TurnFailed") |> Array.length
                   callerFailure = false |}
        else
            box
                {| result = "ExhaustedFailure"
                   childLocalFailures = outcomes |> Array.filter ((=) "TurnFailed") |> Array.length
                   callerFailure = true |}

    let dispose (value: obj) : unit =
        unbox<Harness> value |> fun harness -> harness.Dispose()
