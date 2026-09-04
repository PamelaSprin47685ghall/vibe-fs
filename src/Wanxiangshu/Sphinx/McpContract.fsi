namespace Wanxiangshu.Sphinx

module McpContract =
    val toolStart: string
    val toolAssess: string
    val toolPropose: string
    val toolInvestigate: string
    val toolSynthesize: string
    val toolStatus: string
    val toolCancel: string
    val toolResumeLegacy: string

    val codeQuestionRequired: string
    val codeMissingHandle: string
    val codeUnknownHandle: string
    val codeInvalidObservation: string
    val codeKernelRejected: string
    val codeAlreadyAnswered: string

    val nextTool: request: Request -> string
    val successPayload: success: SessionSuccess -> obj
    val statusPayload: handle: string -> status: SessionStatus -> obj
    val cancelPayload: handle: string -> obj

    type ErrorView =
        { Code: string
          Message: string
          Recoverable: bool
          Retryable: bool
          NextAction: string
          Handle: string option
          Revision: int option
          ExpectedTool: string option }

    val errorObject: view: ErrorView -> obj
    val questionRequiredView: message: string -> ErrorView
    val invalidObservationView: handle: string option -> message: string -> ErrorView
    val failureView: failure: SessionFailureView -> ErrorView
    val summarizeSuccess: success: SessionSuccess -> string
    val summarizeStatus: handle: string -> status: SessionStatus -> string
    val summarizeCancel: unit -> string
    val summarizeError: view: ErrorView -> string
