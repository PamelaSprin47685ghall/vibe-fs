namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Foundation.Identity

type AuthorityRootAcceptedPayload =
    { SchemaVersion: int
      SessionId: SessionId
      LogicalRunId: LogicalRunId
      AuthorityRootUserMessageId: AuthorityRootUserMessageId
      AuthorityKind: string
      IdentitySeed: PromptIdentitySeed }

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
    | UserQuestionAnswered of UserQuestionAnsweredPayload
