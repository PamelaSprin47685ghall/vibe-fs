namespace Wanxiangshu.Interaction.Authority

type PromptRootAuthorityKind =
    | HumanRoot
    | AgentOwnerRoot

type PromptContinuationKind =
    | InteractionRepair
    | JoinGuard
    | ManagerGuard
    | BusyAgentNudge
    | HumanMessage
    | ManagedDelegationAssignment
    | ProviderRetryAttempt
    | FissionHandoff

type PromptOrigin =
    | AuthorityRoot of PromptRootAuthorityKind
    | Continuation of PromptContinuationKind
    | HostInternal
    | UnknownOrigin
