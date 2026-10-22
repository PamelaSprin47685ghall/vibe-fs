namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

/// interaction-authority-018: the durable source terminal of an agent-owner
/// child work.
///
/// A child work reaches a legal terminal outcome when it completes, is
/// abandoned, or fails. That outcome is the only thing the child's authority
/// root may later be closed against, so it is written as a durable typed
/// fact here — at the point where the work's own outcome is already durable.
///
/// A void is not one of those outcomes: a voided work never became a work at
/// all, and its owner owes nothing by it. So a void writes no terminal, and
/// the owner's root stays open until a real outcome arrives.
[<RequireQualifiedAccess>]
module ChildWorkTerminalSource =

    /// The identity of the child work whose terminal is recorded.
    [<RequireQualifiedAccess>]
    type ChildWorkTerminalIdentity =
        {
            /// The child session the work ran in.
            ChildSessionId: SessionId
            /// The child's logical run — the run the terminal closes.
            LogicalRunId: LogicalRunId
            /// The child's authority root.
            AuthorityRootUserMessageId: AuthorityRootUserMessageId
            /// The owner run that delegated the work.
            OwnerLogicalRunId: string
        }

    /// The durable source terminal of a child work.
    ///
    /// The fact id is derived from the identity of the work and the outcome,
    /// so the same outcome recorded twice names the same source, and two
    /// different outcomes of the same work never collide.
    let childWorkTerminal
        (sha256: string -> string)
        (identity: ChildWorkTerminalIdentity)
        (outcome: AuthorityTerminalSource.TerminalOutcome)
        : AuthorityChildWorkTerminalPayload =
        AuthorityTerminalSource.childWorkTerminal
            sha256
            { SessionId = identity.ChildSessionId
              LogicalRunId = identity.LogicalRunId
              AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId }
            identity.OwnerLogicalRunId
            outcome

    /// The durable source terminal of an attached work.
    ///
    /// An attached work is closed by its owner with an attached-work closure,
    /// so its terminal carries the attachment kind and the association
    /// generation that identify the attachment it belongs to.
    let attachedWorkTerminal
        (sha256: string -> string)
        (identity: ChildWorkTerminalIdentity)
        (attachmentKind: string)
        (associationGeneration: string)
        (outcome: AuthorityTerminalSource.TerminalOutcome)
        : AuthorityAttachedWorkTerminalPayload =
        AuthorityTerminalSource.attachedWorkTerminal
            sha256
            { SessionId = identity.ChildSessionId
              LogicalRunId = identity.LogicalRunId
              AuthorityRootUserMessageId = identity.AuthorityRootUserMessageId }
            identity.OwnerLogicalRunId
            attachmentKind
            associationGeneration
            outcome
