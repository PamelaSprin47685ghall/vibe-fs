namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

module SessionExecutionBinding =
    [<RequireQualifiedAccess>]
    type ProviderStartObservationError<'bindingError> =
        | DurableJournalUnavailable
        | PhysicalUserMessageMissing of SessionId
        | AttemptPlanFreezeFailed of 'bindingError
        | FrozenAttemptPlanMissing of ChatExecutionKey * ProviderRunIdentity
        | AcceptedExecutionMissing of ChatExecutionKey
        | AcceptedExecutionAlreadyTerminal of ChatExecutionKey
        | BloggerRequestMissing of SessionId
        | BloggerRequestKindUnsupported of string
        | AuthorityEvidenceInvalid of AcceptedChatExecutionEvidence
        | PersistenceFailed of ManagedChatProviderLifecycleError

    val providerStartObservationErrorCode: ProviderStartObservationError<'bindingError> -> string

    val exactExecutionBindingCount: sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> int

    val releaseAcceptedExecution: sessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> unit

    val bind: parentId: SessionId -> childId: SessionId -> agent: string option -> unit
    val restore: parentId: SessionId -> childId: SessionId -> agent: string option -> unit
    val bindInternalRoot: sessionId: SessionId -> agent: string option -> unit
    val isInternalRoot: sessionId: SessionId -> bool
    val tryParent: sessionId: SessionId -> SessionId option
    val tryAgent: sessionId: SessionId -> string option
    val isUnboundHostAuxiliaryChild: sessionId: SessionId -> bool
    val observeHostAuxiliaryChild: sessionId: SessionId -> unit
    val observeUserFacingAgent: sessionId: SessionId -> agent: string -> unit

    val acceptExternalExecution:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId ->
        effectiveAgent: string ->
        model: OpencodeModel ->
            unit

    val acceptPromptExecution:
        sessionId: SessionId ->
        promptKey: PromptKey ->
        physicalUserMessageId: PhysicalUserMessageId ->
        effectiveAgent: string ->
        model: OpencodeModel ->
            unit

    val beginProviderAttempt:
        sessionId: SessionId ->
        physicalUserMessageId: PhysicalUserMessageId option ->
        promptKey: PromptKey option ->
            Result<unit, string>

    val currentProviderModel: sessionId: SessionId -> OpencodeModel option

    val endProviderStepAtToolBoundary:
        sessionId: SessionId -> providerRunId: ProviderRunIdentity option -> Result<unit, string>

    val beginPhysicalProviderAttemptForTransform:
        beginQuiescence: (SessionId -> unit) -> projectionSessionIdOpt: string option -> outObj: obj -> Task<unit>

    val freezeProviderAttemptPlanForTransform:
        journal: AgentJournal option ->
        freezeAttemptPlan: (SessionId -> PhysicalUserMessageId -> PendingAttemptPlan -> Result<unit, 'bindingError>) ->
        projectionSessionIdOpt: string option ->
        outObj: obj ->
            Task<Result<unit, ProviderStartObservationError<'bindingError>>>

    val persistProviderStartedFromObservation:
        journal: AgentJournal option ->
        bindAttemptPlan: (SessionId -> PhysicalUserMessageId -> ProviderRunIdentity -> AttemptPlan option) ->
        observation: ExactProviderStartObservation ->
            Task<Result<bool, ProviderStartObservationError<unit>>>

    val drop: sessionId: SessionId -> unit
    val cancelUnacquired: sessionId: SessionId -> unit
    val requiresProviderBindingProof: sessionId: SessionId -> bool

    val validateObservedProvider: sessionId: SessionId -> agent: string -> model: OpencodeModel -> Result<bool, string>

    val effectiveAgent: sessionId: SessionId -> opts: OpenCodePromptOptions -> Result<string, string>

    val prepareManagedPrompt:
        sessionId: SessionId -> opts: OpenCodePromptOptions -> Result<OpenCodePromptOptions, string>

    val prepareUserFacingPrompt:
        sessionId: SessionId -> opts: OpenCodePromptOptions -> Result<OpenCodePromptOptions, string>
