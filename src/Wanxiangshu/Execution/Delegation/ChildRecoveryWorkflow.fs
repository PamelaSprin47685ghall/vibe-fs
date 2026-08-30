namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Composition.Durable

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// Direct-CE child recovery (FLOW-001 / P0-1).
/// Sole production owner of HandleController.recordCompletion.
/// GREEN-5: after durable commit, Pulse agent handle only (wake); Journal is fact source.
module ChildRecoveryWorkflow =

    /// DSL-state-combination: physical — optional Journal/Snapshot capabilities
    /// are injected infrastructure ports; the remaining fields are one recovery
    /// invocation's identities and observations, not a stored program counter.
    /// DSL-class: PhysicalHandle — HOST recovery invocation ports and wake handle; owner CHILD-RECOVERY, law FLOW-001, proof direct-ce-contract.
    type Ports =
        {
            Journal: AgentJournal option
            ParentId: SessionId
            Snapshot: ISessionSnapshotPort option
            AgentId: string
            Handle: HandleId
            ChildSession: SessionId
            Role: Role
            Agent: string
            Observations: HostObservation list
            /// Process-local agent wake after durable commit. None = durable only.
            Pulse: (unit -> unit) option
            /// Injectable wall clock (rabbit §15 / G4R-CE S11) — no raw UtcNow.
            Clock: IClockPort
        }

    let private textOfParts (parts: MessagePart array) =
        if isNull parts then
            ""
        else
            parts
            |> Array.choose (function
                | MessagePart.Text text -> Some text
                | _ -> None)
            |> String.concat ""

    let private lastByRole (messages: SessionMessage list) (role: string) =
        messages
        |> List.rev
        |> List.tryFind (fun message -> message.Role.Equals(role, StringComparison.OrdinalIgnoreCase))

    let private isTerminalCompleted (assistant: SessionMessage) =
        match assistant.Finish with
        | Some finish when finish.Equals("stop", StringComparison.OrdinalIgnoreCase) ->
            not (String.IsNullOrWhiteSpace(textOfParts assistant.Parts))
        | _ -> false

    let private evidenceFromDecodedBody (ports: Ports) (body: string) : DurableHandleEvidence =
        match HandleCompletionCodec.decodeBody body with
        | Current decoded ->
            let proof =
                JoinableCompletion.fromDecoded ports.AgentId ports.Handle ports.ChildSession decoded body

            DurableHandleEvidence.CompletedAwaitingJoin proof
        | LegacyFalseAbort _ -> DurableHandleEvidence.Active
        | Invalid _ -> DurableHandleEvidence.Unknown

    let private evidenceFromAwaitingJoin
        (ports: Ports)
        (journal: AgentJournal)
        (record: HandleRecord)
        : Task<DurableHandleEvidence> =
        task {
            match! HandleCompletionCodec.tryReadBody journal record with
            | Ok(Some body, _, _) -> return evidenceFromDecodedBody ports body
            | Ok(None, _, _) -> return DurableHandleEvidence.Active
            | Error _ -> return DurableHandleEvidence.Unknown
        }

    let private evidenceFromLifecycle
        (ports: Ports)
        (journal: AgentJournal)
        (record: HandleRecord)
        : Task<DurableHandleEvidence> =
        match record.Lifecycle with
        | HandleLifecycle.Active -> Task.FromResult DurableHandleEvidence.Active
        | HandleLifecycle.Retired -> Task.FromResult DurableHandleEvidence.Retired
        | HandleLifecycle.Abandoned reason -> Task.FromResult(DurableHandleEvidence.Abandoned reason)
        | HandleLifecycle.CompletedAwaitingJoin _ -> evidenceFromAwaitingJoin ports journal record

    let private readDurableFromJournal (ports: Ports) (journal: AgentJournal) : Task<DurableHandleEvidence> =
        let projection = AgentJournal.handleProjection journal ports.ParentId

        match HandleProjection.tryFind ports.Handle projection with
        | None -> Task.FromResult DurableHandleEvidence.Unknown
        | Some record -> evidenceFromLifecycle ports journal record

    let private readDurableEvidence (ports: Ports) : Task<DurableHandleEvidence> =
        match ports.Journal with
        | None -> Task.FromResult DurableHandleEvidence.Unknown
        | Some journal -> readDurableFromJournal ports journal

    let private terminalFromMessages
        (ports: Ports)
        (messages: SessionMessage list)
        (assistant: SessionMessage)
        : ChildSnapshotEvidence =
        match lastByRole messages "user" with
        | None -> ChildSnapshotEvidence.Unreadable "host restart: terminal child has no user message"
        | Some user ->
            let runId = "run-restored-" + ports.AgentId
            let workRecord = textOfParts assistant.Parts

            let agentOutcome =
                AgentCompletion.completed
                    ports.AgentId
                    ports.ChildSession
                    runId
                    ports.Role
                    (AuthorityRootUserMessageId.create user.Id)
                    (ProviderRunIdentity.create assistant.Id)
                    workRecord
                    None

            let body = HandleCompletionCodec.encodeOutcome runId agentOutcome

            ChildSnapshotEvidence.Terminal(
                TerminalEvidence.completed ports.AgentId ports.Handle ports.ChildSession body
            )

    let private snapshotFromMessages (ports: Ports) (messages: SessionMessage list) : ChildSnapshotEvidence =
        match lastByRole messages "assistant" with
        | None -> ChildSnapshotEvidence.Active
        | Some assistant when isTerminalCompleted assistant -> terminalFromMessages ports messages assistant
        | Some _assistant ->
            // Mid-turn / non-terminal assistant: stream readable → still running.
            ChildSnapshotEvidence.Active

    let private readSnapshotFromPort (ports: Ports) (port: ISessionSnapshotPort) : Task<ChildSnapshotEvidence> =
        task {
            match! port.GetMessages ports.ChildSession with
            | Error reason -> return ChildSnapshotEvidence.Unreadable reason
            | Ok messages -> return snapshotFromMessages ports messages
        }

    let private readSnapshotEvidence (ports: Ports) : Task<ChildSnapshotEvidence> =
        match ports.Snapshot with
        | None -> Task.FromResult ChildSnapshotEvidence.Missing
        | Some port -> readSnapshotFromPort ports port

    let private pulseAfterCommit (ports: Ports) : unit =
        match ports.Pulse with
        | Some pulse -> pulse ()
        | None -> ()

    /// P0-RECOVERY-JOIN-001 §十: sole production caller of HandleController.recordCompletion.
    let commitJoinable
        (journal: AgentJournal option)
        (parentId: SessionId)
        (proof: JoinableCompletion)
        : Task<Result<unit, string>> =
        HandleController.recordCompletion journal parentId proof

    let private commitAbandon
        (ports: Ports)
        (handle: HandleId)
        (reason: HandleAbandonReason)
        : Task<Result<unit, string>> =
        let agentId =
            match HandleId.tryAgent handle with
            | Some id -> AgentHandleId.value id
            | None -> ports.AgentId

        HandleController.recordAbandon ports.Journal ports.ParentId agentId reason (ports.Clock.UtcNow())

    /// Resolve one child and commit through the single write entry.
    /// GREEN-4: ChildRecoveryResult (RecoveredActive ≠ RecoveryIncomplete).
    let resolveAndCommit (ports: Ports) : Task<Result<ChildRecoveryResult, string>> =
        taskResult {
            let! durable = readDurableEvidence ports |> TaskResultCE.ofTask
            let! snapshot = readSnapshotEvidence ports |> TaskResultCE.ofTask
            let resolution = resolveChild durable snapshot ports.Observations

            match resolution with
            | ChildResolution.RecoveredTerminal proof ->
                do! commitJoinable ports.Journal ports.ParentId proof
                pulseAfterCommit ports
                return ChildRecoveryResult.RecoveredTerminal proof
            | ChildResolution.RecoveredAbandoned reason ->
                do! commitAbandon ports ports.Handle reason

                return
                    ChildRecoveryResult.RecoveredAbandoned
                        { Handle = ports.Handle
                          Reason = reason }
            | ChildResolution.RecoveredActive ->
                return
                    ChildRecoveryResult.RecoveredActive
                        { Handle = ports.Handle
                          ChildSession = ports.ChildSession }
            | ChildResolution.RecoveryIncomplete ->
                return
                    ChildRecoveryResult.RecoveryIncomplete(
                        RecoveryDependency.AwaitingTerminalEvidence(ports.Handle, ports.ChildSession)
                    )
            | ChildResolution.RecoveryBlocked reason ->
                return ChildRecoveryResult.RecoveryBlocked(NonEmpty.one (ChildRecoveryBlock.Reason reason))
        }
