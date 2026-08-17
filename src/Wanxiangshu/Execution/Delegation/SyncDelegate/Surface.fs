namespace Wanxiangshu.Execution.Delegation.SyncDelegate

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode
open Wanxiangshu.OpenCode.Host
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Persistence.Journal

/// Delegation-owned opaque runtime harness. Host sessions, journal writers,
/// attached-session state and completion turns never cross into JS; callers
/// observe only invocation promises and child identities.
[<RequireQualifiedAccess>]
module SyncDelegateSurface =
    type private Harness
        (
            journal: AgentJournal,
            runtime: SyncDelegateRuntime,
            scope: ToolRuntimeScope,
            children: ResizeArray<SessionId>,
            answers: Dictionary<string, string>,
            ownerPrefix: string
        ) =
        member _.Journal = journal
        member _.Runtime = runtime
        member _.Scope = scope
        member _.Children = children
        member _.Answers = answers
        member _.OwnerSession(owner: string) = SessionId.create (ownerPrefix + owner)

        member _.Dispose() =
            runtime.Dispose()
            (scope :> IDisposable).Dispose()
            (journal :> IDisposable).Dispose()

    type private SessionPort(children: ResizeArray<SessionId>) =
        interface ISessionHostPort with
            member _.SubscribeTerminal(_, _) =
                { new IDisposable with
                    member _.Dispose() = () }

            member _.SendPrompt(_, _, _) =
                Task.FromResult(SendOutcome.AdmittedWithPhysicalMessage(PhysicalUserMessageId.create "msg-physical"))

            member _.AbortSession _ = Task.FromResult(Ok())
            member _.InterruptSessionOnly _ = Task.FromResult(Ok())
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

    [<Emit("setImmediate($0)")>]
    let private queueImmediate (callback: unit -> unit) : unit = jsNative

    let private nextTurn () : Task<unit> =
        let tcs =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        queueImmediate (fun () -> AsyncSupport.trySetResult tcs () |> ignore)
        tcs.Task

    let private waitForReadyCall
        (runtime: SyncDelegateRuntime)
        (owner: SessionId)
        (role: SyncDelegateRole)
        : Task<SessionId option> =
        let rec loop remaining =
            task {
                match runtime.TryFind(owner, role) with
                | Some child when runtime.HasOpeningCursor child -> return Some child
                | _ when remaining <= 0 -> return None
                | _ ->
                    do! nextTurn ()
                    return! loop (remaining - 1)
            }

        loop 1000

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
            let children = ResizeArray<SessionId>()
            let sessions = SessionPort(children) :> ISessionHostPort
            let dispatcher = PromptDispatcher.Runtime(journal)
            let attached = new AttachedSessionRuntime()
            let gate = new SessionQuiescenceGate()

            let answers = Dictionary<string, string>()

            let workRecordFor (sessionId: SessionId) (_range: MagicTodoLwr.BoundedRange) =
                task {
                    match answers.TryGetValue(SessionId.value sessionId) with
                    | true, value when not (String.IsNullOrWhiteSpace value) -> return Some value
                    | _ -> return None
                }

            let runtime =
                new SyncDelegateRuntime(
                    sessions,
                    dispatcher,
                    journal,
                    attached,
                    (fun _ -> Some AgentTier.Fast),
                    (fun _ _ -> ()),
                    gate,
                    directory,
                    workRecordFor = workRecordFor
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

            let ownerPrefix = sprintf "sync-delegate-surface-%s-" (ToolHostCodec.digest directory)
            return box (Harness(journal, runtime, scope, children, answers, ownerPrefix))
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

    /// Settle the current managed child through the real HandleTurn path.
    let settle (value: obj) (owner: string) (role: string) (answer: string) (runId: string) : Task<bool> =
        task {
            let harness = unbox<Harness> value

            match roleOf role with
            | Error _ -> return false
            | Ok role ->
                match! waitForReadyCall harness.Runtime (harness.OwnerSession owner) role with
                | None -> return false
                | Some child ->
                    let turn =
                        { SessionId = child
                          PhysicalUserMessageId = PhysicalUserMessageId.create "msg-physical"
                          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "msg-root"
                          ProviderRun = ProviderRunIdentity.create runId
                          Role = Some(roleValue role)
                          Directory = None
                          Parts = [||]
                          Finish = Some "stop"
                          ErrorName = None
                          Model = None
                          Outcome = ReconcileProgram.TurnCompleted
                          Observation = None }

                    harness.Answers.[SessionId.value child] <- answer
                    return! harness.Runtime.HandleTurn(turn, None)
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
                match! waitForReadyCall harness.Runtime (harness.OwnerSession owner) role with
                | None -> return false
                | Some child ->
                    if outcomeName = "TurnCompleted" then
                        harness.Answers.[SessionId.value child] <- answer

                    let turn =
                        { SessionId = child
                          PhysicalUserMessageId = PhysicalUserMessageId.create "msg-physical"
                          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create "msg-root"
                          ProviderRun = ProviderRunIdentity.create runId
                          Role = Some(roleValue role)
                          Directory = None
                          Parts = [||]
                          Finish = Some "stop"
                          ErrorName = None
                          Model = None
                          Outcome = outcome
                          Observation = None }

                    return! harness.Runtime.HandleTurn(turn, None)
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
