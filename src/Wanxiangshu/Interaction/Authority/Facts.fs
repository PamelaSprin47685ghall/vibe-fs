namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation
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

/// The exact durable closure of one logical run. It carries the accepted
/// root identity and the exact retirement witness that completed the run.
type AuthorityLogicalRunClosedPayload =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      RootKind: string
      ClosureWitness: string }

/// Durable prompt-authority facts. These facts belong to the authority boundary;
/// Composition only routes the family through the journal's outer union.
[<RequireQualifiedAccess>]
type PromptAbandonReason =
    /// Transport proved the prompt was not accepted.
    | SendFailed of error: string
    /// An idle-derived prompt was durably claimed, but newer physical user
    /// material revoked its quiescence permit before SendPrompt was invoked.
    | SupersededBeforePhysicalSend
    /// Recovery budget expired without proving physical acceptance.
    | UnresolvedAfterRecovery

/// 提问只暂停当前委托，不结束逻辑运行；回执绑定真实调用，不能用文字声称代替。
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

/// The durable source terminal of one authority lifecycle.
///
/// A lifecycle owner states which kind of terminal it reached; the closure
/// later cites exactly one of these, and only if its kind matches the kind
/// the accepted root committed to.
type AuthorityTerminalSourcePayload =
    | ManagedRunTerminal of AuthorityManagedRunTerminalPayload
    | ChildWorkTerminal of AuthorityChildWorkTerminalPayload
    | AttachedWorkTerminal of AuthorityAttachedWorkTerminalPayload
    | InternalLeafTerminal of AuthorityInternalLeafTerminalPayload

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
