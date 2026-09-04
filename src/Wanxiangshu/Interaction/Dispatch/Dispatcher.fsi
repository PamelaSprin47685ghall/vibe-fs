namespace Wanxiangshu.Interaction.Dispatch

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

[<RequireQualifiedAccess>]
module PromptDispatcher =
    val internal originLabel: (PromptAuthority.PromptOrigin -> string)

    [<RequireQualifiedAccess>]
    type AuthorityRegistrationFailure =
        | RegistrationRejected of PromptAuthorityRun.AuthorityRegistrationRejection
        | PersistenceRejected of string

    [<RequireQualifiedAccess>]
    type HumanRootAcceptanceFailure =
        | IdentityRejected of string
        | AuthorityRegistrationRejected of AuthorityRegistrationFailure

    val describeAuthorityRegistrationFailure: AuthorityRegistrationFailure -> string
    val describeHumanRootAcceptanceFailure: HumanRootAcceptanceFailure -> string

    [<RequireQualifiedAccess>]
    type AwaitMode =
        | Await
        | Detached

    [<RequireQualifiedAccess>]
    type internal SendAttemptOutcome =
        | Sent of PromptKey
        | AdmissionRejected of QuiescencePermitFailure
        | NotSent of string
        | Failed of string

    val internal describeIdentitySeedRejection: rejection: PromptAuthority.IdentitySeedValidationError -> string

    type Runtime =
        new: journal: AgentJournal -> Runtime
        member RuntimeId: RuntimeId
        member ProjectionFor: sessionId: SessionId -> PromptAuthority.PromptAuthorityProjection

        member AcceptManagedChatIntent:
            intent: ChatAdmissionIntent.Decision ->
                Task<Result<ManagedChatAcceptanceWitness, ManagedChatAcceptanceError>>

        member internal Persist:
            sessionId: SessionId ->
            providerRun: ProviderRunIdentity option ->
            fact: AgentFact ->
                Task<Result<unit, string>>

        member RegisterAuthority:
            profile: PromptAuthority.AuthorityExecutionProfile ->
                Task<Result<PromptAuthority.AuthorityExecutionProfile, AuthorityRegistrationFailure>>

        member AcceptHumanRoot:
            sessionId: SessionId ->
            physicalMessageId: PhysicalUserMessageId ->
            identitySeed: PromptAuthority.IdentitySeed option ->
                Task<Result<PromptAuthority.AuthorityExecutionProfile, HumanRootAcceptanceFailure>>

        member Abandon:
            key: PromptKey -> sessionId: SessionId -> reason: PromptAbandonReason -> Task<Result<unit, string>>

        member internal ValidateAgentOwnerIdentitySeed:
            identitySeed: PromptAuthority.IdentitySeed ->
                Result<ParticipantIdentityEvidence, PromptAuthority.IdentitySeedValidationError>

        member internal AcceptPhysicalAgentOwnerRoot:
            key: PromptKey ->
            sessionId: SessionId ->
            physicalMessageId: PhysicalUserMessageId ->
            identitySeed: PromptAuthority.IdentitySeed ->
                Task<Result<PromptAuthority.AuthorityExecutionProfile, string>>

        member AcceptAgentOwnerRoot:
            key: PromptKey ->
            sessionId: SessionId ->
            physicalMessageId: PhysicalUserMessageId ->
                Task<Result<PromptAuthority.AuthorityExecutionProfile, string>>

        member AcceptContinuation:
            key: PromptKey ->
            sessionId: SessionId ->
            physicalMessageId: PhysicalUserMessageId ->
                Task<Result<PromptAuthority.ContinuationKind option, string>>

        member ActiveProfile: sessionId: SessionId -> PromptAuthority.AuthorityExecutionProfile option

        member ResolveOrigin:
            physicalMessageId: PhysicalUserMessageId ->
            promptKey: PromptKey option ->
            hostCompaction: bool ->
            sessionId: SessionId ->
                PromptAuthority.PromptOrigin

        member PendingClaim: sessionId: SessionId * promptKey: PromptKey -> PromptAuthority.PromptClaim option
        member DispatchAccepted: sessionId: SessionId * claim: PromptAuthority.PromptClaim -> bool

        member GateNudgeAlreadyAdmitted:
            profile: PromptAuthority.AuthorityExecutionProfile ->
            continuation: PromptAuthority.ContinuationKind ->
            gateKind: string ->
            terminalProviderRun: ProviderRunIdentity ->
                bool

        member GateNudgeAcceptedPhysical:
            profile: PromptAuthority.AuthorityExecutionProfile ->
            continuation: PromptAuthority.ContinuationKind ->
            gateKind: string ->
            terminalProviderRun: ProviderRunIdentity ->
                PhysicalUserMessageId option

        member RepairAlreadyClaimed:
            profile: PromptAuthority.AuthorityExecutionProfile ->
            requestId: BloggerRequestId ->
            terminalProviderRun: ProviderRunIdentity ->
            repairKind: string ->
                bool

        member internal Metadata: key: PromptKey -> origin: string -> logicalRunId: LogicalRunId option -> obj
        member internal SubscribeNoOp: port: ISessionHostPort -> sessionId: SessionId -> IDisposable

    val forJournal: journal: AgentJournal -> Runtime
