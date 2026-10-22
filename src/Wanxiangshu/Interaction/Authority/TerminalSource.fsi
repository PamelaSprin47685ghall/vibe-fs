namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module AuthorityTerminalSource =

    /// The legal terminal outcomes of an authority lifecycle.
    ///
    /// `Completed`, `Cancelled` and `Failed` are the outcomes a lifecycle may
    /// reach; nothing else is a terminal, and nothing else may be written.
    [<RequireQualifiedAccess>]
    type TerminalOutcome =
        | Completed
        | Cancelled
        | Failed

    /// The identity of the lifecycle whose terminal is being recorded.
    [<RequireQualifiedAccess>]
    type TerminalIdentity =
        { SessionId: SessionId
          LogicalRunId: LogicalRunId
          AuthorityRootUserMessageId: AuthorityRootUserMessageId }

    /// The closure kind a terminal source belongs to.
    ///
    /// The labels are the library's closure kinds, so a source and the root it
    /// closes are compared in one vocabulary rather than two.
    [<RequireQualifiedAccess>]
    type SourceKind =
        | HumanRootManagedRun
        | AgentOwnerChildWork
        | AgentOwnerAttachedWork
        | AgentOwnerInternalLeaf

    /// The canonical label of a source kind.
    val sourceKindLabel: kind: SourceKind -> string

    /// A stable id for one terminal outcome.
    ///
    /// Derived from the identity of the lifecycle and the outcome itself, so
    /// the same outcome recorded twice names the same source, and two
    /// different outcomes of the same run never collide.
    val factIdOf: sha256: (string -> string) -> identity: TerminalIdentity -> outcome: TerminalOutcome -> string

    /// The durable source terminal of a managed (non-Manager) HumanRoot run.
    val managedRunTerminal:
        sha256: (string -> string) ->
        identity: TerminalIdentity ->
        outcome: TerminalOutcome ->
            AuthorityManagedRunTerminalPayload

    /// The durable source terminal of an agent-owner child work.
    val childWorkTerminal:
        sha256: (string -> string) ->
        identity: TerminalIdentity ->
        ownerLogicalRunId: string ->
        outcome: TerminalOutcome ->
            AuthorityChildWorkTerminalPayload

    /// The durable source terminal of an agent-owner attached work.
    val attachedWorkTerminal:
        sha256: (string -> string) ->
        identity: TerminalIdentity ->
        ownerLogicalRunId: string ->
        attachmentKind: string ->
        associationGeneration: string ->
        outcome: TerminalOutcome ->
            AuthorityAttachedWorkTerminalPayload

    /// The durable source terminal of an agent-owner internal leaf.
    val internalLeafTerminal:
        sha256: (string -> string) ->
        identity: TerminalIdentity ->
        ownerLogicalRunId: string ->
        decisionOrTransactionId: string ->
        outcome: TerminalOutcome ->
            AuthorityInternalLeafTerminalPayload
