namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Participant.Persona

type ChatAdmissionMessage =
    { SessionId: SessionId
      PhysicalUserMessageId: PhysicalUserMessageId
      ExplicitAgent: string option }

[<RequireQualifiedAccess>]
type ChatAdmissionIntent =
    | AlreadyTerminal of ChatExecutionTerminalDisposition
    | AlreadyStarted of ProviderStartedEvidence
    | ResumeAccepted of AcceptedChatExecutionEvidence
    | NeedAcceptance of AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
type ChatAdmissionError =
    | StateKeyMismatch of suppliedStateKey: ChatExecutionKey * messageKey: ChatExecutionKey
    | AttemptKeyMismatch of attemptKey: ChatExecutionKey * messageKey: ChatExecutionKey
    | ExplicitAgentMismatch of explicitAgent: string * selectedAgent: string
    | AttemptEvidenceInvalid of reason: string
    | ExistingEvidenceConflict of established: AcceptedChatExecutionEvidence * attempted: AcceptedChatExecutionEvidence

[<RequireQualifiedAccess>]
module ChatAdmission =

    let private messageKey (message: ChatAdmissionMessage) : ChatExecutionKey =
        { SessionId = message.SessionId
          PhysicalUserMessageId = message.PhysicalUserMessageId }

    let private attemptKey (evidence: AcceptedChatExecutionEvidence) : ChatExecutionKey =
        { SessionId = evidence.SessionId
          PhysicalUserMessageId = evidence.PhysicalUserMessageId }

    let private validateAttemptKey (expectedKey: ChatExecutionKey) (attemptedEvidence: AcceptedChatExecutionEvidence) =
        let suppliedAttemptKey = attemptKey attemptedEvidence

        if suppliedAttemptKey = expectedKey then
            Ok()
        else
            Error(ChatAdmissionError.AttemptKeyMismatch(suppliedAttemptKey, expectedKey))

    let private validateExplicitAgent
        (message: ChatAdmissionMessage)
        (attemptedEvidence: AcceptedChatExecutionEvidence)
        =
        let selectedAgent =
            attemptedEvidence.IdentitySeed
            |> PromptIdentitySeed.participantIdentity
            |> ParticipantIdentity.selectedAgent

        match message.ExplicitAgent with
        | Some explicitAgent when explicitAgent <> selectedAgent ->
            Error(ChatAdmissionError.ExplicitAgentMismatch(explicitAgent, selectedAgent))
        | _ -> Ok()

    let private classifyState (attemptedEvidence: AcceptedChatExecutionEvidence) (state: ChatExecutionState option) =
        match state with
        | Some established when established.Evidence <> attemptedEvidence ->
            Error(ChatAdmissionError.ExistingEvidenceConflict(established.Evidence, attemptedEvidence))
        | None -> Ok(ChatAdmissionIntent.NeedAcceptance attemptedEvidence)
        | Some { Lifecycle = ChatExecutionLifecycle.Accepted
                 Evidence = evidence } -> Ok(ChatAdmissionIntent.ResumeAccepted evidence)
        | Some { Lifecycle = ChatExecutionLifecycle.ProviderStarted
                 ProviderStarted = Some evidence } -> Ok(ChatAdmissionIntent.AlreadyStarted evidence)
        | Some { Lifecycle = ChatExecutionLifecycle.ProviderStarted
                 ProviderStarted = None } -> invalidOp "ProviderStarted projection is missing exact provider evidence"
        | Some { Lifecycle = ChatExecutionLifecycle.Terminal disposition } ->
            Ok(ChatAdmissionIntent.AlreadyTerminal disposition)

    let decide
        (message: ChatAdmissionMessage)
        (attemptedEvidence: AcceptedChatExecutionEvidence)
        (suppliedState: ChatExecutionState option)
        : Result<ChatAdmissionIntent, ChatAdmissionError> =
        let expectedKey = messageKey message

        match suppliedState with
        | Some state when state.Key <> expectedKey -> Error(ChatAdmissionError.StateKeyMismatch(state.Key, expectedKey))
        | Some { Lifecycle = ChatExecutionLifecycle.Terminal disposition } ->
            Ok(ChatAdmissionIntent.AlreadyTerminal disposition)
        | state ->
            AcceptedChatExecutionEvidence.validate attemptedEvidence
            |> Result.mapError ChatAdmissionError.AttemptEvidenceInvalid
            |> Result.bind (fun () -> validateAttemptKey expectedKey attemptedEvidence)
            |> Result.bind (fun () -> validateExplicitAgent message attemptedEvidence)
            |> Result.bind (fun () -> classifyState attemptedEvidence state)
