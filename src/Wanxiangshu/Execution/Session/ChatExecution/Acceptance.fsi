namespace Wanxiangshu.Execution.Session.ChatExecution

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Failure
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Persistence.Journal

type ManagedChatAcceptanceWitness =
    private | ManagedChatAcceptanceWitness of ChatExecutionKey * AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
module ManagedChatAcceptanceWitness =
    val key: ManagedChatAcceptanceWitness -> ChatExecutionKey
    val evidence: ManagedChatAcceptanceWitness -> AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
type ManagedChatAcceptanceError =
    | IntentRejected of reason: string
    | AuthorityRegistrationRejected of PromptAuthorityRun.AuthorityRegistrationRejection
    | AttemptEvidenceInvalid of reason: string
    | AttemptKeyMismatch of evidenceKey: ChatExecutionKey * requestedKey: ChatExecutionKey
    | EstablishedEvidenceConflict of
        established: AcceptedChatExecutionEvidence *
        attempted: AcceptedChatExecutionEvidence
    | ProjectionMissingAfterCommit of ChatExecutionKey
    | ProjectionConflictAfterCommit of
        established: AcceptedChatExecutionEvidence *
        attempted: AcceptedChatExecutionEvidence
    | NotAttempted of EventId * JournalUnavailable
    | CommitUnknown of EventId * JournalFailure
    | FactRejected of EventId * FoldRejection

type internal ManagedChatAcceptancePersistence =
    { ReadExact: ChatExecutionKey -> ChatExecutionState option
      AppendAccepted: ChatExecutionKey -> AcceptedChatExecutionEvidence -> Task<Result<unit, JournalAppendFailure>> }

type ManagedChatProviderStartedWitness =
    private | ManagedChatProviderStartedWitness of ChatExecutionKey * ProviderStartedEvidence

[<RequireQualifiedAccess>]
module ManagedChatProviderStartedWitness =
    val key: ManagedChatProviderStartedWitness -> ChatExecutionKey
    val evidence: ManagedChatProviderStartedWitness -> ProviderStartedEvidence

type ManagedChatTerminalWitness =
    private | ManagedChatTerminalWitness of
        ChatExecutionKey *
        ChatExecutionTerminalEvidence *
        ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
module ManagedChatTerminalWitness =
    val key: ManagedChatTerminalWitness -> ChatExecutionKey
    val evidence: ManagedChatTerminalWitness -> ChatExecutionTerminalEvidence
    val disposition: ManagedChatTerminalWitness -> ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type ManagedChatProviderLifecycleError =
    | AttemptEvidenceInvalid of reason: string
    | AttemptKeyMismatch of evidenceKey: ChatExecutionKey * requestedKey: ChatExecutionKey
    | MissingAccepted of ChatExecutionKey
    | EstablishedEvidenceConflict of
        established: AcceptedChatExecutionEvidence *
        attempted: AcceptedChatExecutionEvidence
    | ProviderRunConflict of established: ProviderRunIdentity * attempted: ProviderRunIdentity
    | ProviderNotStarted of ChatExecutionKey
    | ProviderStartedAfterTerminal of ChatExecutionTerminalDisposition
    | TerminalConflict of established: ChatExecutionTerminalDisposition * attempted: ChatExecutionTerminalDisposition
    | ProjectionMissingAfterCommit of ChatExecutionKey
    | ProjectionConflictAfterCommit of ChatExecutionState
    | NotAttempted of EventId * JournalUnavailable
    | CommitUnknown of EventId * JournalFailure
    | FactRejected of EventId * FoldRejection

type internal ManagedChatProviderLifecyclePersistence =
    { ReadExact: ChatExecutionKey -> ChatExecutionState option
      AppendFact: ProviderStartedEvidence -> ChatExecutionFactCases -> Task<Result<unit, JournalAppendFailure>> }

[<RequireQualifiedAccess>]
module ManagedChatAcceptance =
    val internal evidenceFromIntent:
        authority: PromptAuthority.AuthorityExecutionProfile ->
        physicalUserMessageId: PhysicalUserMessageId ->
        origin: PromptOrigin ->
        effectiveAgent: string ->
            AcceptedChatExecutionEvidence

    val internal persistenceError: failure: JournalAppendFailure -> ManagedChatAcceptanceError

    val internal acceptWith:
        persistence: ManagedChatAcceptancePersistence ->
        key: ChatExecutionKey ->
        evidence: AcceptedChatExecutionEvidence ->
            Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>

    val internal accept:
        journal: AgentJournal ->
        key: ChatExecutionKey ->
        evidence: AcceptedChatExecutionEvidence ->
            Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>

[<RequireQualifiedAccess>]
module ManagedChatProviderLifecycle =
    val internal startWith:
        persistence: ManagedChatProviderLifecyclePersistence ->
        key: ChatExecutionKey ->
        acceptedEvidence: AcceptedChatExecutionEvidence ->
        providerRun: ProviderRunIdentity ->
        requestKind: ProviderRequestKind ->
        projectionChoice: XProjectionChoice ->
            Task<Result<ManagedChatProviderStartedWitness, ManagedChatProviderLifecycleError>>

    val internal terminalWith:
        persistence: ManagedChatProviderLifecyclePersistence ->
        key: ChatExecutionKey ->
        startedEvidence: ProviderStartedEvidence ->
        disposition: ChatExecutionTerminalDisposition ->
            Task<Result<ManagedChatTerminalWitness, ManagedChatProviderLifecycleError>>

    val internal providerStarted:
        journal: AgentJournal ->
        key: ChatExecutionKey ->
        acceptedEvidence: AcceptedChatExecutionEvidence ->
        providerRun: ProviderRunIdentity ->
        requestKind: ProviderRequestKind ->
        projectionChoice: XProjectionChoice ->
            Task<Result<ManagedChatProviderStartedWitness, ManagedChatProviderLifecycleError>>

    val internal terminal:
        journal: AgentJournal ->
        key: ChatExecutionKey ->
        startedEvidence: ProviderStartedEvidence ->
        disposition: ChatExecutionTerminalDisposition ->
            Task<Result<ManagedChatTerminalWitness, ManagedChatProviderLifecycleError>>
