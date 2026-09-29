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
        | Some established when established.acceptedEvidence <> attemptedEvidence ->
            Error(ChatAdmissionError.ExistingEvidenceConflict(established.acceptedEvidence, attemptedEvidence))
        | None -> Ok(ChatAdmissionIntent.NeedAcceptance attemptedEvidence)
        | Some(ChatExecutionState.Accepted evidence) -> Ok(ChatAdmissionIntent.ResumeAccepted evidence)
        | Some(ChatExecutionState.Started evidence) -> Ok(ChatAdmissionIntent.AlreadyStarted evidence)
        | Some(ChatExecutionState.EndedBeforeStart(_, outcome)) ->
            Ok(ChatAdmissionIntent.AlreadyTerminal(PreStartOutcome.disposition outcome))
        | Some(ChatExecutionState.EndedAfterStart(_, disposition)) ->
            Ok(ChatAdmissionIntent.AlreadyTerminal disposition)

    let decide
        (message: ChatAdmissionMessage)
        (attemptedEvidence: AcceptedChatExecutionEvidence)
        (suppliedState: ChatExecutionState option)
        : Result<ChatAdmissionIntent, ChatAdmissionError> =
        let expectedKey = messageKey message

        match suppliedState with
        | Some state when state.key <> expectedKey -> Error(ChatAdmissionError.StateKeyMismatch(state.key, expectedKey))
        | Some(ChatExecutionState.EndedBeforeStart(_, outcome)) ->
            Ok(ChatAdmissionIntent.AlreadyTerminal(PreStartOutcome.disposition outcome))
        | Some(ChatExecutionState.EndedAfterStart(_, disposition)) ->
            Ok(ChatAdmissionIntent.AlreadyTerminal disposition)
        | state ->
            AcceptedChatExecutionEvidence.validate attemptedEvidence
            |> Result.mapError ChatAdmissionError.AttemptEvidenceInvalid
            |> Result.bind (fun () -> validateAttemptKey expectedKey attemptedEvidence)
            |> Result.bind (fun () -> validateExplicitAgent message attemptedEvidence)
            |> Result.bind (fun () -> classifyState attemptedEvidence state)
