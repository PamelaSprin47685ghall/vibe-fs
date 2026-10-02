namespace Wanxiangshu.OpenCode

open Wanxiangshu.Execution.Delegation
open System.Threading.Tasks

#nowarn "3511"

open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Persistence
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica

[<AutoOpen>]
module private PluginSessionWiringHelpers =

    let recordedBindingIds (owner: SessionId) (projection: StrengthProjection) : Set<SessionId> =
        projection.ByDecision
        |> Map.toSeq
        |> Seq.map snd
        |> Seq.filter (fun view -> view.Request.OwnerSessionId = owner)
        |> Seq.choose (fun view -> view.Binding |> Option.map (fun binding -> binding.ReplicaSessionId))
        |> Set.ofSeq

    let validateResident
        (agent: string)
        (recorded: Set<SessionId>)
        (children: OpenCodeChildInfo list)
        : Result<SessionId option, string> =
        let linked =
            children |> List.filter (fun child -> Set.contains child.SessionId recorded)

        match linked with
        | [] -> Ok None
        | [ child ] when child.Agent = Some agent && child.Title = Some agent -> Ok(Some child.SessionId)
        | _ -> Error "StrengthReplica resident recovery has ambiguous or conflicting Host children"

    let restoreResidentSession
        (durabilityOpt: StrengthDurabilityPort option)
        (sessionPort: ISessionHostPort)
        (owner: SessionId)
        (agent: string)
        : Task<Result<SessionId option, string>> =
        let durabilityResult =
            match durabilityOpt with
            | Some durability -> Ok durability
            | None -> Error "StrengthReplica resident recovery requires durability"

        taskResult {
            let! durability = durabilityResult
            let! projection = durability.LoadProjection()
            let recorded = recordedBindingIds owner projection

            if Set.isEmpty recorded then
                return None
            else
                let! children =
                    sessionPort.ListChildren owner
                    |> TaskResult.mapError (fun reason -> "StrengthReplica resident recovery failed: " + reason)

                return! validateResident agent recorded children
        }

