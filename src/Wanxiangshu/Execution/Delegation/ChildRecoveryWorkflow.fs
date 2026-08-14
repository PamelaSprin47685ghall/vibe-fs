namespace Wanxiangshu.Execution.Delegation

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

    let private readDurableEvidence (ports: Ports) : Task<DurableHandleEvidence> =
        task {
            match ports.Journal with
            | None -> return DurableHandleEvidence.Unknown
            | Some journal ->
                let projection = AgentJournal.handleProjection journal ports.ParentId

                match HandleProjection.tryFind ports.Handle projection with
                | None -> return DurableHandleEvidence.Unknown
                | Some record ->
                    match record.Lifecycle with
                    | HandleLifecycle.Active -> return DurableHandleEvidence.Active
                    | HandleLifecycle.Retired -> return DurableHandleEvidence.Retired
                    | HandleLifecycle.Abandoned reason -> return DurableHandleEvidence.Abandoned reason
                    | HandleLifecycle.CompletedAwaitingJoin cell ->
                        match! HandleCompletionCodec.tryReadBody journal record with
                        | Ok(Some body, _, _) ->
                            match HandleCompletionCodec.decodeBody body with
                            | Current decoded ->
                                let proof =
                                    JoinableCompletion.fromDecoded
                                        ports.AgentId
                                        ports.Handle
                                        ports.ChildSession
                                        decoded
                                        body

                                return DurableHandleEvidence.CompletedAwaitingJoin proof
                            | LegacyFalseAbort _ -> return DurableHandleEvidence.Active
                            | Invalid _ -> return DurableHandleEvidence.Unknown
                        | Ok(None, _, _) ->
                            match cell.Kind with
                            | HandleCompletionKind.Cancelled -> return DurableHandleEvidence.Active
                            | HandleCompletionKind.Terminal
                            | HandleCompletionKind.SendFailure -> return DurableHandleEvidence.Active
                        | Error _ -> return DurableHandleEvidence.Unknown
        }

    let private readSnapshotEvidence (ports: Ports) : Task<ChildSnapshotEvidence> =
        task {
            match ports.Snapshot with
            | None -> return ChildSnapshotEvidence.Missing
            | Some port ->
                match! port.GetMessages ports.ChildSession with
                | Error reason -> return ChildSnapshotEvidence.Unreadable reason
                | Ok messages ->
                    match lastByRole messages "assistant" with
                    | None -> return ChildSnapshotEvidence.Active
                    | Some assistant when isTerminalCompleted assistant ->
                        match lastByRole messages "user" with
                        | None ->
                            return ChildSnapshotEvidence.Unreadable "host restart: terminal child has no user message"
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

                            return
                                ChildSnapshotEvidence.Terminal(
                                    TerminalEvidence.completed ports.AgentId ports.Handle ports.ChildSession body
                                )
                    | Some _assistant ->
                        // Mid-turn / non-terminal assistant: stream readable → still running.
                        return ChildSnapshotEvidence.Active
        }

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
        task {
            let! durable = readDurableEvidence ports
            let! snapshot = readSnapshotEvidence ports
            let resolution = resolveChild durable snapshot ports.Observations

            match resolution with
            | ChildResolution.RecoveredTerminal proof ->
                match! commitJoinable ports.Journal ports.ParentId proof with
                | Error reason -> return Error reason
                | Ok() ->
                    pulseAfterCommit ports
                    return Ok(ChildRecoveryResult.RecoveredTerminal proof)
            | ChildResolution.RecoveredAbandoned reason ->
                match! commitAbandon ports ports.Handle reason with
                | Error err -> return Error err
                | Ok() ->
                    return
                        Ok(
                            ChildRecoveryResult.RecoveredAbandoned
                                { Handle = ports.Handle
                                  Reason = reason }
                        )
            | ChildResolution.RecoveredActive ->
                return
                    Ok(
                        ChildRecoveryResult.RecoveredActive
                            { Handle = ports.Handle
                              ChildSession = ports.ChildSession }
                    )
            | ChildResolution.RecoveryIncomplete ->
                return
                    Ok(
                        ChildRecoveryResult.RecoveryIncomplete(
                            RecoveryDependency.AwaitingTerminalEvidence(ports.Handle, ports.ChildSession)
                        )
                    )
            | ChildResolution.RecoveryBlocked reason ->
                return Ok(ChildRecoveryResult.RecoveryBlocked(NonEmpty.one (ChildRecoveryBlock.Reason reason)))
        }
