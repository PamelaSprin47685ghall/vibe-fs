namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Microsoft.FSharp.Control
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Execution.Session.Recovery.SessionRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Host
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Persistence.Journal
open Fable.Core
open Wanxiangshu.Mission.Lifecycle


[<RequireQualifiedAccess>]
module private HostPtyOwnership =
    type Owner =
        { Execute: string -> obj -> DurableWorkHost.HostReceipt
          MayStart: DurableWorkHost.HostReceipt -> DurableWorkHost.OwnershipLease option
          Blockers: string -> DurableWorkHost.OwnershipBlocker list }

    let create (commonDir: string option) (registryId: string) : Owner =
        commonDir
        |> Option.iter (fun directory ->
            if String.IsNullOrWhiteSpace directory then
                invalidArg (nameof commonDir) "PTY ownership common directory is required")

        match commonDir with
        | Some directory ->
            { Execute = DurableWorkHost.executeOwnershipDurable directory registryId
              MayStart = DurableWorkHost.mayStartResource
              Blockers = DurableWorkHost.ownershipBlockersDurable directory registryId }
        | None ->
            let ownership = DurableWorkHost.createOwnership registryId [||]

            { Execute = DurableWorkHost.executeOwnershipInMemory ownership
              MayStart = DurableWorkHost.mayStartInMemoryResource
              Blockers = DurableWorkHost.ownershipBlockersInMemory ownership }

    let private requireObserved (kind: string) (receipt: DurableWorkHost.HostReceipt) =
        if
            receipt.Kind <> kind
            || not (List.contains receipt.Status [ "Committed"; "Applied"; "Observed"; "Replayed" ])
        then
            invalidOp (sprintf "PTY ownership %s was not acknowledged: %s %s" kind receipt.Status receipt.Reason)

    let openScope (ownership: Owner) (scopeId: string) (parentId: string option) =
        DurableWorkHost.ownershipMessage
            "OpenScope"
            [ "scopeId", box scopeId
              "parentId", parentId |> Option.map box |> Option.defaultValue null ]
        |> ownership.Execute("open:" + scopeId)
        |> requireObserved "ScopeOpened"

    let reserve (ownership: Owner) (scopeId: string) (ptyId: string) =
        try
            let receipt =
                DurableWorkHost.ownershipMessage
                    "Reserve"
                    [ "resourceId", box ("pty:" + ptyId)
                      "scopeId", box scopeId
                      "kind", box "process" ]
                |> ownership.Execute("reserve:pty:" + ptyId)

            ownership.MayStart receipt
            |> Option.map Ok
            |> Option.defaultValue (
                Error(sprintf "PTY reservation did not grant launch permission: %s %s" receipt.Status receipt.Reason)
            )
        with ex ->
            Error ex.Message

    let drain (ownership: Owner) (scopeId: string) =
        DurableWorkHost.ownershipMessage "Drain" [ "scopeId", box scopeId ]
        |> ownership.Execute("drain:" + scopeId)
        |> requireObserved "Draining"

    let cancel (ownership: Owner) (ptyId: string) (lease: DurableWorkHost.OwnershipLease) =
        let receipt =
            DurableWorkHost.ownershipMessage "Cancel" [ "lease", DurableWorkHost.leaseObject lease ]
            |> ownership.Execute("cancel:pty:" + ptyId)

        requireObserved
            (if receipt.Kind = "Settled" then
                 "Settled"
             else
                 "CancellationRequested")
            receipt

    let ended
        (ownership: Owner)
        (ptyId: string)
        (lease: DurableWorkHost.OwnershipLease)
        (incarnationId: string)
        (outcome: string)
        =
        DurableWorkHost.ownershipMessage
            "Ended"
            [ "lease", DurableWorkHost.leaseObject lease
              "binding", DurableWorkHost.bindingObject ptyId incarnationId
              "outcome", box outcome ]
        |> ownership.Execute("ended:pty:" + ptyId + ":" + incarnationId)
        |> requireObserved "Ended"

    let notStarted (ownership: Owner) id lease incarnation reason =
        DurableWorkHost.ownershipMessage
            "NotStarted"
            [ "lease", DurableWorkHost.leaseObject lease; "reason", box reason ]
        |> ownership.Execute("not-started:pty:" + id + ":" + incarnation)
        |> requireObserved "NotStarted"

    let settleLocal ownership id lease incarnation =
        function
        | PtyExitEvent.NotStarted(_, reason) -> notStarted ownership id lease incarnation reason
        | PtyExitEvent.Exited _ -> ended ownership id lease incarnation "Completed"
        | PtyExitEvent.Failed _ -> ended ownership id lease incarnation "Failed"
        | PtyExitEvent.Aborted _ -> ended ownership id lease incarnation "Cancelled"

    let unsettledIds (ownership: Owner) (scopeId: string) : string list =
        ownership.Blockers scopeId
        |> List.map (fun item ->
            match item.ExternalId with
            | Some id -> id
            | None when item.Lease.ResourceId.StartsWith("pty:", StringComparison.Ordinal) ->
                item.Lease.ResourceId.Substring 4
            | None -> invalidOp "PTY ownership contains an invalid resource identity")

    let runScopeId root runId =
        root + ":run:" + LogicalRunId.value runId

    let openRun ownership root runId =
        let scopeId = runScopeId root runId
        openScope ownership root None
        openScope ownership scopeId (Some root)
        scopeId

    let runBlockers (ownership: Owner) root runId =
        let scopeId = runScopeId root runId

        ownership.Blockers root
        |> List.filter (fun blocker ->
            blocker.ScopeId = scopeId
            || blocker.ScopeId.StartsWith(scopeId + ":runtime:", StringComparison.Ordinal)
            || not (blocker.ScopeId.StartsWith(root + ":run:", StringComparison.Ordinal)))

    let requireRunSettled ownership root runId =
        if not (List.isEmpty (runBlockers ownership root runId)) then
            invalidOp "PTY ownership has no local process binding for unsettled resources; exit was not observed"

    let recoveryBinding (commonDir: string) (lease: DurableWorkHost.OwnershipLease) incarnation : PtyRecoveryBinding =
        let key =
            [| lease.RegistryId; lease.ResourceId; lease.Token |]
            |> JS.JSON.stringify
            |> HostDigest.sha256Hex

        { Directory = commonDir.TrimEnd('/') + "/wanxiangshu/pty/" + key
          Key = key
          IncarnationId = incarnation }

    let private settleRecovered ownership id lease result =
        match result with
        | PtyRecoveryExit.Ended(incarnation, stopped, _, _) ->
            ended ownership id lease incarnation (if stopped then "Cancelled" else "Completed")
        | PtyRecoveryExit.NotStarted(incarnation, reason) -> notStarted ownership id lease incarnation reason

    let private recoverBlocker (recover: RecoverPty) ownership commonDir (blocker: DurableWorkHost.OwnershipBlocker) =
        task {
            try
                if
                    blocker.Kind <> "process"
                    || not (blocker.Lease.ResourceId.StartsWith("pty:", StringComparison.Ordinal))
                then
                    invalidOp "PTY recovery requires its exact process resource"

                let id = blocker.Lease.ResourceId.Substring 4
                let binding = recoveryBinding commonDir blocker.Lease ""

                let! result = recover binding.Directory binding.Key blocker.IncarnationId PtyOutcome.termToKillGraceMs

                settleRecovered ownership id blocker.Lease result
                return None
            with error ->
                return
                    Some(
                        InvalidOperationException(
                            "PTY ownership has no local process binding or verifiable exit: "
                            + error.Message
                        )
                        :> exn
                    )
        }

    let recoverRun (recover: RecoverPty) ownership commonDir root runId localIds =
        task {
            let failures = ResizeArray<exn>()
            let localResources = localIds |> List.map (fun id -> "pty:" + id)

            let unbound =
                runBlockers ownership root runId
                |> List.filter (fun blocker -> not (List.contains blocker.Lease.ResourceId localResources))

            for blocker in unbound do
                let! failure = recoverBlocker recover ownership commonDir blocker
                failure |> Option.iter failures.Add

            failures |> Seq.tryHead |> Option.iter raise
        }

