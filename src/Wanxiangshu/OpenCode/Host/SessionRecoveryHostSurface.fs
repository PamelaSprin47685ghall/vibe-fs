namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

/// JS semantic boundary over the compiled session recovery host.
///
/// The host, scope and journal stay opaque handles: JS obtains them from
/// `bootRecoveryHost`, passes them back, and never inspects them. Proof
/// inputs cross as plain strings (session/physical/provider-run ids and the
/// `absent`/`reject`/`accept` port answer); observations return as plain JSON.
/// Every decision — resume/manual disposition, provider-started seeding,
/// terminal settlement and manual revocation — delegates to the real
/// SessionRecoveryHost, PluginRecoveryScope and ManagedChat journal owners.
/// No recovery model is copied here.
module SessionRecoveryHostSurface =

    /// Opaque recovery host bundle: JS obtains it from `bootRecoveryHost`,
    /// passes it back, and never inspects it.
    type RecoveryHostHandle =
        { Journal: JournalHandle
          Scope: PluginRecoveryScope
          Host: SessionRecoveryHost
          PortOutcome: string
          ResumeCalls: System.Collections.Generic.List<bool> }


    let private acceptedEvidence (sessionId: string) (physicalUserMessageId: string) : AcceptedChatExecutionEvidence =
        let identity =
            ParticipantIdentity.resolveAtRoot "coder"
            |> Result.defaultWith (fun error -> invalidOp (sprintf "%A" error))

        { SessionId = SessionId.create sessionId
          LogicalRunId = LogicalRunId.create $"run-{sessionId}"
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create $"root-{sessionId}"
          AuthorityKind = PromptRootAuthorityKind.HumanRoot
          IdentitySeed = RootSelection identity
          PhysicalUserMessageId = PhysicalUserMessageId.create physicalUserMessageId
          Origin = PromptOrigin.AuthorityRoot PromptRootAuthorityKind.HumanRoot }

    let private keyOf (evidence: AcceptedChatExecutionEvidence) : ChatExecutionKey =
        { SessionId = evidence.SessionId
          PhysicalUserMessageId = evidence.PhysicalUserMessageId }

    let private reasonName =
        function
        | ManualInterventionReason.MissingExternalReceipt -> "MissingExternalReceipt"
        | ManualInterventionReason.AmbiguousExternalReceipt -> "AmbiguousExternalReceipt"
        | ManualInterventionReason.PhysicalOutcomeUnknown -> "PhysicalOutcomeUnknown"
        | ManualInterventionReason.PersistenceOutcomeUnknown -> "PersistenceOutcomeUnknown"
        | ManualInterventionReason.NoAuthorizedProviderDisposition -> "NoAuthorizedProviderDisposition"

    let private observationName =
        function
        | ProviderPhysicalObservation.ReceiptMissing -> "ReceiptMissing"
        | ProviderPhysicalObservation.ReceiptAmbiguous -> "ReceiptAmbiguous"
        | ProviderPhysicalObservation.ProviderAbsent _ -> "ProviderAbsent"
        | ProviderPhysicalObservation.ProviderAlive _ -> "ProviderAlive"
        | ProviderPhysicalObservation.ProviderTerminal _ -> "ProviderTerminal"

    let private dispositionName =
        function
        | ChatExecutionTerminalDisposition.Completed -> "Completed"
        | ChatExecutionTerminalDisposition.Cancelled -> "Cancelled"
        | ChatExecutionTerminalDisposition.Rejected -> "Rejected"
        | ChatExecutionTerminalDisposition.Failed -> "Failed"

    let private lifecycleView (state: ChatExecutionState) : obj =
        match state.Lifecycle with
        | ChatExecutionLifecycle.Accepted ->
            box
                {| phase = "Accepted"
                   disposition = null |}
        | ChatExecutionLifecycle.ProviderStarted ->
            box
                {| phase = "ProviderStarted"
                   disposition = null |}
        | ChatExecutionLifecycle.Terminal disposition ->
            box
                {| phase = "Terminal"
                   disposition = dispositionName disposition |}

    let private manualView (request: ManualInterventionRequest) : obj =
        let lifecycle = lifecycleView request.ExecutionState

        box
            {| reason = reasonName request.InterventionReason
               observation = observationName request.ProviderObservation
               lifecycle = lifecycle?phase
               disposition = lifecycle?disposition
               sessionId = SessionId.value request.ExecutionState.Key.SessionId
               physicalUserMessageId = PhysicalUserMessageId.value request.ExecutionState.Key.PhysicalUserMessageId |}

    let private manualsOf (scope: PluginRecoveryScope) : obj =
        scope.ManualChatInterventions() |> Array.map manualView |> box

    let private portOf
        (calls: System.Collections.Generic.List<bool>)
        (portOutcome: string)
        : ExactAcceptedMessageRecoveryPort option =
        match portOutcome with
        | "absent" -> None
        | "accept" ->
            Some
                { ResumeAccepted =
                    fun _ ->
                        calls.Add true
                        Task.FromResult true }
        | "reject" ->
            Some
                { ResumeAccepted =
                    fun _ ->
                        calls.Add true
                        Task.FromResult false }
        | value -> invalidArg "portOutcome" $"unknown recovery proof port outcome '{value}'"

    /// Boot a temp journal, empty snapshot scope and the production recovery
    /// host. The snapshot port is the fixed empty physical read (no assistant
    /// material observed); only the accept/reject port answer varies.
    let bootRecoveryHost (directory: string) (portOutcome: string) : Task<RecoveryHostHandle> =
        task {
            let calls = System.Collections.Generic.List<bool>()
            // Fail fast on an unknown port answer before booting durability.
            let port = portOf calls portOutcome

            let! opened =
                JournalSurface.bootWithWriterId
                    directory
                    (Guid.NewGuid().ToString("N"))
                    $"recovery-proof-{portOutcome}"
                    4242
                    "2026-08-30T00:00:00Z"

            let ok: bool = opened?ok

            if not ok then
                let error: string = opened?error
                return invalidOp $"recovery proof journal boot failed: {error}"
            else
                let journalHandle: JournalHandle = opened?journal
                let scope = PluginRecoveryScope(None)

                let snapshot =
                    { new ISessionSnapshotPort with
                        member _.GetMessages _ = Task.FromResult(Ok []) }

                let host = SessionRecoveryHost(journalHandle.Journal, snapshot, scope, port)

                return
                    { Journal = journalHandle
                      Scope = scope
                      Host = host
                      PortOutcome = portOutcome
                      ResumeCalls = calls }
        }

    /// Resume one accepted execution through the production host and report
    /// the complete observable output: port invocations and manual
    /// interventions. Recovery retains no resume DTO; the returned view is
    /// the whole effect.
    let resumeAccepted (handle: RecoveryHostHandle) (sessionId: string) (physicalUserMessageId: string) : Task<obj> =
        task {
            let evidence = acceptedEvidence sessionId physicalUserMessageId

            do!
                handle.Host.ResumePreProvider
                    { ExecutionKey = keyOf evidence
                      AcceptedEvidence = evidence }

            return
                box
                    {| calls =
                        if handle.PortOutcome = "absent" then
                            0
                        else
                            handle.ResumeCalls.Count
                       manuals = manualsOf handle.Scope |}
        }

    let seedAccepted (handle: RecoveryHostHandle) (sessionId: string) (physicalUserMessageId: string) : Task<unit> =
        task {
            let evidence = acceptedEvidence sessionId physicalUserMessageId

            match! ManagedChatAcceptance.accept handle.Journal.Journal (keyOf evidence) evidence with
            | Ok _ -> ()
            | Error error -> return invalidOp $"recovery proof acceptance seeding failed: {error}"
        }

    /// Seed Accepted + ProviderStarted through the real managed-chat journal
    /// owners so a later terminal settlement revokes through production fold.
    let seedProviderStarted
        (handle: RecoveryHostHandle)
        (sessionId: string)
        (physicalUserMessageId: string)
        (providerRun: string)
        : Task<unit> =
        task {
            let evidence = acceptedEvidence sessionId physicalUserMessageId
            let key = keyOf evidence
            let journal = handle.Journal.Journal
            let run = ProviderRunIdentity.create providerRun

            do! seedAccepted handle sessionId physicalUserMessageId

            match!
                ManagedChatProviderLifecycle.providerStarted
                    journal
                    key
                    evidence
                    run
                    ProviderRequestKind.WorkMain
                    XProjectionChoice.UseCommittedEpoch
            with
            | Ok _ -> ()
            | Error error -> return invalidOp $"recovery proof provider-started seeding failed: {error}"
        }

    /// Finalize with provider-started terminal evidence through the
    /// production host and report the settled projection lifecycle plus the
    /// remaining manuals (revocation proof).
    let finalizeCompleted
        (handle: RecoveryHostHandle)
        (sessionId: string)
        (physicalUserMessageId: string)
        (providerRun: string)
        : Task<obj> =
        task {
            let evidence = acceptedEvidence sessionId physicalUserMessageId
            let key = keyOf evidence

            let started =
                { Accepted = evidence
                  ProviderRun = ProviderRunIdentity.create providerRun
                  RequestKind = ProviderRequestKind.WorkMain
                  ProjectionChoice = XProjectionChoice.UseCommittedEpoch }

            do!
                handle.Host.Finalize
                    { ExecutionKey = key
                      TerminalEvidence = ChatExecutionTerminalEvidence.AfterProviderStart started
                      TerminalDisposition = ChatExecutionTerminalDisposition.Completed }

            let projection =
                (AgentJournal.snapshot handle.Journal.Journal).AgentProjections.ChatExecutions

            let lifecycle =
                ChatExecutionProjection.byKey key projection
                |> Option.map lifecycleView
                |> Option.defaultWith (fun () -> invalidOp "recovery proof terminal settlement left no projection")

            return
                box
                    {| manuals = manualsOf handle.Scope
                       lifecycle = lifecycle?phase
                       disposition = lifecycle?disposition
                       sessionId = sessionId
                       physicalUserMessageId = physicalUserMessageId |}
        }

    let signalCancelled (handle: RecoveryHostHandle) (sessionId: string) (physicalUserMessageId: string) : Task<obj> =
        task {
            let evidence = acceptedEvidence sessionId physicalUserMessageId
            let key = keyOf evidence
            do! handle.Host.Signal(ChatExecutionRecoveryLifecycleEvent.SessionCancelled key)

            let projection =
                (AgentJournal.snapshot handle.Journal.Journal).AgentProjections.ChatExecutions

            let lifecycle =
                ChatExecutionProjection.byKey key projection
                |> Option.map lifecycleView
                |> Option.defaultWith (fun () -> invalidOp "recovery proof cancellation left no projection")

            return
                box
                    {| manuals = manualsOf handle.Scope
                       lifecycle = lifecycle?phase
                       disposition = lifecycle?disposition
                       sessionId = sessionId
                       physicalUserMessageId = physicalUserMessageId |}
        }

    /// Release the journal capability. The caller removes the directory.
    let disposeRecoveryHost (handle: RecoveryHostHandle) : unit = JournalSurface.dispose handle.Journal
