namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
type internal ChatAdmissionBindingKind =
    | ExternalRoot
    | ActiveHumanContinuation
    | PendingPrompt of PromptKey

type internal ChatAdmissionBindingReceipt =
    private
        { Identity: ExecutionAdmissionExactIdentity
          Kind: ChatAdmissionBindingKind }

type internal HostModelProjectionReceipt =
    private | HostModelProjectionReceipt of ExecutionAdmissionExactIdentity * OpencodeModel

[<RequireQualifiedAccess>]
/// DSL-class: ExternalSignal
type internal ChatAdmissionTransactionStep =
    | ResolveState
    | Accept
    | AcceptedWitness
    | AcquireLease
    | LeaseTarget
    | BindExecution
    | ProjectHost
    | CommitLease
    | TerminalizeAccepted
    | UnbindExecution
    | ReleaseBeforeProvider
    | Settled

[<RequireQualifiedAccess>]
type internal ChatAdmissionTransactionOutcome =
    | Settled of
        witness: ManagedChatAcceptanceWitness *
        target: ModelRoutingTarget *
        binding: ChatAdmissionBindingReceipt *
        hostProjection: HostModelProjectionReceipt
    | Superseded of ManagedChatAcceptanceWitness
    | CapacityQueueFull of ManagedChatAcceptanceWitness
    | Cancelled of ManagedChatAcceptanceWitness
    | AlreadyStarted of ProviderStartedEvidence
    | AlreadyTerminal of ChatExecutionTerminalDisposition

[<RequireQualifiedAccess>]
type internal ChatAdmissionReleaseOutcome =
    | Settled of CapacityTransitionOutcome
    | BoundaryFailed of exn

[<RequireQualifiedAccess>]
/// DSL-class: Evidence
type internal ChatAdmissionTransactionError =
    | AdmissionRejected of ChatAdmissionError
    | AcceptanceFailed of ManagedChatAcceptanceError
    | AcceptanceBoundaryFailed of exn
    | PreProviderSettlementFailed of PreProviderSettlementError
    | PreProviderSettlementBoundaryFailed of exn
    | PreProviderUnbindBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseAcquisitionFailed of exn
    | LeaseTargetFailed of ExecutionAdmissionRejection * release: ChatAdmissionReleaseOutcome
    | LeaseTargetBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseTargetProjectionFailed of exn * release: ChatAdmissionReleaseOutcome
    | BindingFailed of exn * release: ChatAdmissionReleaseOutcome
    | HostProjectionFailed of exn * release: ChatAdmissionReleaseOutcome
    | LeaseCommitFailed of commit: CapacityTransitionOutcome * release: ChatAdmissionReleaseOutcome
    | LeaseCommitBoundaryFailed of exn * release: ChatAdmissionReleaseOutcome

type internal ChatAdmissionTransactionInput =
    { Intent: ChatAdmissionIntent.Decision
      CurrentState: ChatExecutionState option }

type internal ChatAdmissionTransactionPorts =
    { Accept: ChatAdmissionIntent.Decision -> Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>
      Acquire: ManagedChatAcceptanceWitness -> Task<Result<ExecutionAdmissionAcquisition, exn>>
      LeaseTarget: ExecutionAdmissionLease -> Result<ModelRoutingTarget, ExecutionAdmissionRejection>
      Bind: ChatAdmissionIntent.Decision -> ManagedChatAcceptanceWitness -> OpencodeModel -> Result<unit, exn>
      ProjectHost: OpencodeModel -> Result<unit, exn>
      Commit: ExecutionAdmissionLease -> ExecutionAdmissionExactIdentity -> CapacityTransitionOutcome
      ReleaseBeforeProvider: ExecutionAdmissionLease -> CapacityTransitionOutcome
      SettlePreProvider:
          ChatExecutionKey
              -> AcceptedChatExecutionEvidence
              -> ChatExecutionTerminalDisposition
              -> Task<Result<PreProviderTerminalWitness, PreProviderSettlementError>>
      Unbind: ChatExecutionKey -> unit }

[<RequireQualifiedAccess>]
module internal ChatAdmissionTransaction =
    val executeWith:
        observe: (ChatAdmissionTransactionStep -> unit) ->
        ports: ChatAdmissionTransactionPorts ->
        input: ChatAdmissionTransactionInput ->
            Task<Result<ChatAdmissionTransactionOutcome, ChatAdmissionTransactionError>>

    val production:
        journal: AgentJournal ->
        acceptManagedIntent:
            (ChatAdmissionIntent.Decision -> Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>) ->
        projectHostModel: (OpencodeModel -> Result<unit, exn>) ->
            ChatAdmissionTransactionPorts

    val execute:
        ports: ChatAdmissionTransactionPorts ->
        input: ChatAdmissionTransactionInput ->
            Task<Result<ChatAdmissionTransactionOutcome, ChatAdmissionTransactionError>>