type private OwnedPty =
    { Lease: DurableWorkHost.OwnershipLease
      IncarnationId: string
      RunId: LogicalRunId option }

[<RequireQualifiedAccess>]
module private PtyHostHelpers =
    let bindTerminalName (gate: obj) (terminalByName: Dictionary<string, string>) (name: string) (id: PtyId) =
        lock gate (fun () ->
            match terminalByName.TryGetValue(name.Trim()) with
            | true, existing when existing <> id.Value ->
                Error(sprintf "Terminal name '%s' is already in use" (name.Trim()))
            | _ ->
                terminalByName.[name.Trim()] <- id.Value
                Ok())

    let ptyByName
        (gate: obj)
        (terminalByName: Dictionary<string, string>)
        (ownsPtyId: string -> bool)
        (known: PtyId -> bool)
        (name: string)
        =
        lock gate (fun () ->
            match terminalByName.TryGetValue(name.Trim()) with
            | true, id when ownsPtyId id && known (PtyId.Create id) -> Some(PtyId.Create id)
            | _ -> None)

    let ensureLf (prompt: string) =
        if
            prompt.EndsWith("\n", StringComparison.Ordinal)
            || prompt.EndsWith("\r", StringComparison.Ordinal)
        then
            prompt
        else
            prompt + "\n"

    let emptyRead (id: PtyId) : PtyRead =
        { Id = id; Output = ""; Closed = false }

    let mapRead (id: PtyId) (output: string, closed: bool) : PtyRead =
        { Id = id
          Output = output
          Closed = closed }

    let tryTerminalNameByPtyId (gate: obj) (terminalByName: Dictionary<string, string>) (ptyId: string) =
        lock gate (fun () ->
            terminalByName
            |> Seq.tryPick (fun (KeyValue(name, id)) -> if id = ptyId then Some name else None))

type private PromptAcceptanceState =
    | Open of ResizeArray<IDisposable>
    | Closed

type private PromptAcceptanceScope(notify: PhysicalUserMessageId -> unit, onClosed: PromptAcceptanceScope -> unit) as this
    =
    let gate = obj ()
    // DSL-MUTABLE: resource — registrations belong to one observed Fork prompt.
    let mutable state = Open(ResizeArray<IDisposable>())

    let disposeRegistrations (owned: IDisposable array) =
        for registration in owned do
            registration.Dispose()

    let notifyAndClose physical =
        try
            notify physical
        finally
            this.Dispose()

    member _.Dispose() =
        let resources =
            lock gate (fun () ->
                match state with
                | Closed -> None
                | Open resources ->
                    state <- Closed
                    Some(resources.ToArray()))

        resources
        |> Option.iter (fun owned ->
            try
                disposeRegistrations owned
            finally
                onClosed this)

    member _.Observer: ContinuationAcceptanceObserver =
        { Notify =
            fun physical ->
                lock gate (fun () ->
                    match state with
                    | Closed -> ()
                    | Open _ -> notifyAndClose physical)
          AttachDisposable =
            fun registration ->
                let release =
                    lock gate (fun () ->
                        match state with
                        | Closed -> true
                        | Open resources ->
                            resources.Add registration
                            false)

                if release then
                    registration.Dispose() }

