// WHAT[EPI-030]: durable codec for legacy Sphinx observations.
// Accepted legacy observations become canonical envelopes on one stream per
// handle; the runner appends them after a successful Resume and replays the
// decoded raws through a fresh store to recover the same handle at restart.
namespace Wanxiangshu.Sphinx

open Fable.Core
open Fable.Core.JsInterop

module LegacyDurability =

    let observationType = SphinxEventTypes.LegacyObservation

    let streamFor (handle: string) : string = "sphinx-legacy/" + handle

    let envelopeId (handle: string) (revision: int) : string = handle + ":" + string revision

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private isFiniteNumber (value: obj) : bool = emitJsExpr value "Number.isFinite($0)"

    let private fieldOf (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private textOf (value: obj) : string =
        if isNullish value then "" else string value

    let private revisionOf (value: obj) : int =
        let raw: obj = fieldOf value "revision"

        if isNullish raw || not (isFiniteNumber raw) then
            -1
        else
            int (unbox<float> raw)

    /// Decoded and envelope-wrapped raws share handle/tool/args/revision, so
    /// accept either shape and read the shared fields underneath.
    let private rawPayload (raw: obj) : obj =
        let inner: obj = fieldOf raw "payload"

        if isNullish inner then raw else inner

    let encodeObservation (handle: string) (tool: string) (args: obj) (revision: int) : obj =
        let parents: string array =
            if revision <= 0 then
                [||]
            else
                [| envelopeId handle (revision - 1) |]

        createObj
            [ "id" ==> envelopeId handle revision
              "stream" ==> streamFor handle
              "type" ==> observationType
              "parents" ==> parents
              "payload"
              ==> createObj
                      [ "handle" ==> handle
                        "tool" ==> tool
                        "args" ==> args
                        "revision" ==> revision ]
              "payloadRefs" ==> ([||]: string array) ]

    let private decodePayload (envelope: obj) (payload: obj) : obj =
        if isNullish payload then
            null
        else
            createObj
                [ "handle" ==> textOf (fieldOf payload "handle")
                  "tool" ==> textOf (fieldOf payload "tool")
                  "args" ==> fieldOf payload "args"
                  "revision" ==> revisionOf payload
                  "id" ==> textOf (fieldOf envelope "id")
                  "stream" ==> textOf (fieldOf envelope "stream") ]

    let decodeObservation (envelope: obj) : obj =
        if textOf (fieldOf envelope "type") <> observationType then
            null
        else
            decodePayload envelope (fieldOf envelope "payload")

    [<Emit("$0.sessions.get($1)")>]
    let private entryOf (store: SessionStore) (handle: string) : obj = jsNative

    [<Emit("$0.sessions.set($1, $2)")>]
    let private insertEntry (store: SessionStore) (handle: string) (entry: obj) : unit = jsNative

    [<Emit("$0.sessions.delete($1)")>]
    let private removeEntry (store: SessionStore) (handle: string) : unit = jsNative

    let private observationOf (tool: string) (args: obj) : Result<Observation, string> =
        match tool with
        | "assess" -> ObservationCodec.decodeSemanticAssessment args
        | "propose" -> ObservationCodec.decodeCandidates args
        | "investigate" -> ObservationCodec.decodeInvestigation args
        | "synthesize" -> ObservationCodec.decodeSynthesis args
        | _ -> Error("unknown legacy tool: " + tool)

    let private missingObservationsView () : obj =
        box
            {| ok = false
               error =
                box
                    {| code = "missing-observations"
                       message = "replay needs at least the start observation" |} |}

    let private missingStartView () : obj =
        box
            {| ok = false
               error =
                box
                    {| code = "missing-start"
                       message = "replay must open with the start observation" |} |}

    let private startRejectedView (message: string) : obj =
        let view = McpContract.questionRequiredView message

        box
            {| ok = false
               error = McpContract.errorObject view |}

    let private completedReplayView (target: string) (state: EpistemicState) : obj =
        box
            {| ok = true
               handle = target
               revision = state.Revision |}

    let private invalidReplayView (target: string) (current: EpistemicState) (message: string) : obj =
        let view = McpContract.invalidObservationView (Some target) message

        box
            {| ok = false
               error = McpContract.errorObject view
               handle = target
               revision = current.Revision |}

    let private failedRevisionOf (failure: SessionFailureView) (current: EpistemicState) : obj =
        match failure.State with
        | Some(prior: EpistemicState) -> box prior.Revision
        | None -> box current.Revision

    let private failedReplayView (target: string) (current: EpistemicState) (failure: SessionFailureView) : obj =
        let view = McpContract.failureView failure

        box
            {| ok = false
               error = McpContract.errorObject view
               handle = target
               revision = failedRevisionOf failure current |}

    let private resumeDecoded
        (store: SessionStore)
        (target: string)
        (observation: Observation)
        (current: EpistemicState)
        : Result<EpistemicState, obj> =
        match store.ResumeObservation(target, observation) with
        | SessionOutcome.Success(success: SessionSuccess) -> Ok success.State
        | SessionOutcome.Failure(failure: SessionFailureView) -> Error(failedReplayView target current failure)

    let private advanceReplay
        (store: SessionStore)
        (target: string)
        (ordered: obj array)
        (index: int)
        (current: EpistemicState)
        : Result<EpistemicState, obj> =
        let raw: obj = rawPayload ordered.[index]
        let tool = textOf (fieldOf raw "tool")

        match observationOf tool (fieldOf raw "args") with
        | Error(message: string) -> Error(invalidReplayView target current message)
        | Ok(observation: Observation) -> resumeDecoded store target observation current

    let private replayRest
        (store: SessionStore)
        (target: string)
        (ordered: obj array)
        (first: EpistemicState)
        : Result<EpistemicState, obj> =
        let folder (advanced: Result<EpistemicState, obj>) (index: int) : Result<EpistemicState, obj> =
            advanced
            |> Result.bind (fun (current: EpistemicState) -> advanceReplay store target ordered index current)

        [| 1 .. ordered.Length - 1 |] |> Array.fold folder (Ok first)

    let private resumeAll (store: SessionStore) (target: string) (ordered: obj array) (state: EpistemicState) : obj =
        match replayRest store target ordered state with
        | Ok(final: EpistemicState) -> completedReplayView target final
        | Error(view: obj) -> view

    let private relocateEntry (store: SessionStore) (target: string) (liveHandle: string) : unit =
        if target <> liveHandle then
            removeEntry store liveHandle

    let private openReplay (store: SessionStore) (target: string) (ordered: obj array) (question: string) : obj =
        match store.StartTyped(question) with
        | StartOutcome.Rejected(message: string) -> startRejectedView message
        | StartOutcome.Started(liveHandle: string, state: EpistemicState, _result: InquiryResult) ->
            insertEntry store target (entryOf store liveHandle)
            relocateEntry store target liveHandle
            resumeAll store target ordered state

    let private replayOrdered (store: SessionStore) (ordered: obj array) : obj =
        let first: obj = rawPayload ordered.[0]
        let target = textOf (fieldOf first "handle")

        if textOf (fieldOf first "tool") <> "start" then
            missingStartView ()
        else
            openReplay store target ordered (textOf (fieldOf (fieldOf first "args") "question"))

    /// Replay decoded raws (oldest first) through a fresh store, preserving the
    /// original handle: start mints the entry under a live id, the entry then
    /// moves to the durable handle, and every later raw resumes in order.
    let replayObservations (store: SessionStore) (raws: obj array) : obj =
        if isNullish (box raws) || Array.isEmpty raws then
            missingObservationsView ()
        else
            replayOrdered store (raws |> Array.sortBy (fun (raw: obj) -> revisionOf (rawPayload raw)))
