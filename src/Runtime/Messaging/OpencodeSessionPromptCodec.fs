module Wanxiangshu.Runtime.OpencodeSessionPromptCodec

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.Messaging

let tryDecodePromptModelFromModelString (modelString: string) : obj option =
    if modelString = "" then
        None
    else
        let colon = modelString.IndexOf(':')

        let basePart =
            if colon = -1 then
                modelString
            else
                modelString.[0 .. colon - 1].Trim()

        let variant =
            if colon = -1 then
                None
            else
                let v = modelString.[colon + 1 ..].Trim()
                if v = "" then None else Some v

        if basePart = "" then
            None
        else
            let slash = basePart.IndexOf('/')

            if slash <= 0 || slash >= basePart.Length - 1 then
                None
            else
                let p = basePart.[0 .. slash - 1]
                let m = basePart.[slash + 1 ..]

                Some(
                    box
                        {| providerID = p
                           modelID = m
                           variant =
                            match variant with
                            | Some v -> box v
                            | None -> null |}
                )

let tryDecodePromptModelFromPayload (payload: obj) : obj option =
    let promptModel = Dyn.get payload "model"

    if not (Dyn.isNullish promptModel) then
        Some promptModel
    else
        tryDecodePromptModelFromModelString (Dyn.str payload "modelString")

/// Versioned plugin metadata carried on an OpenCode text part.
/// Probe only for chat.message classification — domain state must bind
/// HostUserMessageId from the host-issued message id, never treat this
/// metadata as the sole durable provenance fact (SPEC §七 step 1).
module WanxiangshuMetadataCodec =

    [<NoComparison; NoEquality>]
    type T =
        { Schema: int
          Kind: string
          Origin: MessageOrigin option
          Nonce: string
          ContinuationId: string
          ContinuationOrdinal: int
          Attempt: int
          HumanTurnId: string
          ContextGeneration: int
          CancelGeneration: int }

    let currentSchema = 2
    let nudgeKind = "nudge"
    let fallbackContinuationKind = "fallback_continuation"

    let private empty =
        { Schema = 0
          Kind = ""
          Origin = None
          Nonce = ""
          ContinuationId = ""
          ContinuationOrdinal = 0
          Attempt = 0
          HumanTurnId = ""
          ContextGeneration = 0
          CancelGeneration = 0 }

    let private tryInt (o: obj) (key: string) : int option =
        match Dyn.opt o key with
        | Some v when not (Dyn.isNullish v) ->
            match System.Int32.TryParse(string v) with
            | true, i -> Some i
            | _ -> None
        | _ -> None

    /// Decode a single metadata record from a message part. Accepts the
    /// versioned `metadata.wanxiangshu` object as well as the legacy flat
    /// `metadata.nonce` used by older prompts.
    let tryDecodeFromPart (part: obj) : T option =
        if Dyn.isNullish part then
            None
        else
            let metadata = Dyn.get part "metadata"

            if Dyn.isNullish metadata then
                None
            else
                let ws = Dyn.get metadata "wanxiangshu"

                if not (Dyn.isNullish ws) then
                    let kindStr = Dyn.str ws "kind"
                    let originStr = Dyn.str ws "origin"

                    let originOpt =
                        if originStr <> "" then
                            MessageOrigin.tryParse originStr
                        else
                            MessageOrigin.tryParse kindStr

                    Some
                        { Schema = tryInt ws "schema" |> Option.defaultValue 0
                          Kind = kindStr
                          Origin = originOpt
                          Nonce = Dyn.str ws "nonce"
                          ContinuationId = Dyn.str ws "continuationId"
                          ContinuationOrdinal = tryInt ws "continuationOrdinal" |> Option.defaultValue 0
                          Attempt = tryInt ws "attempt" |> Option.defaultValue 0
                          HumanTurnId = Dyn.str ws "humanTurnId"
                          ContextGeneration = tryInt ws "contextGeneration" |> Option.defaultValue 0
                          CancelGeneration = tryInt ws "cancelGeneration" |> Option.defaultValue 0 }
                else
                    let legacy = Dyn.str metadata "nonce"

                    if legacy <> "" then
                        Some
                            { empty with
                                Schema = 1
                                Kind = nudgeKind
                                Origin = Some MessageOrigin.TodoNudge
                                Nonce = legacy }
                    else
                        None

    /// Scan a message `parts` array and return the first recognised
    /// `Wanxiangshu` metadata record.
    let tryDecodeFromParts (parts: obj) : T option =
        if Dyn.isNullish parts || not (Dyn.isArray parts) then
            None
        else
            (parts :?> obj array) |> Array.tryPick tryDecodeFromPart

    /// Build a `part.metadata` object carrying versioned `wanxiangshu`
    /// provenance. Optional continuation fields are omitted when empty/zero
    /// so the wire shape stays minimal for nudge prompts.
    let encodePartMetadataWithOrigin
        (nonce: string)
        (kind: string)
        (origin: MessageOrigin option)
        (continuationId: string option)
        (continuationOrdinal: int)
        (attempt: int)
        (humanTurnId: string)
        (contextGeneration: int)
        (cancelGeneration: int)
        : obj =
        let ws = createObj []
        Dyn.setKey ws "schema" (box currentSchema)
        Dyn.setKey ws "kind" (box kind)

        origin
        |> Option.iter (fun o -> Dyn.setKey ws "origin" (box (MessageOrigin.toWireString o)))

        Dyn.setKey ws "nonce" (box nonce)

        let setIfNonEmpty key value =
            if value <> "" then
                Dyn.setKey ws key (box value)

        let setIfPositive key value =
            if value > 0 then
                Dyn.setKey ws key (box value)

        continuationId |> Option.iter (setIfNonEmpty "continuationId")
        setIfPositive "continuationOrdinal" continuationOrdinal
        setIfPositive "attempt" attempt
        setIfNonEmpty "humanTurnId" humanTurnId
        setIfPositive "contextGeneration" contextGeneration
        setIfPositive "cancelGeneration" cancelGeneration

        box {| wanxiangshu = ws |}

    let encodePartMetadata
        (nonce: string)
        (kind: string)
        (continuationId: string option)
        (continuationOrdinal: int)
        (attempt: int)
        (humanTurnId: string)
        (contextGeneration: int)
        (cancelGeneration: int)
        : obj =
        let defaultOrigin = MessageOrigin.tryParse kind

        encodePartMetadataWithOrigin
            nonce
            kind
            defaultOrigin
            continuationId
            continuationOrdinal
            attempt
            humanTurnId
            contextGeneration
            cancelGeneration