/// Bridges real child sessions to the existing completion mailbox.
/// Fork / Reuse / Pty operations live in extension files (semantic split).
type HostForkRuntime
    (
        parentId: SessionId,
        sessions: ISessionHostPort,
        childWorkRecordForRun: SessionId -> XTraceRange -> ProviderRunIdentity -> Task<string option>,
        createMailbox: obj -> ForkCompletionMailbox,
        clock: IClockPort,
        raceExit: Task -> int -> Task<bool>,
        ptyPort: PtyPort option,
        recoverPty: RecoverPty,
        ?journal: AgentJournal,
        ?onChildCreated: string -> Role -> SessionId -> unit,
        ?onChildCreatedDir: string -> SessionId -> string option -> unit,
        ?directoryFor: string -> string option,
        ?onRunStarted: SessionId -> Role -> string option -> unit,
        ?parentWorkRecordFor: SessionId -> Task<string option>,
        ?childWorkRecordFor: SessionId -> Task<string option>,
        ?handoff: ReusableHandoffPort,
        ?sessionSnapshot: ISessionSnapshotPort,
        ?cancelSignals: SessionId seq -> unit,
        /// Ownership of every handle this runtime forks. Host-owned hidden
        /// children stay outside the parent's list/join/recovery surface.
        ?ownership: HandleOwnership,
        /// Optional capability to drain child PTYs upon run completion (e.g. for DevOps session).
        ?drainChildPtys: AdmittedWork -> Task<unit>,
        ?ownershipCommonDir: string
    ) as this =
    let clockPort = clock
    let runtime = ForkRuntimeBackend.create clockPort raceExit createMailbox
    // DSL-MUTABLE: resource — live child session registry by agent id
    let children = Dictionary<string, SessionId>()
    // DSL-MUTABLE: resource — process-owned agent handle set
    let processOwnedAgents = HashSet<string>()

    /// participant-horizon-011 / WP-025: agents this process established (fork,
    /// adoption, dormant activation) stay established even after their runtime is
    /// replaced by a cancellation. This is process-local and never persisted: a
    /// restarted process still sees nothing from the previous one.
    let processEstablishedAgents = HashSet<string>()
    // DSL-MUTABLE: resource — dormant /continue child registry by agent id
    let dormantChildren = Dictionary<string, SessionId>()
    // DSL-MUTABLE: resource — pending host run registry by agent id
    let pendingRuns = Dictionary<string, PendingHostRun>()
    /// Provider TerminalName → PtyId. Occupied until backend exit.
    // DSL-MUTABLE: resource — terminal name to PtyId map
    let terminalByName = Dictionary<string, string>()
    let ptyCompletionObservers = ResizeArray<PtyJoinItem -> unit>()
    let bufferedJoinItems = Queue<JoinItem>()
    let gate = obj ()
    // DSL-MUTABLE: resource — local acceptance scopes, including durable-Pending prompts without a run yet.
    let mutable promptAcceptanceScopes =
        Some(Dictionary<string, ResizeArray<PromptAcceptanceScope>>())

    let cancelGate = obj ()
    let ownedWorkGate = obj ()
    // DSL-MUTABLE: single-flight — duplicate joins fail before waiting
    let mutable joinInFlight = false
    // DSL-MUTABLE: single-flight — current parent cancellation episode.
    let mutable cancelTask: Task option = None
    // DSL-MUTABLE: single-flight — process-local observer shutdown.
    let mutable detachTask: Task option = None
    // DSL-MUTABLE: resource — terminal/failure callback admission latch.
    let mutable acceptingOwnedWork = true
    // DSL-MUTABLE: resource — in-flight runtime-owned callback count.
    let mutable ownedWorkCount = 0
    // DSL-MUTABLE: single-flight — shared waiter for callback drain.
    let mutable ownedWorkDrainWaiter: TaskCompletionSource<unit> option = None
    // DSL-MUTABLE: resource — first runtime-owned callback failure for shutdown propagation.
    let mutable ownedWorkFailure: exn option = None
    // DSL-MUTABLE: resource — acknowledgement for observed callbacks without closing admission.
    let mutable observedWorkWaiter: TaskCompletionSource<unit> option = None

    let removePromptAcceptanceScope agentId scope =
        lock gate (fun () ->
            match
                promptAcceptanceScopes
                |> Option.bind (fun registry ->
                    match registry.TryGetValue agentId with
                    | true, scopes -> Some(registry, scopes)
                    | false, _ -> None)
            with
            | Some(registry, scopes) ->
                scopes.Remove scope |> ignore

                if scopes.Count = 0 then
                    registry.Remove agentId |> ignore
            | None -> ())

    let addPromptAcceptanceScope (registry: Dictionary<string, ResizeArray<PromptAcceptanceScope>>) agentId scope =
        let scopes =
            match registry.TryGetValue agentId with
            | true, current -> current
            | false, _ ->
                let created = ResizeArray<PromptAcceptanceScope>()
                registry.[agentId] <- created
                created

        scopes.Add scope

    let releaseRejectedPrompt (scope: PromptAcceptanceScope) outcome =
        match outcome with
        | HostForkRunLifecycle.AgentOwnerDispatchOutcome.Rejected _ -> scope.Dispose()
        | _ -> ()

    let observePromptSend agentId onAccepted send =
        let scope = PromptAcceptanceScope(onAccepted, removePromptAcceptanceScope agentId)

        let registered =
            lock gate (fun () ->
                match promptAcceptanceScopes with
                | None -> false
                | Some registry ->
                    addPromptAcceptanceScope registry agentId scope
                    true)

        let rejected () =
            scope.Dispose()
            Task.FromResult(HostForkRunLifecycle.AgentOwnerDispatchOutcome.Rejected "Fork runtime observers are closed")

        let sendObserved () =
            task {
                try
                    let! outcome = send scope.Observer
                    releaseRejectedPrompt scope outcome
                    return outcome
                with error ->
                    scope.Dispose()
                    return raise error
            }

        if registered then sendObserved () else rejected ()

    let closePromptAcceptanceScopes stopping predicate =
        let scopes =
            lock gate (fun () ->
                let current = promptAcceptanceScopes

                if stopping then
                    promptAcceptanceScopes <- None

                current
                |> Option.map (fun registry ->
                    registry
                    |> Seq.filter (fun (KeyValue(agentId, _)) -> predicate agentId)
                    |> Seq.collect (fun (KeyValue(_, scopes)) -> scopes)
                    |> Seq.toArray)
                |> Option.defaultValue [||])

        for scope in scopes do
            scope.Dispose()

    let observedWorkTask () =
        match observedWorkWaiter with
        | Some waiter -> waiter.Task
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            observedWorkWaiter <- Some waiter
            waiter.Task

    let finishOwnedWork () =
        lock ownedWorkGate (fun () ->
            ownedWorkCount <- ownedWorkCount - 1

            if ownedWorkCount = 0 then
                observedWorkWaiter
                |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore)

                observedWorkWaiter <- None

            if not acceptingOwnedWork && ownedWorkCount = 0 then
                ownedWorkDrainWaiter
                |> Option.iter (fun waiter -> AsyncSupport.trySetResult waiter () |> ignore))

    let recordOwnedWorkFailure (failure: exn) =
        lock ownedWorkGate (fun () -> ownedWorkFailure <- Option.orElse ownedWorkFailure (Some failure))

    let captureOwnedWorkFailure (work: unit -> Task) : Task<exn option> =
        task {
            try
                do! work ()
                return None
            with ex ->
                return Some ex
        }

    let observeOwnedWork (work: unit -> Task) : Task =
        task {
            let! failure = captureOwnedWorkFailure work
            failure |> Option.iter recordOwnedWorkFailure
            finishOwnedWork ()
        }
        :> Task

    let startOwnedWork (work: unit -> Task) : Task =
        let admitted =
            lock ownedWorkGate (fun () ->
                if not acceptingOwnedWork then
                    false
                else
                    ownedWorkCount <- ownedWorkCount + 1
                    true)

        if admitted then
            observeOwnedWork work
        else
            Task.FromResult(()) :> Task

    let ownedWorkDrainTask () : Task =
        match ownedWorkDrainWaiter with
        | Some waiter -> waiter.Task :> Task
        | None ->
            let waiter =
                TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

            ownedWorkDrainWaiter <- Some waiter
            waiter.Task :> Task

    let stopOwnedWorkAndDrain () : Task =
        let waiting =
            lock ownedWorkGate (fun () ->
                acceptingOwnedWork <- false

                if ownedWorkCount = 0 then
                    Task.FromResult(()) :> Task
                else
                    ownedWorkDrainTask ())

        task {
            do! waiting

            match lock ownedWorkGate (fun () -> ownedWorkFailure) with
            | Some failure -> return raise failure
            | None -> return ()
        }
        :> Task

    // DSL-MUTABLE: resource — first prompts deferred until the review barrier
    // has durably opened (GLORY-040: barrier before assignment).
    let deferredFirstPrompts =
        Dictionary<
            string,
            {| ChildId: SessionId
               IdentitySeed: PromptAuthority.IdentitySeed
               Prompt: string |}
         >()

    let directoryOf = defaultArg directoryFor (fun _ -> None)
    let childCreated = defaultArg onChildCreated (fun _ _ _ -> ())
    let childCreatedDir = defaultArg onChildCreatedDir (fun _ _ _ -> ())
    let runStarted = defaultArg onRunStarted (fun _ _ _ -> ())

    let parentWorkRecordOf =
        defaultArg parentWorkRecordFor (fun _ -> Task.FromResult None)

    let childWorkRecordOf =
        defaultArg childWorkRecordFor (fun _ -> Task.FromResult None)

    let childWorkRecordOfRun = childWorkRecordForRun

    let handoffPort = handoff

    let xTraceOf (sessionId: SessionId) =
        journal
        |> Option.bind (fun durable ->
            AgentJournal.snapshot durable
            |> fun snapshot -> AgentProjection.tryFind sessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.XTrace))

    let xTraceHead (sessionId: SessionId) : XTraceCursor =
        xTraceOf sessionId
        |> Option.defaultValue XTraceProjection.empty
        |> XTraceProjection.headCursor

    let completedFrontier sessionId providerRun =
        xTraceOf sessionId
        |> Option.bind (XTraceProjection.terminalEvidenceForProviderRun providerRun)
        |> Option.map (fun terminal -> terminal.Frontier)

    let prepareCompletion (run: PendingHostRun) (outcome: TerminalOutcome) =
        task {
            match run.Role, drainChildPtys with
            | Role.DevOps, Some drain ->
                let work =
                    run.Work
                    |> Option.defaultWith (fun () -> invalidOp "DevOps PTY drain requires admitted work")

                do! drain work
            | Role.DevOps, None -> invalidOp "DevOps PTY round drain is unavailable"
            | _ -> ()

            return! HostForkRunLifecycle.workRecordForOutcome childWorkRecordOfRun completedFrontier run outcome
        }

    let cancelSignals = defaultArg cancelSignals (fun _ -> ())

    let requirePtyPort () =
        ptyPort
        |> Option.defaultWith (fun () -> invalidOp "PTY physical capability is unavailable")

    let parentKey = SessionId.value parentId

    let ptyRootScopeId = parentKey
    let ptyRuntimeId = Guid.NewGuid().ToString("N")
    let ptyScopeId = parentKey + ":runtime:" + ptyRuntimeId
    // DSL-MUTABLE: resource — scopes owned by this runtime, including reservations with lost acknowledgements.
    let ptyOwnedScopes = HashSet<string>([ ptyScopeId ])
    // DSL-MUTABLE: resource — wanxiangli/ownership registry for PTY leases in this runtime
    let ptyOwnership =
        HostPtyOwnership.create ownershipCommonDir ("fork-pty:" + ptyRootScopeId)
    // DSL-MUTABLE: resource — active PTY lease and incarnation binding by PtyId
    let ptyLeases = Dictionary<string, OwnedPty>()
    // DSL-MUTABLE: resource — exact exit observations awaiting an ownership acknowledgement.
    let ptyExitObservations = Dictionary<string, PtyExitEvent>()
    // DSL-MUTABLE: resource — prevents new local PTYs during a run's exit drain.
    let mutable ptyClosing = false
    // DSL-MUTABLE: resource — permanent detach closes local admission even if its durable Drain fails.
    let mutable ptyDetachRequested = false
    // DSL-MUTABLE: resource — exit subscription retained until the terminal runtime has no leases.
    let mutable ptyExitSubscription: IDisposable option = None

    do
        HostPtyOwnership.openScope ptyOwnership ptyRootScopeId None
        HostPtyOwnership.openScope ptyOwnership ptyScopeId (Some ptyRootScopeId)

    let exactPtyRun expected work =
        let runId = AdmittedWork.logicalRunId work

        if runId = expected then
            Ok(Some runId)
        else
            Error "PTY work does not match canonical authority"

    let admittedPtyRun durable (profile: PromptAuthority.AuthorityExecutionProfile) =
        let projection = (AgentJournal.snapshot durable).AgentProjections

        match PromptIdentitySeed.owner profile.IdentitySeed, Map.tryFind parentId projection.HandleByChildSession with
        | None, _ -> Ok(Some profile.LogicalRunId)
        | Some(owner, _, _), Some binding ->
            HandleProjection.tryAdmittedWork
                { Handle = binding.Handle
                  ChildSessionId = parentId
                  AuthorityRoot = profile.AuthorityRootUserMessageId }
                (AgentJournal.handleProjection durable owner)
            |> Result.mapError (fun _ -> "Exact PTY work is not admitted")
            |> Result.bind (exactPtyRun profile.LogicalRunId)
        | _ -> Error "PTY child has no canonical handle binding"

    let canonicalPtyRun durable (requestedRun: LogicalRunId option) =
        PromptAuthorityProjectionQueries.activeProfile parentId (AgentJournal.snapshot durable).AgentProjections
        |> Option.filter (fun profile -> requestedRun.IsNone || requestedRun = Some profile.LogicalRunId)
        |> Option.map (admittedPtyRun durable)
        |> Option.defaultValue (Error "PTY call does not belong to the active canonical work round")

    let ptyRunFor (requestedRun: LogicalRunId option) =
        match journal with
        | None when requestedRun.IsNone -> Ok None
        | None -> Error "Canonical PTY work is unavailable"
        | Some durable -> canonicalPtyRun durable requestedRun

    let ptyScopeForRun runId =
        match runId with
        | None -> ptyScopeId
        | Some run ->
            let runScope = HostPtyOwnership.openRun ptyOwnership ptyRootScopeId run
            let scopeId = runScope + ":runtime:" + ptyRuntimeId
            HostPtyOwnership.openScope ptyOwnership scopeId (Some runScope)
            ptyOwnedScopes.Add scopeId |> ignore
            scopeId

    let handleOwnership = defaultArg ownership HandleOwnership.DurableParentHandle

    let sendChildPrompt =
        fun agentId childId role identitySeed prompt onAccepted ->
            observePromptSend agentId onAccepted (fun observer ->
                HostForkRunLifecycle.childPromptSender
                    sessions
                    parentId
                    journal
                    directoryOf
                    agentId
                    childId
                    role
                    identitySeed
                    prompt
                    observer)

    let sendBusyNudge = HostForkBusyNudge.sender sessions journal directoryOf

    let parentAbortToken = Pty.registerParentAbort parentKey (fun () -> this.Cancel())

    let notifyPtyObserver observer item =
        try
            observer item
        with _ ->
            ()

    let notifyPtyObservers item =
        let observers = lock gate (fun () -> ptyCompletionObservers |> Seq.toList)

        for observer in observers do
            notifyPtyObserver observer item

    let mapExitEvent (event: PtyExitEvent) : PtyJoinItem =
        match event with
        | PtyExitEvent.Exited(id, outcome) ->
            PtyExited
                { PtyId = id.Value
                  Outcome = outcome
                  Closed = true }
        | PtyExitEvent.Failed(id, code, msg) ->
            PtyFailed
                { PtyId = id.Value
                  Outcome = msg
                  Closed = true
                  Code = code
                  Message = msg }
        | PtyExitEvent.NotStarted(id, reason) ->
            PtyFailed
                { PtyId = id.Value
                  Outcome = reason
                  Closed = true
                  Code = "PTY_NOT_STARTED"
                  Message = reason }
        | PtyExitEvent.Aborted(id, code, msg) ->
            PtyAborted
                { PtyId = id.Value
                  Outcome = msg
                  Closed = true
                  Code = code
                  Message = msg }

    let releasePtyExitSubscription () =
        lock gate (fun () ->
            if (ptyDetachRequested || runtime.IsCancelled) && ptyLeases.Count = 0 then
                ptyExitSubscription |> Option.iter (fun subscription -> subscription.Dispose())
                ptyExitSubscription <- None)

    let settlePtyLease (event: PtyExitEvent) =
        let id = event.Id.Value

        lock gate (fun () ->
            match ptyLeases.TryGetValue id with
            | true, owned ->
                HostPtyOwnership.settleLocal ptyOwnership id owned.Lease owned.IncarnationId event
                ptyLeases.Remove id |> ignore
                ptyExitObservations.Remove id |> ignore
            | false, _ -> ()

            let stale =
                terminalByName
                |> Seq.filter (fun kv -> kv.Value = id)
                |> Seq.map (fun kv -> kv.Key)
                |> Seq.toList

            for name in stale do
                terminalByName.Remove name |> ignore)

        runtime.UnregisterPty id
        releasePtyExitSubscription ()

    let settleObservedPty (id: string) =
        let observed =
            lock gate (fun () ->
                match ptyExitObservations.TryGetValue id with
                | true, value -> Some value
                | false, _ -> None)

        match observed with
        | Some event ->
            settlePtyLease event
            notifyPtyObservers (mapExitEvent event)
        | None -> ()

    do
        ptyExitSubscription <-
            ptyPort
            |> Option.map (fun port ->
                port.AddExitListener(fun exitEvent ->
                    let item = mapExitEvent exitEvent
                    let id = PtyJoinItem.ptyId item
                    let owned = lock gate (fun () -> ptyLeases.ContainsKey id)

                    if owned then
                        // A PtyPort can be shared by multiple runtimes. Its sender fan-out
                        // must not turn another runtime's exit into this runtime's join.
                        lock gate (fun () -> ptyExitObservations.[id] <- exitEvent)
                        runtime.PublishPtyCompletion item
                        settlePtyLease exitEvent
                        notifyPtyObservers item))

    member internal _.Runtime = runtime
    member internal _.Children = children
    member internal _.DormantChildren = dormantChildren
    member internal _.PendingRuns = pendingRuns
    member internal _.PtyRuns = ptyLeases.Keys :> ICollection<string>

    /// Fission admission snapshot: existing external work belongs to the logical
    /// owner and becomes broadcast completion sources. Snapshot identities only;
    /// no completion is consumed here.
    member _.SnapshotOutstandingAgentRuns() =
        lock gate (fun () ->
            pendingRuns.Values
            |> Seq.filter (fun run -> not run.Finished)
            |> Seq.map (fun run -> run.AgentId, run.ChildId)
            |> Seq.toList)

    member _.SnapshotOutstandingPtyRuns() =
        lock gate (fun () -> HostPtyOwnership.unsettledIds ptyOwnership ptyRootScopeId)

    member _.CancelOwnedPtys() =
        lock gate (fun () ->
            for KeyValue(id, owned) in ptyLeases do
                HostPtyOwnership.cancel ptyOwnership id owned.Lease)

    member _.SubscribePtyCompletion(listener: PtyJoinItem -> unit) : IDisposable =
        lock gate (fun () -> ptyCompletionObservers.Add listener)

        { new IDisposable with
            member _.Dispose() =
                lock gate (fun () -> ptyCompletionObservers.Remove listener |> ignore) }

    member internal _.HandleOwnership = handleOwnership
    member internal _.DeferredFirstPrompts = deferredFirstPrompts
    member internal _.Clock = clockPort
    /// Wall-clock read for Session extension modules (avoids raw DateTimeOffset stamps).
    member internal _.Now() = clockPort.UtcNow()

    /// GLORY-045: re-enlist a still-ungraduated historical child into this
    /// runtime before Fork, so Fork's existing-child path reuses the SAME Host
    /// session (X/Y context preserved) instead of creating a second one.
    member internal _.AdoptChild(agentId: string, childId: SessionId) : unit =
        lock gate (fun () ->
            children.[agentId] <- childId
            processOwnedAgents.Add agentId |> ignore
            processEstablishedAgents.Add agentId |> ignore)

    member internal _.ObservePromptSend
        (
            agentId: string,
            onAccepted: PhysicalUserMessageId -> unit,
            send: ContinuationAcceptanceObserver -> Task<HostForkRunLifecycle.AgentOwnerDispatchOutcome>
        ) =
        observePromptSend agentId onAccepted send

    /// GLORY-040: deliver a first prompt that was deferred until its review
    /// barrier had durably opened. Idempotent per agent id: a second call with
    /// nothing pending is a no-op success.
    member this.SendDeferredFirstPrompt(agentId: string) : Task<Result<unit, string>> =
        let pendingRunForAgent () =
            lock gate (fun () ->
                match pendingRuns.TryGetValue agentId with
                | true, run -> Some run
                | false, _ -> None)

        let deliverDeferredPrompt
            (pending:
                {| ChildId: SessionId
                   IdentitySeed: PromptAuthority.IdentitySeed
                   Prompt: string |})
            =
            task {
                let! sent =
                    observePromptSend agentId ignore (fun observer ->
                        HostForkAgentOwner.sendFirstPromptObserved
                            this.Sessions
                            this.Journal
                            pending.ChildId
                            pending.IdentitySeed
                            (this.DirectoryOf agentId)
                            pending.Prompt
                            observer)

                return
                    match sent with
                    | HostForkRunLifecycle.AgentOwnerDispatchOutcome.Accepted _ ->
                        lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)
                        Ok()
                    | HostForkRunLifecycle.AgentOwnerDispatchOutcome.AcceptanceUncertain _ ->
                        // Never resend an outcome-unknown first prompt. Durable
                        // PromptAuthority recovery owns the pending claim now.
                        lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)
                        Ok()
                    | HostForkRunLifecycle.AgentOwnerDispatchOutcome.Rejected err -> Error err
            }

        task {
            let pendingOpt =
                lock gate (fun () ->
                    match deferredFirstPrompts.TryGetValue agentId with
                    | true, pending -> Some pending
                    | false, _ -> None)

            match pendingOpt with
            | None -> return Ok()
            | Some pending -> return! deliverDeferredPrompt pending
        }

    /// Drop a deferSend preamble so process-review assignment can go out as
    /// AgentOwnerRoot instead of a busy nudge against a session with no profile.
    member _.DiscardDeferredFirstPrompt(agentId: string) : unit =
        lock gate (fun () -> deferredFirstPrompts.Remove agentId |> ignore)

    member internal _.Gate = gate
    member internal _.TerminalByName = terminalByName
    member internal _.Sessions = sessions
    member internal _.Journal = journal
    member internal _.SessionSnapshot = sessionSnapshot
    member internal _.ParentId = parentId
    member internal _.ParentKey = parentKey

    /// EXEC-017/018: one join waiter owns the runtime wake channels at a time.
    /// Join workflow (HostForkJoin) acquires/releases through these, so the
    /// latch stays with the state it guards.
    member internal _.TryAcquireJoin() : bool =
        lock gate (fun () ->
            if joinInFlight then
                false
            else
                joinInFlight <- true
                true)

    member internal _.ReleaseJoin() =
        lock gate (fun () -> joinInFlight <- false)

    member internal _.PtyPort = requirePtyPort ()
    member internal _.DirectoryOf = directoryOf
    member internal _.RunStarted = runStarted
    member internal _.ChildCreated = childCreated
    member internal _.ChildCreatedDir = childCreatedDir
    member internal _.ParentWorkRecordOf = parentWorkRecordOf
    member internal _.ChildWorkRecordOf = childWorkRecordOf
    member internal _.PrepareCompletion = prepareCompletion
    member internal _.XTraceHead = xTraceHead
    member internal _.HandoffPort = handoffPort

    member internal _.PrepareHandoff(route: DelegationHandoffRoute) : Task<Result<PreparedDelegationHandoff, string>> =
        match handoffPort with
        | Some port ->
            task {
                let! prepared = port.Prepare parentId route
                return Ok prepared
            }
        | None -> Task.FromResult(Error "reusable delegation handoff capability is unavailable")

    member internal _.TrackOwnedWork(work: unit -> Task) = startOwnedWork work |> ignore
    member internal _.SendChildPrompt = sendChildPrompt
    member internal _.SendBusyNudge = sendBusyNudge
    member internal _.ParentAbortToken = parentAbortToken

    member private _.DetachPendingObservers() =
        closePromptAcceptanceScopes true (fun _ -> true)

        let subscriptions =
            lock gate (fun () ->
                let values =
                    pendingRuns.Values
                    |> Seq.choose (fun run ->
                        run.Finished <- true
                        run.Subscription)
                    |> Seq.toList

                pendingRuns.Clear()
                children.Clear()
                dormantChildren.Clear()
                deferredFirstPrompts.Clear()

                values)

        subscriptions |> List.iter (fun subscription -> subscription.Dispose())

    member private this.DrainCancelledCallbacks(agentIds: string list, retainedAgents: string list) : Task<unit> =
        let cancelled = Set.ofList agentIds
        closePromptAcceptanceScopes false (fun agentId -> Set.contains agentId cancelled)

        let subscriptions =
            lock gate (fun () ->
                pendingRuns.Values
                |> Seq.filter (fun run -> Set.contains run.AgentId cancelled)
                |> Seq.choose (fun run ->
                    let subscription = run.Subscription
                    run.Subscription <- None
                    subscription)
                |> Seq.toList)

        subscriptions |> List.iter (fun subscription -> subscription.Dispose())

        task {
            if List.isEmpty retainedAgents then
                do! stopOwnedWorkAndDrain ()
            else
                do! this.AwaitObservedWork()
        }

    /// EXEC-009: retired OR abandoned ids must never re-fork under the same handle.
    member _.IsRetiredHandle(agentId: string) =
        journal
        |> Option.map (fun durable ->
            let projection = AgentJournal.handleProjection durable parentId
            let handle = HandleController.agentHandle agentId

            HandleProjection.isRetired handle projection
            || HandleProjection.isAbandoned handle projection)

    member this.Complete(run: PendingHostRun, outcome: TerminalOutcome) =
        // Terminal delivery is a synchronous Host callback. The runtime owns the
        // async tail so shutdown can drain it before releasing the Journal.
        startOwnedWork (fun () ->
            HostForkRunLifecycle.complete gate pendingRuns journal parentId handoffPort prepareCompletion run outcome)
        |> ignore

    member this.InstallRun
        (
            agentId: string,
            childId: SessionId,
            role: Role,
            authorityRoot: AuthorityRootUserMessageId,
            ?preparedHandoff: PreparedDelegationHandoff
        ) =
        lock gate (fun () ->
            processOwnedAgents.Add agentId |> ignore
            processEstablishedAgents.Add agentId |> ignore)

        let run =
            HostForkRunLifecycle.installRun
                gate
                pendingRuns
                journal
                parentId
                sessions
                prepareCompletion
                xTraceHead
                (fun work -> startOwnedWork work |> ignore)
                handoffPort
                preparedHandoff
                agentId
                childId
                role
                authorityRoot

        runtime.BindChildSession(agentId, childId)
        runStarted childId role (directoryOf agentId)
        run

    member this.FailRun(run: PendingHostRun, error: string) : Task =
        startOwnedWork (fun () ->
            HostForkRunLifecycle.failRun gate pendingRuns journal parentId sessions handoffPort run error)

    member this.MarkReady(run: PendingHostRun) =
        // markReady is intentionally a no-op; do not perform an unnecessary WorkRecord read.
        HostForkRunLifecycle.markReady gate pendingRuns journal parentId sessions run None

    member private _.WorkRecordFromCompletion(completion: RunCompletion) : Result<string, string> =
        match completion.Outcome with
        | AgentCompleted payload when not (String.IsNullOrWhiteSpace payload.WorkRecord) -> Ok payload.WorkRecord
        | AgentCompleted _ -> Error "reusable fork completed without bounded delta WorkRecord"
        | AgentFailed payload -> Error payload.Message
        | AgentAbandoned(_, reason) -> Error reason

    member internal this.AwaitCurrentWorkRecord(agentId: string) : Task<Result<string, string>> =
        taskResult {
            let! completion = this.AwaitChild agentId
            return! this.WorkRecordFromCompletion completion
        }

    member private this.CancelParentEpisode(completion: TaskCompletionSource<unit>) : Task =
        task {
            let! failure =
                captureOwnedWorkFailure (fun () ->
                    HostForkChildDispatch.cancelParent
                        cancelSignals
                        (fun cancelled retained -> this.DrainCancelledCallbacks(cancelled, retained))
                        runtime
                        (fun () -> this.ClosePtys(Seq.toList ptyOwnedScopes, None))
                        parentKey
                        parentAbortToken
                        gate
                        pendingRuns
                        children
                        sessions
                        journal
                        (journal
                         |> Option.map (fun durable -> AgentJournal.handleProjection durable parentId))
                        parentId
                        (fun run -> HostForkRunLifecycle.settleParentCancelled gate pendingRuns run)
                        (clockPort.UtcNow())
                    :> Task)

            releasePtyExitSubscription ()
            lock cancelGate (fun () -> cancelTask <- None)

            match failure with
            | None -> completion.SetResult()
            | Some failure -> completion.SetException failure
        }
        :> Task

    member this.CancelAndDrain() : Task =
        lock cancelGate (fun () ->
            match detachTask, cancelTask with
            | Some drain, _
            | _, Some drain -> drain
            | None, None ->
                let completion =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                let drain = completion.Task :> Task
                cancelTask <- Some drain
                this.CancelParentEpisode completion |> ignore
                drain)

    member private this.DetachObserversAfter(cancelling: Task option) : Task =
        // DSL-MUTABLE: algorithm-scratch — actual failures from independent cleanup boundaries.
        let failures = ResizeArray<exn>()

        let release work =
            task {
                let! failure = captureOwnedWorkFailure work
                failure |> Option.iter failures.Add
            }

        task {
            match cancelling with
            | Some cancellation -> do! release (fun () -> cancellation)
            | None -> ()

            do! release stopOwnedWorkAndDrain

            do!
                release (fun () ->
                    runtime.Cancel()
                    AsyncSupport.completedTask ())

            do!
                release (fun () ->
                    this.DetachPendingObservers()
                    AsyncSupport.completedTask ())

            do!
                release (fun () ->
                    for scopeId in ptyOwnedScopes do
                        HostPtyOwnership.drain ptyOwnership scopeId

                    AsyncSupport.completedTask ())

            do! release (fun () -> this.ClosePtys(Seq.toList ptyOwnedScopes, None) :> Task)

            do!
                release (fun () ->
                    Pty.unregisterParentAbort parentKey parentAbortToken
                    AsyncSupport.completedTask ())

            match Seq.toList failures with
            | [] -> return ()
            | [ failure ] -> return raise failure
            | failures ->
                return
                    raise (
                        emitJsExpr
                            (List.toArray failures)
                            "new AggregateError($0, 'fork runtime detach failed', { cause: $0[0] })"
                    )
        }
        :> Task

    /// managed-session-lifecycle-018: plugin/process lifetime ending is not a logical
    /// parent cancellation. Stop this process's observers and local runtime
    /// resources without writing HandleAbandoned and without aborting live Host
    /// child sessions. Durable Active handles remain the restart authority.
    member this.DetachAndDrain() : Task =
        lock cancelGate (fun () ->
            match detachTask with
            | Some drain -> drain
            | None ->
                lock gate (fun () -> ptyDetachRequested <- true)
                let drain = this.DetachObserversAfter cancelTask
                detachTask <- Some drain
                drain)

    member this.Cancel() : unit = this.CancelAndDrain() |> ignore

    member _.List() = runtime.List()

    member _.TryFindAgent(agentId: string) =
        runtime.List() |> fst |> List.tryFind (fun a -> a.AgentId = agentId)

    member internal _.OwnsAgent(agentId: string) =
        lock gate (fun () -> processOwnedAgents.Contains agentId)

    /// participant-horizon-011 / WP-025: established-by-this-process is not
    /// currently-driven-by-this-process. A child cancelled by its own process stays
    /// visible to that process' horizon/join (durable Abandoned, consequence not
    /// yet consumed) without widening OwnsAgent, which remains the driving predicate.
    member internal _.EstablishedByProcess(agentId: string) =
        lock gate (fun () -> processEstablishedAgents.Contains agentId)

    member internal _.EstablishedAgentIds: string list =
        lock gate (fun () -> processEstablishedAgents |> Seq.toList)

    member internal _.RestoreEstablishedAgents(agentIds: string list) =
        lock gate (fun () -> processEstablishedAgents.UnionWith agentIds)

    /// crash-reconciliation-018: explicit /continue may discover a physically surviving child.
    /// It stays dormant: addressable by a later explicit reuse, but excluded from
    /// this process's cancellation/teardown ownership until that reuse begins.
    ///
    /// delegation-026: adoption registers identity only. Restoring here would
    /// plant an active ChildRun with no work behind it, so the next dispatch's
    /// backend Fork answers Nudged, its runTask never starts, and settle's
    /// AwaitCurrentWorkRecord waits on the restored cell forever. The dispatch
    /// itself installs the run that owns the completion cell.
    member _.AdoptExisting(agentId: string, childId: SessionId) : unit =
        lock gate (fun () -> dormantChildren.[agentId] <- childId)

    /// crash-reconciliation-020: the durable handle projection is the single
    /// source of truth for which children this parent still owns. A restarted
    /// process has empty process tables, so every reuse/await path must be able
    /// to resolve a child from it — the in-process maps are only a cache of what
    /// this process currently drives.
    member internal _.TryChildFromDurable(agentId: string) : (SessionId * Role * string) option =
        journal
        |> Option.bind (fun durable ->
            DurableChildLookup.byHandleId (AgentJournal.handleProjection durable this.ParentId) agentId)

    member private this.AdoptDurableChild(agentId: string) : (SessionId * bool) option =
        match this.TryChildFromDurable agentId with
        | None -> None
        | Some(childId, _, _) ->
            this.AdoptExisting(agentId, childId)
            Some(childId, true)

    /// Resolve a child for reuse: process-local registration first, then the
    /// durable handle, which is adopted on demand so placement/await see it.
    member internal this.ReusableChildOrAdopt(agentId: string) : (SessionId * bool) option =
        match this.TryReusableChild agentId with
        | Some found -> Some found
        | None -> this.AdoptDurableChild agentId

    /// Resolve a child through durable evidence when this process has not met it
    /// yet (restart). The handle is the existence evidence; the in-process maps
    /// are only a cache of what this process currently drives.
    member this.TryFindAgentOrAdopt(agentId: string) : (SessionId * Role * string) option =
        match this.TryReusableChild agentId, this.TryChildFromDurable agentId with
        | Some(childId, _), Some(_, role, agent) -> Some(childId, role, agent)
        | Some _, None -> None
        | None, None -> None
        | None, Some(childId, role, agent) ->
            this.AdoptExisting(agentId, childId)
            Some(childId, role, agent)

    /// A parent-visible child exists when this process drives it or when the
    /// durable handle says so — the process tables alone would answer "no" after
    /// every restart.
    member this.HasChild(agentId: string) : bool =
        match this.TryReusableChild agentId, this.TryChildFromDurable agentId with
        | Some _, _ -> true
        | None, Some _ -> true
        | None, None -> false

    member private this.ChildNotReusableOutcome(agentId: string) : Result<RunCompletion, string> =
        match this.TryFindAgentOrAdopt agentId with
        | None -> Error(sprintf "Unknown agent id: %s" agentId)
        | Some _ -> Error(sprintf "No work in flight for %s" agentId)

    /// Await one child's completion. A restarted process has nothing in flight for
    /// a child it only knows from the journal: that answers explicitly instead of
    /// the bare "Unknown agent id" the process tables produced.
    member this.AwaitChild(agentId: string, ?timeoutMs: int) : Task<Result<RunCompletion, string>> =
        task {
            match this.TryReusableChild agentId with
            | Some _ -> return! this.Runtime.AwaitAgent(agentId, ?timeoutMs = timeoutMs)
            | None -> return this.ChildNotReusableOutcome agentId
        }

    member internal _.TryReusableChild(agentId: string) : (SessionId * bool) option =
        lock gate (fun () ->
            match children.TryGetValue agentId, dormantChildren.TryGetValue agentId with
            | (true, childId), _ -> Some(childId, false)
            | _, (true, childId) -> Some(childId, true)
            | _ -> None)

    member internal _.ActivateDormantChild(agentId: string, childId: SessionId, role: Role) : unit =
        lock gate (fun () ->
            dormantChildren.Remove agentId |> ignore
            children.[agentId] <- childId
            processOwnedAgents.Add agentId |> ignore
            processEstablishedAgents.Add agentId |> ignore)

        childCreated agentId role childId

    member internal this.ActivateDormantChildIfNeeded
        (wasDormant: bool, agentId: string, childId: SessionId, role: Role)
        =
        if wasDormant then
            this.ActivateDormantChild(agentId, childId, role)

    /// The Host child session a forked agent id drives.
    ///
    /// ORCH-006 needs it right after a fork, to record `ManagerJobCreated`. The map is
    /// the same one restart recovery repopulates from `HandleLinked.ChildSessionId`, so
    /// a resumed job reads the session the Host actually issued rather than one derived
    /// from the agent id.
    member this.TryChildSession(agentId: string) : SessionId option =
        let local =
            lock gate (fun () ->
                match children.TryGetValue agentId with
                | true, childId -> Some childId
                | false, _ -> None)

        match local with
        | Some childId -> Some childId
        | None -> this.TryChildFromDurable agentId |> Option.map (fun (childId, _, _) -> childId)

    member _.PendingRunCount = lock gate (fun () -> pendingRuns.Count)
    member _.PendingCompletionCount = runtime.PendingCompletionCount
    member _.IsCancelled = runtime.IsCancelled
    member internal _.IsCancelling = lock cancelGate (fun () -> cancelTask.IsSome)

    member this.RegisterPtySnapshot (id: PtyId) (command: string) =
        runtime.RegisterPty
            { PtyId = id.Value
              Command = command
              StartedAt = this.Now() }

    member this.OwnsPty(id: PtyId) =
        lock gate (fun () -> ptyLeases.ContainsKey id.Value)

    member this.AwaitObservedWork() : Task<unit> =
        let waiting =
            lock ownedWorkGate (fun () ->
                if ownedWorkCount = 0 then
                    Task.FromResult(())
                else
                    observedWorkTask ())

        task {
            do! waiting

            match lock ownedWorkGate (fun () -> ownedWorkFailure) with
            | Some failure -> return raise failure
            | None -> return ()
        }

    member this.DrainOwnedWork() : Task<unit> =
        task {
            let! _ = stopOwnedWorkAndDrain ()
            return ()
        }

    member private this.TryClosePty(id: PtyId, graceMs: int option) : Task<exn option> =
        task {
            try
                do! this.ClosePty(id, ?graceMs = graceMs)
                return None
            with ex ->
                return Some ex
        }

    member private this.ClosePtySnapshot(ids: PtyId list, graceMs: int option) : Task<unit> =
        task {
            let failures = ResizeArray<exn>()

            for id in ids do
                let! failure = this.TryClosePty(id, graceMs)
                failure |> Option.iter failures.Add

            if failures.Count > 0 then
                raise failures.[0]
        }

    member private this.DrainPtySnapshot
        (ids: PtyId list, blockerScopeIds: string list, graceMs: int option)
        : Task<unit> =
        task {
            do! this.ClosePtySnapshot(ids, graceMs)

            let remaining =
                lock gate (fun () -> blockerScopeIds |> List.collect (HostPtyOwnership.unsettledIds ptyOwnership))

            if not (List.isEmpty remaining) then
                invalidOp (
                    sprintf
                        "PTY exit was not observed for resources without a local process binding: %s"
                        (String.concat ", " remaining)
                )
        }

    member private this.ClosePtys(blockerScopeIds: string list, graceMs: int option) : Task<unit> =
        task {
            let ids =
                lock gate (fun () ->
                    if ptyClosing then
                        invalidOp "PTY exit drain is already in progress"

                    ptyClosing <- true
                    ptyLeases.Keys |> Seq.map PtyId.Create |> Seq.toList)

            try
                do! this.DrainPtySnapshot(ids, blockerScopeIds, graceMs)
            finally
                lock gate (fun () -> ptyClosing <- false)
                releasePtyExitSubscription ()
        }

    member this.CloseOwnedPtys(?graceMs: int) : Task<unit> =
        this.ClosePtys([ ptyRootScopeId ], graceMs)

    static member internal CloseUnboundPtyRun(recoverPty: RecoverPty, commonDir: string, work: AdmittedWork) =
        task {
            let root = AdmittedWork.id work |> fun work -> SessionId.value work.ChildSessionId
            let runId = AdmittedWork.logicalRunId work
            let ownership = HostPtyOwnership.create (Some commonDir) ("fork-pty:" + root)

            HostPtyOwnership.openRun ownership root runId
            |> HostPtyOwnership.drain ownership

            do! HostPtyOwnership.recoverRun recoverPty ownership commonDir root runId []
            HostPtyOwnership.requireRunSettled ownership root runId
        }

    member internal this.ClosePtyRun(work: AdmittedWork) : Task<unit> =
        task {
            if (AdmittedWork.id work).ChildSessionId <> parentId then
                invalidArg (nameof work) "PTY work belongs to another session"

            let runId = AdmittedWork.logicalRunId work

            let stopFailure =
                try
                    HostPtyOwnership.openRun ptyOwnership ptyRootScopeId runId
                    |> HostPtyOwnership.drain ptyOwnership

                    None
                with error ->
                    Some error

            let ids =
                lock gate (fun () ->
                    ptyLeases
                    |> Seq.choose (fun pair ->
                        if pair.Value.RunId = Some runId then
                            Some(PtyId.Create pair.Key)
                        else
                            None)
                    |> Seq.toList)

            let! closeFailure = captureOwnedWorkFailure (fun () -> this.ClosePtySnapshot(ids, None) :> Task)

            let! recoveryFailure =
                captureOwnedWorkFailure (fun () ->
                    task {
                        match stopFailure, ownershipCommonDir with
                        | None, Some directory ->
                            let localIds = lock gate (fun () -> ptyLeases.Keys |> Seq.toList)

                            do!
                                HostPtyOwnership.recoverRun
                                    recoverPty
                                    ptyOwnership
                                    directory
                                    ptyRootScopeId
                                    runId
                                    localIds
                        | _ -> ()
                    }
                    :> Task)

            match stopFailure, closeFailure, recoveryFailure with
            | Some failure, _, _
            | None, Some failure, _
            | None, None, Some failure -> raise failure
            | None, None, None -> ()

            HostPtyOwnership.requireRunSettled ptyOwnership ptyRootScopeId runId
        }

    member private this.RequestPtyCancellation(id: PtyId) =
        lock gate (fun () ->
            match ptyLeases.TryGetValue id.Value with
            | true, owned -> HostPtyOwnership.cancel ptyOwnership id.Value owned.Lease
            | false, _ when this.SnapshotOutstandingPtyRuns() |> List.contains id.Value ->
                invalidOp (sprintf "PTY %s has no local process binding; exit was not observed" id.Value)
            | false, _ -> ())

    member this.ClosePty(id: PtyId, ?graceMs: int) : Task<unit> =
        task {
            settleObservedPty id.Value

            let cancellationFailure =
                try
                    this.RequestPtyCancellation id
                    None
                with ex ->
                    Some ex

            let! closeFailure =
                captureOwnedWorkFailure (fun () ->
                    task {
                        if this.OwnsPty id then
                            do! (requirePtyPort ()).ClosePty(id, ?graceMs = graceMs)

                        settleObservedPty id.Value

                        if this.OwnsPty id then
                            invalidOp (sprintf "PTY %s exit was not observed; ownership remains unsettled" id.Value)
                    }
                    :> Task)

            match cancellationFailure, closeFailure with
            | Some failure, _
            | None, Some failure -> return raise failure
            | None, None -> return ()
        }

    member this.IsPtyCompletion(runId: string) = this.OwnsPty(PtyId.Create runId)

    member this.TryBindTerminalName(name: string, id: PtyId) : Result<unit, string> =
        if String.IsNullOrWhiteSpace name then
            Error "Terminal name is required"
        elif not (this.OwnsPty id) then
            Error(sprintf "PTY %s has already exited or is not owned by this runtime" id.Value)
        else
            PtyHostHelpers.bindTerminalName gate terminalByName name id

    member this.TryPtyByName(name: string) : PtyId option =
        if String.IsNullOrWhiteSpace name then
            None
        else
            PtyHostHelpers.ptyByName
                gate
                terminalByName
                (fun id -> ptyLeases.ContainsKey id)
                (fun id -> ptyPort |> Option.exists (fun port -> port.Known id))
                name

    member private this.SpawnReservedPty
        (id: PtyId, command: string, agent: ManagedAgent, cwd: string option)
        : Result<PtyId, string> =
        try
            let owned = lock gate (fun () -> ptyLeases.[id.Value])

            let recovery =
                ownershipCommonDir
                |> Option.map (fun directory ->
                    HostPtyOwnership.recoveryBinding directory owned.Lease owned.IncarnationId)

            (requirePtyPort ())
                .Fork(command, agent.Name, ptyId = id, ?cwd = cwd, ?recovery = recovery)
            |> ignore

            Ok id
        with ex ->
            Error ex.Message

    member private this.ReserveAndForkPty
        (command: string, agent: ManagedAgent, cwd: string option, runId: LogicalRunId option, id: PtyId)
        : Result<PtyId, string> =
        let reserved =
            lock gate (fun () ->
                if ptyDetachRequested then
                    Error "PTY runtime detach has started"
                elif runtime.IsCancelled then
                    Error "PTY runtime is cancelled"
                elif ptyClosing then
                    Error "PTY exit drain is in progress"
                else
                    let scopeId = ptyScopeForRun runId

                    HostPtyOwnership.reserve ptyOwnership scopeId id.Value
                    |> Result.map (fun lease ->
                        ptyLeases.[id.Value] <-
                            { Lease = lease
                              IncarnationId = Guid.NewGuid().ToString("N")
                              RunId = runId }

                        this.RegisterPtySnapshot id command))

        reserved
        |> Result.bind (fun () -> this.SpawnReservedPty(id, command, agent, cwd))

    member private this.ForkPtyForRun
        (command: string, agent: ManagedAgent, cwd: string option, requestedRun: LogicalRunId option, id: PtyId)
        : Task<Result<PtyId, string>> =
        let valid =
            if String.IsNullOrWhiteSpace command then
                Error "PTY command is required"
            elif ptyPort.IsNone then
                Error "Durable PTY ownership is unavailable"
            else
                Ok()

        try
            valid
            |> Result.bind (fun () -> ptyRunFor requestedRun)
            |> Result.bind (fun runId -> this.ReserveAndForkPty(command, agent, cwd, runId, id))
            |> Task.FromResult
        with error ->
            Task.FromResult(Error error.Message)

    member this.ForkPty(command: string, agent: ManagedAgent, ?cwd: string) : Task<Result<PtyId, string>> =
        this.ForkPtyForRun(command, agent, cwd, None, Pty.newId ())

    member this.TryPty(id: string) =
        if String.IsNullOrWhiteSpace id then
            None
        elif
            this.OwnsPty(PtyId.Create id)
            && (ptyPort |> Option.exists (fun port -> port.Known(PtyId.Create id)))
        then
            Some(PtyId.Create id)
        else
            None

    member this.SendPty(id: PtyId, prompt: string, signal: PtySignal option) : Task<Result<PtyRead, string>> =
        taskResult {
            do!
                if not (this.OwnsPty id) then
                    Error(sprintf "Unknown PTY id: %s" id.Value)
                elif not (ptyPort |> Option.exists (fun port -> port.Exists id)) then
                    Error(sprintf "Unknown PTY id: %s" id.Value)
                else
                    Ok()

            match signal with
            | Some value ->
                do! (requirePtyPort ()).Send(id, PtyCommand.Signal value)
                return PtyHostHelpers.emptyRead id
            | None when String.IsNullOrEmpty prompt ->
                let! output, closed = (requirePtyPort ()).Read id
                return PtyHostHelpers.mapRead id (output, closed)
            | None ->
                do!
                    (requirePtyPort ())
                        .Send(id, PtyCommand.Write(Pty.bytes (PtyHostHelpers.ensureLf prompt)))

                return PtyHostHelpers.emptyRead id
        }

    member this.TryTerminalNameByPtyId(ptyId: string) : string option =
        PtyHostHelpers.tryTerminalNameByPtyId gate terminalByName ptyId

    member this.PtyCapability: DelegationPtyCapability =
        { TryPtyByName = this.TryPtyByName
          ForkPty = fun (cmd, agent, cwd) -> this.ForkPty(cmd, agent, ?cwd = cwd)
          TryBindTerminalName = fun (name, id) -> this.TryBindTerminalName(name, id)
          ClosePty = fun id -> this.ClosePty id
          SendPty = fun (id, prompt, sigOpt) -> this.SendPty(id, prompt, sigOpt)
          OwnsPty = this.OwnsPty
          TryPty = this.TryPty
          TryTerminalNameByPtyId = this.TryTerminalNameByPtyId }

    member internal this.PtyCapabilityForRun
        (runId: LogicalRunId, providerRun: ProviderRunIdentity, toolCall: ToolCallId)
        =
        let owns (id: PtyId) =
            lock gate (fun () ->
                match ptyLeases.TryGetValue id.Value with
                | true, owned -> owned.RunId = Some runId
                | _ -> false)

        let id =
            [| parentKey
               LogicalRunId.value runId
               ProviderRunIdentity.value providerRun
               ToolCallId.value toolCall |]
            |> JS.JSON.stringify
            |> HostDigest.sha256Hex
            |> fun digest -> PtyId.Create("pty-" + digest)

        let unknown (id: PtyId) =
            sprintf "Unknown PTY id for this work round: %s" id.Value

        { TryPtyByName = fun name -> this.TryPtyByName name |> Option.filter owns
          ForkPty = fun (command, agent, cwd) -> this.ForkPtyForRun(command, agent, cwd, Some runId, id)
          TryBindTerminalName =
            fun (name, id) ->
                if owns id then
                    this.TryBindTerminalName(name, id)
                else
                    Error(unknown id)
          ClosePty =
            fun id ->
                if owns id then
                    this.ClosePty id
                else
                    task { return invalidOp (unknown id) }
          SendPty =
            fun (id, prompt, signal) ->
                if owns id then
                    this.SendPty(id, prompt, signal)
                else
                    Task.FromResult(Error(unknown id))
          OwnsPty = owns
          TryPty = fun id -> this.TryPty id |> Option.filter owns
          TryTerminalNameByPtyId =
            fun id ->
                if owns (PtyId.Create id) then
                    this.TryTerminalNameByPtyId id
                else
                    None }


    member _.EnqueueBufferedJoinItems(items: JoinItem seq) =
        lock gate (fun () ->
            for item in items do
                bufferedJoinItems.Enqueue item)

    member _.DrainBufferedJoinItems(maxCount: int) : JoinItem list =
        lock gate (fun () ->
            let count = min maxCount bufferedJoinItems.Count
            [ for _ in 1..count -> bufferedJoinItems.Dequeue() ])

    member _.HasBufferedJoinItems = lock gate (fun () -> bufferedJoinItems.Count > 0)
