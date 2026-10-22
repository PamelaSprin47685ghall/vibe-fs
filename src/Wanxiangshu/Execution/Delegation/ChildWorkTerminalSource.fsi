namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority

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
    val childWorkTerminal:
        sha256: (string -> string) ->
        identity: ChildWorkTerminalIdentity ->
        outcome: AuthorityTerminalSource.TerminalOutcome ->
            AuthorityChildWorkTerminalPayload

    /// The durable source terminal of an attached work.
    ///
    /// An attached work is closed by its owner with an attached-work closure,
    /// so its terminal carries the attachment kind and the association
    /// generation that identify the attachment it belongs to.
    val attachedWorkTerminal:
        sha256: (string -> string) ->
        identity: ChildWorkTerminalIdentity ->
        attachmentKind: string ->
        associationGeneration: string ->
        outcome: AuthorityTerminalSource.TerminalOutcome ->
            AuthorityAttachedWorkTerminalPayload