module PluginSessionWiring =

    let private attachReplicaRuntime
        (boot: PluginBoot.Boot)
        (host: PluginHostWiring.Host)
        dispatcher
        registerReplica
        acquireModel
        =
        if boot.StrengthScope.StrengthReplicaRuntime.IsNone then
            boot.StrengthScope.AttachStrengthReplicaRuntime(
                new StrengthReplicaRuntime(
                    host.SessionPort,
                    dispatcher,
                    boot.StrengthScope.StrengthRuntime,
                    registerReplica,
                    ?workspaceDirectory = boot.WorkspaceDirectory,
                    ?tryAcquireModel = Some acquireModel,
                    ?releaseModel = Some(fun sessionId -> ModelRouting.releaseExecution sessionId |> ignore),
                    ?restoreResident = Some(restoreResidentSession host.StrengthDurability host.SessionPort),
                    ?snapshotPort = host.SnapshotOpt
                )
            )

    /// SyncDelegate + StrengthReplica runtimes, attached only when a durable
    /// journal exists (the sync path is what makes both runtimes meaningful).
    let attach (boot: PluginBoot.Boot) (host: PluginHostWiring.Host) : unit =
        let scope = boot.Scope
        let journal = boot.Journal
        let sessionPort = host.SessionPort
        let wired = host.Wired
        let workspaceDirectory = boot.WorkspaceDirectory

        /// The delegate's role is whatever its managed name resolves to; a name
        /// that resolves to nothing must fail loudly rather than guess a role.
        let roleForAgent (agent: string) : Role =
            match ManagedAgent.tryParse agent with
            | Some managed -> managed.Role
            | None -> invalidArg "agent" (sprintf "unknown managed agent '%s'" agent)

        let bindStrengthReplica replicaId agent =
            match ManagedAgent.tryParse agent with
            | Some managed -> wired.BindActiveRun replicaId managed.Role workspaceDirectory
            | None -> ()

        /// The retry decorator's real plug for dedicated delegate children: the
        /// provider-owned engine decides, admits and re-dispatches; the delegate
        /// observes only continue-vs-terminal (delegation-023).
        let delegateRetryPort (durable: AgentJournal) : SyncDelegateRetryPort =
            { Retry =
                fun turn failure error ->
                    task {
                        let! verdict =
                            ProviderRecoveryWorkflow.continueDelegateCallAfterConfirmedFailure
                                sessionPort
                                host.RootWorkspace
                                scope.BloggerRuntimeHost
                                durable
                                turn
                                failure
                                error

                        match verdict with
                        | RetryVerdict.Dispatched
                        | RetryVerdict.Superseded -> return Ok()
                        | RetryVerdict.Terminal reason -> return Error reason
                    } }

        let seedDurableSessions (durable: AgentJournal) =
            let snapshot = AgentJournal.snapshot durable

            snapshot.AgentProjections.Sessions
            |> Map.iter (fun sessionId sessionProj ->
                sessionProj.PromptAuthority
                |> Option.bind (fun authority -> authority.ActiveLogicalRun)
                |> Option.iter (fun _ -> scope.Sessions.ModelRoutingSessions.Add(SessionId.value sessionId) |> ignore))

        match journal with
        | Some durable ->
            // durable-events-020: history-derived process bindings are semantic
            // state, so seeding them belongs to the first durable admission, not
            // plugin construction. This callback also forces the deferred
            // WorkspaceEventStore Current exactly at that activation boundary.
            scope.AttachDurabilityActivation(fun () -> seedDurableSessions durable)

            // SyncDelegate attaches whenever the durable journal exists.
            let attached =
                AttachedSessionRuntime(
                    registerParent =
                        (fun owner child ->
                            scope.Sessions.SessionParents.[SessionId.value child] <- SessionId.value owner),
                    isUsable = (fun _ -> true)
                )

            let dispatcher = PromptDispatcher.forPrompts (PromptJournalAdapter.create durable)

            let registerDelegate (delegateId: SessionId) (agent: string) =
                wired.RegisterOwned(SessionId.value delegateId)

                wired.BindActiveRun delegateId (roleForAgent agent) workspaceDirectory

            let workRecordCapability: DelegationWorkRecordCapability =
                { ParentWorkRecord =
                    fun sessionId -> LifecycleWorkRecordProjection.lifecycleWorkRecord (Some durable) sessionId true
                  ParentWorkRecordBounded =
                    fun sessionId range ->
                        LifecycleWorkRecordProjection.lifecycleWorkRecordBounded (Some durable) sessionId range }

            let syncDelegate =
                new SyncDelegateRuntime(
                    sessionPort,
                    CausalAwait.awaitTask host.CausalWaitObserver,
                    CausalAwait.awaitTask host.CausalWaitObserver,
                    dispatcher,
                    durable,
                    (attached :> IAttachedSessionPort),
                    registerDelegate,
                    scope.Sessions.Quiescence,
                    (fun sessionId range providerRun ->
                        LifecycleWorkRecordProjection.lifecycleWorkRecordBoundedForRun
                            (Some durable)
                            sessionId
                            range
                            providerRun),
                    DelegationHandoffLedger.port workRecordCapability durable,
                    delegateRetryPort durable,
                    toolMapForRole =
                        (fun role ->
                            // epistemic-reasoning-032: delegated Engineers have
                            // the same WorkMain capabilities as other Engineers.
                            PromptAuthority.toolCapabilitiesFor role ProviderRequestKind.WorkMain
                            |> StaticTools.requestToolMap),
                    ?workspaceDirectory = workspaceDirectory,
                    ?onDelegatePrompt = Some CasebookLifecycle.notePrompt,
                    ?onDelegateAnswer = Some CasebookLifecycle.noteAnswer,
                    ?onDelegateCleanup = Some CasebookLifecycle.cleanupDraft
                )

            scope.AttachSyncDelegateRuntime syncDelegate

            let registerStrengthReplica (ownerId: SessionId) (replicaId: SessionId) (agent: string) =
                let ownerKey = SessionId.value ownerId
                let replicaKey = SessionId.value replicaId
                scope.Sessions.SessionParents.[replicaKey] <- ownerKey
                wired.RegisterOwned ownerKey
                wired.RegisterOwned replicaKey

                bindStrengthReplica replicaId agent

            let tryParentKey sessionId =
                let found, parentKey =
                    scope.Sessions.SessionParents.TryGetValue(SessionId.value sessionId)

                if found then Some parentKey else None

            attachReplicaRuntime boot host dispatcher registerStrengthReplica (fun sessionId agent ->
                ModelRouting.tryReserveManaged
                    sessionId
                    (roleForAgent agent)
                    ModelExecutionPurpose.ReadonlyDelegate
                    (tryParentKey sessionId)
                |> Option.map ModelRouting.toOpenCodeModel)
        | None -> ()
