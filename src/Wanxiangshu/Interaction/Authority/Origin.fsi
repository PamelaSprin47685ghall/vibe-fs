namespace Wanxiangshu.Interaction.Authority

type PromptRootAuthorityKind =
    | HumanRoot
    | AgentOwnerRoot

type PromptContinuationKind =
    | InteractionRepair
    | JoinGuard
    | ManagerGuard
    | ReviewerGuard
    | BusyAgentNudge
    | HumanMessage
    | ManagedDelegationAssignment
    | ProviderRetryAttempt
    | DegenerationGuard
    | FissionHandoff
    | ManagerIdleEncouragement
    | FinalityRejected
    | FinalitySteer

type PromptOrigin =
    | AuthorityRoot of PromptRootAuthorityKind
    | Continuation of PromptContinuationKind
    | HostInternal
    | UnknownOrigin
