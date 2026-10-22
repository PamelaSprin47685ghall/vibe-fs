namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity

type AuthorityRootAcceptedPayload =
    {
        SchemaVersion: int
        SessionId: SessionId
        LogicalRunId: LogicalRunId
        AuthorityRootUserMessageId: AuthorityRootUserMessageId
        AuthorityKind: string
        /// The single closure kind this root acceptance must close under.
        ExpectedClosureKind: string
        /// The owner logical run id of an agent-owner root (None for HumanRoot).
        OwnerLogicalRunId: string option
        /// The agent-owner life of an agent-owner root: "Child" | "Attached" | "InternalLeaf".
        AgentOwnerLife: string option
        IdentitySeed: PromptIdentitySeed
    }

type AuthorityLogicalRunClosedPayload =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      RootKind: string
      ClosureWitness: string }

[<RequireQualifiedAccess>]
type PromptAbandonReason =
    | SendFailed of error: string
    | SupersededBeforePhysicalSend
    | UnresolvedAfterRecovery

type UserQuestionAskedPayload =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      PhysicalUserMessageId: PhysicalUserMessageId
      ProviderRunId: ProviderRunIdentity
      ToolCallId: ToolCallId
      Question: string }

type UserQuestionAnsweredPayload =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      ToolCallId: ToolCallId
      PhysicalUserMessageId: PhysicalUserMessageId }

/// The durable terminal outcome of a managed (non-Manager) HumanRoot run.
/// It is the unique legal source witness for `HumanRootManagedRun`.
type AuthorityManagedRunTerminalPayload =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      FactId: string }

/// The durable terminal outcome of an agent-owner child work.
type AuthorityChildWorkTerminalPayload =
    { SessionId: SessionId
      OwnerLogicalRunId: string
      ChildLogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      FactId: string }

/// The durable terminal outcome of an agent-owner attached work.
type AuthorityAttachedWorkTerminalPayload =
    { SessionId: SessionId
      OwnerLogicalRunId: string
      ChildLogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      AttachmentKind: string
      AssociationGeneration: string
      FactId: string }

/// The durable terminal outcome of an agent-owner internal leaf.
type AuthorityInternalLeafTerminalPayload =
    { SessionId: SessionId
      OwnerLogicalRunId: string
      LeafLogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      DecisionOrTransactionId: string
      FactId: string }

type PromptFactCases =
    | PluginPromptClaimed of
        {| PromptKey: PromptKey
           SessionId: SessionId
           ContinuationKind: string
           LogicalRunId: LogicalRunId option
           AuthorityRootUserMessageId: AuthorityRootUserMessageId option
           IdentitySeed: PromptIdentitySeed
           PayloadDigest: string |}
    | PluginPromptSubmitted of
        {| PromptKey: PromptKey
           SessionId: SessionId
           Receipt: TransportReceipt |}
    | PluginPromptPhysicalAccepted of
        {| PromptKey: PromptKey
           SessionId: SessionId
           PhysicalUserMessageId: PhysicalUserMessageId |}
    | PluginPromptAbandoned of
        {| PromptKey: PromptKey
           SessionId: SessionId
           Reason: PromptAbandonReason |}
    | AuthorityRootAccepted of AuthorityRootAcceptedPayload
    | AuthorityLogicalRunClosed of AuthorityLogicalRunClosedPayload
    | UserQuestionAsked of UserQuestionAskedPayload
    | AuthorityManagedRunTerminal of AuthorityManagedRunTerminalPayload
    | AuthorityChildWorkTerminal of AuthorityChildWorkTerminalPayload
    | AuthorityAttachedWorkTerminal of AuthorityAttachedWorkTerminalPayload
    | AuthorityInternalLeafTerminal of AuthorityInternalLeafTerminalPayload
    | UserQuestionAnswered of UserQuestionAnsweredPayload
