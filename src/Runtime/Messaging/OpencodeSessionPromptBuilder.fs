module Wanxiangshu.Runtime.OpencodeSessionPromptBuilder

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec
open Wanxiangshu.Kernel.Fallback.Continuation

/// Build the host wire prompt body for `session.prompt`. The agent-scoped
/// variant carries an `agent` field so the host routes to the same assistant
/// that produced the last turn; without an agent the host falls back to the
/// session default.
let createPromptBody (agent: string option) (text: string) : obj =
    match agent with
    | Some a ->
        box
            {| agent = a
               parts = [| box {| ``type`` = "text"; text = text |} |] |}
    | None -> box {| parts = [| box {| ``type`` = "text"; text = text |} |] |}

/// Build prompt body with optional agent and model override.
let createPromptBodyWithModelAndNonce
    (agent: string option)
    (model: string option)
    (text: string)
    (nonce: string option)
    : obj =
    let textPart =
        match nonce with
        | Some n ->
            box
                {| ``type`` = "text"
                   text = text
                   metadata = box {| nonce = n |} |}
        | None -> box {| ``type`` = "text"; text = text |}

    let parts: obj array = [| textPart |]

    let baseBody =
        match agent with
        | Some a -> box {| agent = a; parts = parts |}
        | None -> box {| parts = parts |}

    match model |> Option.bind tryDecodePromptModelFromModelString with
    | Some promptModel -> Dyn.withKey baseBody "model" promptModel
    | None -> baseBody

let createPromptBodyWithModel (agent: string option) (model: string option) (text: string) : obj =
    createPromptBodyWithModelAndNonce agent model text None

/// Build a continuation prompt body with namespaced metadata.
/// The payload is U+200B text plus `metadata.wanxiangshu` carrying the
/// continuation identity. The model and agent come from the frozen request.
let createFallbackContinuationPromptBody
    (agent: string option)
    (continuationPayload: string)
    (request: ContinuationRequest)
    : obj =
    let variantVal: obj =
        match request.Model.Variant with
        | Some v -> box v
        | None -> null

    let modelObj =
        {| providerID = request.Model.ProviderID
           modelID = request.Model.ModelID
           variant = variantVal |}

    let metadata =
        {| wanxiangshu =
            {| kind = "fallback_continuation"
               schema = 2
               continuationId = request.ContinuationId
               continuationOrdinal = request.ContinuationOrdinal
               attempt = request.Attempt
               humanTurnId = request.HumanTurnId
               contextGeneration = request.ContextGeneration
               cancelGeneration = request.CancelGeneration |} |}

    let textPart =
        box
            {| ``type`` = "text"
               text = continuationPayload
               metadata = metadata |}

    let parts: obj array = [| textPart |]

    match agent with
    | Some a when a <> "" ->
        box
            {| agent = a
               parts = parts
               model = modelObj |}
    | _ -> box {| parts = parts; model = modelObj |}
