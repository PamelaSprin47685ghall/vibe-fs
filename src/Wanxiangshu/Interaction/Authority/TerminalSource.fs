namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

/// interaction-authority-018: the single writer of the durable source
/// terminals of the authority lifecycles.
///
/// A lifecycle reaches a legal terminal outcome — `Completed`, `Cancelled` or
/// `Failed` — and that outcome is the only thing a closure may cite. This
/// module turns such an outcome into the typed durable fact of its kind; it
/// never writes the closure itself, and it never writes a closure from an
/// observation.
///
/// A void is not a terminal outcome: an aborted work owes nothing and produced
/// nothing, so no `*Terminal` is written for it. The lifecycle that owns the
/// work decides which of the three legal outcomes it reached; this module only
/// records that decision durably.
///
/// The fact id is derived from the identity of the lifecycle and the outcome,
/// so the same outcome recorded twice names the same source, and two different
/// outcomes of the same run never collide. The closure cites that id, so a
/// repeated closure is recognised as the same one and a different one is
/// recognised as a conflict.
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
    let sourceKindLabel (kind: SourceKind) : string =
        match kind with
        | SourceKind.HumanRootManagedRun -> "HumanRootManagedRun"
        | SourceKind.AgentOwnerChildWork -> "AgentOwnerChildWork"
        | SourceKind.AgentOwnerAttachedWork -> "AgentOwnerAttachedWork"
        | SourceKind.AgentOwnerInternalLeaf -> "AgentOwnerInternalLeaf"

    /// A stable id for one terminal outcome.
    ///
    /// Derived from the identity of the lifecycle and the outcome itself, so
    /// the same outcome recorded twice names the same source, and two
    /// different outcomes of the same run never collide.
    let factIdOf (sha256: string -> string) (identity: TerminalIdentity) (outcome: TerminalOutcome) : string =
        let outcomeLabel =
            match outcome with
            | TerminalOutcome.Completed -> "completed"
            | TerminalOutcome.Cancelled -> "cancelled"
            | TerminalOutcome.Failed -> "failed"

        sha256 (
            sprintf
                "authority-terminal:%s:%s:%s:%s"
                (SessionId.value identity.SessionId)
                (LogicalRunId.value identity.LogicalRunId)
                (AuthorityRootUserMessageId.value identity.AuthorityRootUserMessageId)
                outcomeLabel
        )

    /// The durable source terminal of a managed (non-Manager) HumanRoot run.
    let managedRunTerminal
        (sha256: string -> string)
        (identity: TerminalIdentity)
        (outcome: TerminalOutcome)
        : AuthorityManagedRunTerminalPayload =
        { SessionId = identity.SessionId
          LogicalRunId = identity.LogicalRunId
          AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId
          FactId = factIdOf sha256 identity outcome }

    /// The durable source terminal of an agent-owner child work.
    let childWorkTerminal
        (sha256: string -> string)
        (identity: TerminalIdentity)
        (ownerLogicalRunId: string)
        (outcome: TerminalOutcome)
        : AuthorityChildWorkTerminalPayload =
        { SessionId = identity.SessionId
          OwnerLogicalRunId = ownerLogicalRunId
          ChildLogicalRunId = identity.LogicalRunId
          AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId
          FactId = factIdOf sha256 identity outcome }

    /// The durable source terminal of an agent-owner attached work.
    let attachedWorkTerminal
        (sha256: string -> string)
        (identity: TerminalIdentity)
        (ownerLogicalRunId: string)
        (attachmentKind: string)
        (associationGeneration: string)
        (outcome: TerminalOutcome)
        : AuthorityAttachedWorkTerminalPayload =
        { SessionId = identity.SessionId
          OwnerLogicalRunId = ownerLogicalRunId
          ChildLogicalRunId = identity.LogicalRunId
          AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId
          AttachmentKind = attachmentKind
          AssociationGeneration = associationGeneration
          FactId = factIdOf sha256 identity outcome }

    /// The durable source terminal of an agent-owner internal leaf.
    let internalLeafTerminal
        (sha256: string -> string)
        (identity: TerminalIdentity)
        (ownerLogicalRunId: string)
        (decisionOrTransactionId: string)
        (outcome: TerminalOutcome)
        : AuthorityInternalLeafTerminalPayload =
        { SessionId = identity.SessionId
          OwnerLogicalRunId = ownerLogicalRunId
          LeafLogicalRunId = identity.LogicalRunId
          AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId
          DecisionOrTransactionId = decisionOrTransactionId
          FactId = factIdOf sha256 identity outcome }
