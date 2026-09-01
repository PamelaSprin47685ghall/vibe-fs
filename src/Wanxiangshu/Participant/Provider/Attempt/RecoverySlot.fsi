namespace Wanxiangshu.Participant.Provider.Attempt

[<RequireQualifiedAccess>]
type SlotArming =
    | NotArmed
    | ArmedByAdvance

[<RequireQualifiedAccess>]
type RecoveryOpportunity =
    | OrdinaryAttempt
    | RecoveryAttempt

[<RequireQualifiedAccess>]
type BloggerSlotDispatchError =
    | MissingProjection
    | NoActiveBloggerRun

[<RequireQualifiedAccess>]
type AttemptOutcome =
    | Completed
    | CompletedInvalid
    | Failed
    | Aborted

[<RequireQualifiedAccess>]
type SlotDecision =
    | CommitSquashThenMain
    | MainWithoutSquash
    | CommitMain of clearsFailureCount: bool
    | RepairOnce
    | AbandonRoundProduct
    | FailSlot

[<RequireQualifiedAccess>]
module RecoverySlot =
    val beginSequence: SlotArming
    val afterFailureAdvance: SlotArming
    val afterRestart: SlotArming
    val isArmed: arming: SlotArming -> bool
    val opportunity: arming: SlotArming -> offset: AgentPairCursor.FallbackOffset -> RecoveryOpportunity
    val mayRecover: arming: SlotArming -> offset: AgentPairCursor.FallbackOffset -> hasMaterial: bool -> bool
    val nextBloggerRequest:
        failedKind: ProviderRequestKind ->
        nextOpportunity: RecoveryOpportunity ->
        hasSquashMaterial: bool ->
        Result<ProviderRequestKind, BloggerSlotDispatchError>
    val onSquashOutcome: outcome: AttemptOutcome -> SlotDecision
    val onMainOutcome: kind: ProviderRequestKind -> aabbConsumed: bool -> outcome: AttemptOutcome -> SlotDecision
    val advancesCursor: decision: SlotDecision -> bool
    val nextArming: decision: SlotDecision -> SlotArming
