// WHAT[EPI-030]: frozen-transcript replay over the public legacy adapter.
// Keeps one SessionStore per replayed inquiry in a module table; start mints a
// fresh inquiry id and ignores any frozen handle, later calls run through the
// live store handle. Every verdict comes from SessionStore + McpContract views.
namespace Wanxiangshu.Sphinx

open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop

module GecLegacy =

    let private stores = Dictionary<string, SessionStore>()

    let private liveHandles = Dictionary<string, string>()

    [<Import("randomUUID", "node:crypto")>]
    let private randomUUID () : string = jsNative

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private fieldOf (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private textOf (value: obj) : string =
        if isNullish value then "" else string value

    let private observationOf (tool: string) (args: obj) : Result<Observation, string> =
        match tool with
        | "assess" -> ObservationCodec.decodeSemanticAssessment args
        | "propose" -> ObservationCodec.decodeCandidates args
        | "investigate" -> ObservationCodec.decodeInvestigation args
        | "synthesize" -> ObservationCodec.decodeSynthesis args
        | _ -> Error("unknown legacy tool: " + tool)

    let private successView (inquiryId: string) (liveHandle: string) (success: SessionSuccess) : obj =
        let payload: obj = McpContract.successPayload success

        createObj
            [ "inquiryId" ==> inquiryId
              "handle" ==> liveHandle
              "status" ==> fieldOf payload "status"
              "revision" ==> fieldOf payload "revision"
              "nextTool" ==> fieldOf payload "nextTool"
              "request" ==> fieldOf payload "request"
              "answer" ==> fieldOf payload "answer" ]

    let private currentView (inquiryId: string) (liveHandle: string) (state: EpistemicState) (error: obj) : obj =
        let nextTool: obj =
            match state.PendingRequest with
            | Some(request: Request) -> box (McpContract.nextTool request)
            | None -> null

        let request: obj =
            match state.PendingRequest with
            | Some(request: Request) -> Codec.requestObject request
            | None -> null

        createObj
            [ "inquiryId" ==> inquiryId
              "handle" ==> liveHandle
              "status" ==> "yield"
              "revision" ==> state.Revision
              "nextTool" ==> nextTool
              "request" ==> request
              "error" ==> error ]

    let private failureView (inquiryId: string) (liveHandle: string option) (failure: SessionFailureView) : obj =
        let view: McpContract.ErrorView = McpContract.failureView failure

        let revision: obj =
            match failure.State with
            | Some(prior: EpistemicState) -> box prior.Revision
            | None -> null

        createObj
            [ "inquiryId" ==> inquiryId
              "handle" ==> (liveHandle |> Option.map box |> Option.toObj)
              "revision" ==> revision
              "error" ==> McpContract.errorObject view ]

    let private startFreshReplay (question: string) : obj =
        let fresh = randomUUID ()
        let store = SessionStore()

        match store.StartTyped(question) with
        | StartOutcome.Rejected(message: string) ->
            let view = McpContract.questionRequiredView message

            createObj [ "inquiryId" ==> fresh; "error" ==> McpContract.errorObject view ]
        | StartOutcome.Started(liveHandle: string, state: EpistemicState, result: InquiryResult) ->
            let success: SessionSuccess =
                { Handle = liveHandle
                  State = state
                  Result = result }

            stores.[fresh] <- store
            liveHandles.[fresh] <- liveHandle
            successView fresh liveHandle success

    let private missingHandleReplay () : obj =
        let failure: SessionFailureView =
            { Handle = None
              State = None
              Failure = SessionFailure.MissingHandle }

        let view = McpContract.failureView failure

        createObj [ "error" ==> McpContract.errorObject view ]

    let private unknownHandleReplay (inquiryId: string) : obj =
        let failure: SessionFailureView =
            { Handle = Some inquiryId
              State = None
              Failure = SessionFailure.UnknownHandle }

        let view = McpContract.failureView failure

        createObj [ "inquiryId" ==> inquiryId; "error" ==> McpContract.errorObject view ]

    let private resumeInvalid (inquiryId: string) (store: SessionStore) (liveHandle: string) (message: string) : obj =
        match store.TryState liveHandle with
        | Some(state: EpistemicState) ->
            let view = McpContract.invalidObservationView (Some liveHandle) message

            currentView inquiryId liveHandle state (McpContract.errorObject view)
        | None ->
            let failure: SessionFailureView =
                { Handle = Some liveHandle
                  State = None
                  Failure = SessionFailure.UnknownHandle }

            failureView inquiryId (Some liveHandle) failure

    let private resumeDecoded
        (inquiryId: string)
        (store: SessionStore)
        (liveHandle: string)
        (observation: Observation)
        : obj =
        match store.ResumeObservation(liveHandle, observation) with
        | SessionOutcome.Success(success: SessionSuccess) -> successView inquiryId liveHandle success
        | SessionOutcome.Failure(failure: SessionFailureView) -> failureView inquiryId (Some liveHandle) failure

    let private resumeStored (inquiryId: string) (tool: string) (args: obj) : obj =
        let store: SessionStore = stores.[inquiryId]
        let liveHandle: string = liveHandles.[inquiryId]

        match observationOf tool args with
        | Error(message: string) -> resumeInvalid inquiryId store liveHandle message
        | Ok(observation: Observation) -> resumeDecoded inquiryId store liveHandle observation

    let replayLegacyCall (input: obj) : obj =
        let tool = textOf (fieldOf input "tool")
        let args: obj = fieldOf input "args"
        let inquiryId = textOf (fieldOf input "inquiryId")

        if tool = "start" then
            startFreshReplay (textOf (fieldOf args "question"))
        elif inquiryId = "" then
            missingHandleReplay ()
        elif not (stores.ContainsKey inquiryId) then
            unknownHandleReplay inquiryId
        else
            resumeStored inquiryId tool args

    let methods: (string * obj) list = [ "replayLegacyCall", box replayLegacyCall ]
