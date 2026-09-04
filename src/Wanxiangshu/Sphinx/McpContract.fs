namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop

/// MCP affordance contract: translates kernel decisions and session outcomes
/// into tool-level DTOs (success payloads, typed error views, human summaries).
/// Pure F# + plain JS objects; the MCP SDK and schema validation stay in
/// McpServer.fs.
module McpContract =

    let toolStart = "start"
    let toolAssess = "assess"
    let toolPropose = "propose"
    let toolInvestigate = "investigate"
    let toolSynthesize = "synthesize"
    let toolStatus = "status"
    let toolCancel = "cancel"
    let toolResumeLegacy = "resume"

    let codeQuestionRequired = "QUESTION_REQUIRED"
    let codeMissingHandle = "MISSING_HANDLE"
    let codeUnknownHandle = "UNKNOWN_HANDLE"
    let codeInvalidObservation = "INVALID_OBSERVATION"
    let codeKernelRejected = "KERNEL_REJECTED"
    let codeAlreadyAnswered = "ALREADY_ANSWERED"

    /// The one translation from a kernel-decided pending Request to the tool
    /// the caller must use next. Not a scheduler: the kernel already decided.
    let nextTool (request: Request) : string =
        match request with
        | SemanticAssessmentRequest _ -> toolAssess
        | GenerateCandidatesRequest _ -> toolPropose
        | InvestigateRequest _ -> toolInvestigate
        | SynthesizeRequest _ -> toolSynthesize

    let private requestQuestion (request: Request) : string option =
        match request with
        | SemanticAssessmentRequest question -> Some question
        | InvestigateRequest action -> Some action.Question
        | GenerateCandidatesRequest _ -> None
        | SynthesizeRequest _ -> None

    let private yieldPayload (handle: string) (revision: int) (request: Request) =
        createObj
            [ "handle" ==> handle
              "status" ==> "yield"
              "revision" ==> revision
              "nextTool" ==> nextTool request
              "request" ==> Codec.requestObject request
              "answer" ==> null ]

    let private answeredPayload (handle: string) (answer: CanonicalAnswer) =
        createObj
            [ "handle" ==> handle
              "status" ==> "answered"
              "revision" ==> answer.Revision
              "nextTool" ==> null
              "request" ==> null
              "answer" ==> Codec.answerObject answer ]

    let successPayload (success: SessionSuccess) : obj =
        match success.Result with
        | InquiryResult.Yield request -> yieldPayload success.Handle success.State.Revision request
        | InquiryResult.Answered answer -> answeredPayload success.Handle answer
        | InquiryResult.Error message ->
            raise (InvalidOperationException($"session success cannot wrap kernel error: {message}"))

    let private activeStatusPayload (handle: string) (state: EpistemicState) : obj =
        match state.PendingRequest with
        | Some request ->
            createObj
                [ "handle" ==> handle
                  "status" ==> "active"
                  "revision" ==> state.Revision
                  "nextTool" ==> nextTool request
                  "request" ==> Codec.requestObject request ]
        | None -> raise (InvalidOperationException "active session without pending kernel request")

    let statusPayload (handle: string) (status: SessionStatus) : obj =
        match status with
        | SessionStatus.Active state -> activeStatusPayload handle state
        | SessionStatus.Answered(answer, _) ->
            createObj
                [ "handle" ==> handle
                  "status" ==> "answered"
                  "revision" ==> answer.Revision
                  "nextTool" ==> null
                  "request" ==> null
                  "answer" ==> Codec.answerObject answer ]

    let cancelPayload (handle: string) : obj =
        createObj [ "handle" ==> handle; "status" ==> "cancelled" ]

    /// DSL-class: ExternalSignal — Sphinx wire error projection for the external caller.
    type ErrorView =
        { Code: string
          Message: string
          Recoverable: bool
          Retryable: bool
          NextAction: string
          Handle: string option
          Revision: int option
          ExpectedTool: string option }

    let errorObject (view: ErrorView) : obj =
        createObj
            [ "code" ==> view.Code
              "message" ==> view.Message
              "recoverable" ==> view.Recoverable
              "retryable" ==> view.Retryable
              "nextAction" ==> view.NextAction
              "handle" ==> (view.Handle |> Option.map box |> Option.toObj)
              "revision" ==> (view.Revision |> Option.map box |> Option.toObj)
              "expectedTool" ==> (view.ExpectedTool |> Option.map box |> Option.toObj) ]

    let private view code message recoverable retryable nextAction handle revision expectedTool =
        { Code = code
          Message = message
          Recoverable = recoverable
          Retryable = retryable
          NextAction = nextAction
          Handle = handle
          Revision = revision
          ExpectedTool = expectedTool }

    let questionRequiredView (message: string) : ErrorView =
        view codeQuestionRequired message true false "Call start with a non-empty question." None None None

    let invalidObservationView (handle: string option) (message: string) : ErrorView =
        view
            codeInvalidObservation
            message
            true
            false
            "Fix the observation payload and call the tool named by nextTool for the same inquiry."
            handle
            None
            None

    let private kernelRejectedAction (expectedTool: string option) : string =
        match expectedTool with
        | Some tool -> $"Call {tool} for the same inquiry."
        | None -> "Call status for this handle and follow nextTool."

    /// Session-layer failure → typed MCP error view. Revision and expectedTool
    /// come from the pre-failure state the kernel refused to advance.
    let failureView (failure: SessionFailureView) : ErrorView =
        let revision = failure.State |> Option.map (fun state -> state.Revision)

        let expectedTool =
            failure.State
            |> Option.bind (fun state -> state.PendingRequest)
            |> Option.map nextTool

        match failure.Failure with
        | SessionFailure.MissingHandle ->
            view
                codeMissingHandle
                "missing handle"
                true
                false
                "Pass the opaque inquiry handle returned by start."
                None
                None
                None
        | SessionFailure.UnknownHandle ->
            view
                codeUnknownHandle
                "unknown handle"
                false
                false
                "Start a new inquiry with start; this handle is unknown to this store. Cancelled handles stay unknown; durable handles recover by replaying their accepted observations into a fresh store before retry."
                failure.Handle
                None
                None
        | SessionFailure.InvalidObservation message ->
            { invalidObservationView failure.Handle message with
                Revision = revision }
        | SessionFailure.KernelRejected message ->
            view
                codeKernelRejected
                message
                true
                false
                (kernelRejectedAction expectedTool)
                failure.Handle
                revision
                expectedTool
        | SessionFailure.AlreadyAnswered ->
            view
                codeAlreadyAnswered
                "already answered"
                false
                false
                "Use the completed answer. Do not submit another observation."
                failure.Handle
                revision
                None

    let private questionLine (request: Request) =
        match requestQuestion request with
        | Some question -> $"\nQuestion: {question}"
        | None -> ""

    let summarizeSuccess (success: SessionSuccess) : string =
        match success.Result with
        | InquiryResult.Yield request ->
            "Sphinx inquiry yielded.\n"
            + $"Handle: {success.Handle}\n"
            + $"Next tool: {nextTool request}\n"
            + $"Revision: {success.State.Revision}{questionLine request}"
        | InquiryResult.Answered answer ->
            "Sphinx inquiry answered.\n"
            + $"Handle: {success.Handle}\n"
            + $"Revision: {answer.Revision}\n"
            + $"Stop reason: {answer.StopReason}"
        | InquiryResult.Error message ->
            raise (InvalidOperationException $"session success cannot wrap kernel error: {message}")

    let private summarizeActive (handle: string) (state: EpistemicState) : string =
        match state.PendingRequest with
        | Some request ->
            "Sphinx inquiry active.\n"
            + $"Handle: {handle}\n"
            + $"Next tool: {nextTool request}\n"
            + $"Revision: {state.Revision}{questionLine request}"
        | None -> raise (InvalidOperationException "active session without pending kernel request")

    let summarizeStatus (handle: string) (status: SessionStatus) : string =
        match status with
        | SessionStatus.Active state -> summarizeActive handle state
        | SessionStatus.Answered(answer, _) ->
            "Sphinx inquiry answered.\n"
            + $"Handle: {handle}\n"
            + $"Revision: {answer.Revision}\n"
            + $"Stop reason: {answer.StopReason}"

    let summarizeCancel () : string = "Sphinx inquiry cancelled."

    let summarizeError (view: ErrorView) : string =
        $"Sphinx tool error [{view.Code}]: {view.Message}\n"
        + $"Next action: {view.NextAction}"
