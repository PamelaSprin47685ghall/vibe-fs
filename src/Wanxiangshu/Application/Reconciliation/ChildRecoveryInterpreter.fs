namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Production interpreter for ChildRecoveryProgram (P0-RECOVERY-JOIN-001 / FLOW-003).
/// CommitCompletion → HandleController.recordCompletion only; no bare PublishCompletion bypass.
module ChildRecoveryInterpreter =

    type Ports =
        {
            Journal: AgentJournal option
            ParentId: SessionId
            Snapshot: ISessionSnapshotPort option
            AgentId: string
            Handle: HandleId
            ChildSession: SessionId
            Role: AgentRole
            Agent: string
            Observations: HostObservation list
            /// Process-local mailbox after durable commit. None = durable only.
            Publish: (RunCompletion -> unit) option
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

    let private durableEvidence (ports: Ports) : DurableHandleEvidence =
        match ports.Journal with
        | None -> DurableHandleEvidence.Unknown
        | Some journal ->
            let projection = AgentJournal.handleProjection journal ports.ParentId

            match HandleProjection.tryFind ports.Handle projection with
            | None -> DurableHandleEvidence.Unknown
            | Some record ->
                match record.Lifecycle with
                | HandleLifecycle.Active -> DurableHandleEvidence.Active
                | HandleLifecycle.Retired -> DurableHandleEvidence.Retired
                | HandleLifecycle.Abandoned reason -> DurableHandleEvidence.Abandoned reason
                | HandleLifecycle.CompletedAwaitingJoin cell ->
                    match HandleCompletionCodec.tryReadBody journal record with
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

                            DurableHandleEvidence.CompletedAwaitingJoin proof
                        | LegacyFalseAbort _ -> DurableHandleEvidence.Active
                        | Invalid _ -> DurableHandleEvidence.Unknown
                    | Ok(None, _, _) ->
                        match cell.Kind with
                        | HandleCompletionKind.Cancelled -> DurableHandleEvidence.Active
                        | HandleCompletionKind.Terminal
                        | HandleCompletionKind.SendFailure -> DurableHandleEvidence.Active
                    | Error _ -> DurableHandleEvidence.Unknown

    let private handleRecord (ports: Ports) : HandleRecord =
        { Handle = ports.Handle
          ChildSessionId = ports.ChildSession
          TargetAgent = ports.Agent
          CanonicalRole = AgentRoleIdentity.toRole ports.Role
          Lifecycle = HandleLifecycle.Active
          // Decode-only stub for HandleCompletionCodec; CreationOrder unused.
          CreationOrder = 0
          LastCompletion = None }

    let private publishProof (ports: Ports) (proof: JoinableCompletion) : unit =
        match ports.Publish, JoinableCompletion.body proof with
        | Some publish, Some body ->
            match HandleCompletionCodec.tryDecode (handleRecord ports) ports.AgentId body with
            | Ok completion -> publish completion
            | Error _ -> ()
        | _ -> ()

    /// P0-RECOVERY-JOIN-001 §十: sole production caller of HandleController.recordCompletion.
    /// Host hot paths (proven terminal) must commit through this function — not call the controller.
    let commitJoinable
        (journal: AgentJournal option)
        (parentId: SessionId)
        (proof: JoinableCompletion)
        : Result<unit, string> =
        HandleController.recordCompletion journal parentId proof

    /// Interpret a child recovery program. Fail closed on commit/proof errors.
    let interpret (ports: Ports) (program: ChildRecoveryProgram<'result>) : Task<Result<'result, string>> =
        let rec go (program: ChildRecoveryProgram<'result>) : Task<Result<'result, string>> =
            task {
                match program with
                | Return value -> return Ok value
                | Block reason -> return Error reason
                | ReadDurableHandle(_, next) -> return! go (next (durableEvidence ports))
                | ReadChildSnapshot(_, next) ->
                    match ports.Snapshot with
                    | None -> return! go (next ChildSnapshotEvidence.Missing)
                    | Some port ->
                        match! port.GetMessages ports.ChildSession with
                        | Error reason -> return! go (next (ChildSnapshotEvidence.Unreadable reason))
                        | Ok messages ->
                            match lastByRole messages "assistant" with
                            | None -> return! go (next ChildSnapshotEvidence.Active)
                            | Some assistant when isTerminalCompleted assistant ->
                                match lastByRole messages "user" with
                                | None ->
                                    return!
                                        go (
                                            next (
                                                ChildSnapshotEvidence.Unreadable
                                                    "host restart: terminal child has no user message"
                                            )
                                        )
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

                                    return!
                                        go (
                                            next (
                                                ChildSnapshotEvidence.Terminal(
                                                    TerminalEvidence.completed
                                                        ports.AgentId
                                                        ports.Handle
                                                        ports.ChildSession
                                                        body
                                                )
                                            )
                                        )
                            | Some assistant ->
                                return!
                                    go (
                                        next (
                                            ChildSnapshotEvidence.Unreadable(
                                                sprintf
                                                    "host restart: child not terminal (finish=%s)"
                                                    (defaultArg assistant.Finish "none")
                                            )
                                        )
                                    )
                | ObserveHostSignals(_, next) -> return! go (next ports.Observations)
                | ProveTerminal(evidence, next) ->
                    match JoinableCompletion.tryFromProvenTerminal evidence with
                    | Ok proof -> return! go (next proof)
                    | Error reason -> return Error reason
                | CommitCompletion(proof, next) ->
                    match commitJoinable ports.Journal ports.ParentId proof with
                    | Error reason -> return Error reason
                    | Ok() ->
                        publishProof ports proof
                        return! go (next ())
                | CommitAbandonment(handle, reason, next) ->
                    let agentId =
                        match HandleId.tryAgent handle with
                        | Some id -> AgentHandleId.value id
                        | None -> ports.AgentId

                    match
                        HandleController.recordAbandon ports.Journal ports.ParentId agentId reason DateTimeOffset.UtcNow
                    with
                    | Error err -> return Error err
                    | Ok() -> return! go (next ())
                | KeepWaiting(_, next) -> return! go (next ())
            }

        go program

    /// Run Domain recoverChild and commit via the single write entry.
    let resolveAndCommit (ports: Ports) : Task<Result<ChildResolution, string>> =
        interpret ports (recoverChild ports.Handle ports.ChildSession)
