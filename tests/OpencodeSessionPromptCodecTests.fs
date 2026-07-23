module Wanxiangshu.Tests.OpencodeSessionPromptCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

module Metadata = Wanxiangshu.Runtime.OpencodeSessionPromptCodec.WanxiangshuMetadataCodec

let private providerId (m: obj) = unbox<string> m?providerID
let private modelId (m: obj) = unbox<string> m?modelID

let modelObjectPassthroughFromPayload () =
    let hostModel =
        createObj [ "providerID", box "anthropic"; "modelID", box "claude-3" ]

    let payload = createObj [ "model", hostModel ]

    match tryDecodePromptModelFromPayload payload with
    | None -> check "model object passthrough" false
    | Some m -> check "model object same reference" (obj.ReferenceEquals(m, hostModel))

let modelStringSlashDecodes () =
    match tryDecodePromptModelFromModelString "openai/gpt-4o" with
    | None -> check "modelString slash decodes" false
    | Some m ->
        check "providerID from slash" (providerId m = "openai")
        check "modelID from slash" (modelId m = "gpt-4o")

let modelStringViaPayload () =
    let payload = createObj [ "modelString", box "google/gemini-pro" ]

    match tryDecodePromptModelFromPayload payload with
    | None -> check "modelString via payload" false
    | Some m ->
        check "payload providerID" (providerId m = "google")
        check "payload modelID" (modelId m = "gemini-pro")

let emptyModelStringNone () =
    check "empty modelString" (tryDecodePromptModelFromModelString "" = None)
    check "empty modelString payload" (tryDecodePromptModelFromPayload (createObj [ "modelString", box "" ]) = None)

let invalidSlashNone () =
    check "no slash" (tryDecodePromptModelFromModelString "gpt-4o" = None)
    check "slash at start" (tryDecodePromptModelFromModelString "/only-model" = None)
    check "slash at end" (tryDecodePromptModelFromModelString "provider/" = None)
    check "only slash" (tryDecodePromptModelFromModelString "/" = None)

let modelKeyWinsOverModelString () =
    let hostModel = createObj [ "providerID", box "x"; "modelID", box "y" ]
    let payload = createObj [ "model", hostModel; "modelString", box "ignored/z" ]

    match tryDecodePromptModelFromPayload payload with
    | None -> check "model key wins" false
    | Some m -> check "model key not parsed from modelString" (obj.ReferenceEquals(m, hostModel))

let nestedSlashPreservesMiddlePath () =
    match tryDecodePromptModelFromModelString "a/b/c" with
    | None -> check "nested slash preserves middle path" false
    | Some m ->
        check "nested providerID" (providerId m = "a")
        check "nested modelID keeps middle path" (modelId m = "b/c")

let variantSuffixStrippedSingle () =
    match tryDecodePromptModelFromModelString "provider/model:variant" with
    | None -> check "variant suffix stripped single" false
    | Some m ->
        check "variant providerID" (providerId m = "provider")
        check "variant modelID stripped" (modelId m = "model")

let variantSuffixStrippedNested () =
    match tryDecodePromptModelFromModelString "provider/nested/model:variant" with
    | None -> check "variant suffix stripped nested" false
    | Some m ->
        check "nested variant providerID" (providerId m = "provider")
        check "nested variant modelID stripped" (modelId m = "nested/model")

let variantOnlyModelIdEmpty () =
    check "variant only modelId empty" (tryDecodePromptModelFromModelString "provider/:variant" = None)

let variantWithSpecialChars () =
    match tryDecodePromptModelFromModelString "anthropic/claude-3.5-sonnet:fast-2024" with
    | None -> check "variant with special chars" false
    | Some m ->
        check "special providerID" (providerId m = "anthropic")
        check "special modelID stripped" (modelId m = "claude-3.5-sonnet")

let nudgeMetadataCarriesVersionedSchema () =
    let encoded = Metadata.encodePartMetadata "abc" Metadata.nudgeKind None 0 0 "" 0 0

    let part =
        createObj [ "type", box "text"; "text", box "hello"; "metadata", encoded ]

    match Metadata.tryDecodeFromPart part with
    | None -> check "nudge metadata carries versioned schema" false
    | Some m ->
        check "nudge metadata schema is 2" (m.Schema = 2)
        check "nudge metadata kind is nudge" (m.Kind = "nudge")
        check "nudge metadata nonce" (m.Nonce = "abc")

let legacyFlatNonceStillDecodes () =
    let part =
        createObj
            [ "type", box "text"
              "text", box "hello"
              "metadata", box (createObj [ "nonce", box "legacy" ]) ]

    match Metadata.tryDecodeFromPart part with
    | None -> check "legacy nonce decodes" false
    | Some m ->
        check "legacy schema is 1" (m.Schema = 1)
        check "legacy kind is nudge" (m.Kind = "nudge")
        check "legacy nonce value" (m.Nonce = "legacy")

let continuationMetadataRoundTrips () =
    let metadata =
        Metadata.encodePartMetadata "cont-1" Metadata.fallbackContinuationKind (Some "cont-1") 7 2 "h-1" 5 3

    let part = createObj [ "type", box "text"; "text", box "x"; "metadata", metadata ]

    match Metadata.tryDecodeFromPart part with
    | None -> check "continuation metadata round-trips" false
    | Some m ->
        check "continuation schema" (m.Schema = 2)
        check "continuation kind" (m.Kind = "fallback_continuation")
        check "continuation nonce" (m.Nonce = "cont-1")
        check "continuation id" (m.ContinuationId = "cont-1")
        check "continuation ordinal" (m.ContinuationOrdinal = 7)
        check "continuation attempt" (m.Attempt = 2)
        check "continuation humanTurnId" (m.HumanTurnId = "h-1")
        check "continuation contextGeneration" (m.ContextGeneration = 5)
        check "continuation cancelGeneration" (m.CancelGeneration = 3)

let run () =
    modelObjectPassthroughFromPayload ()
    modelStringSlashDecodes ()
    modelStringViaPayload ()
    emptyModelStringNone ()
    invalidSlashNone ()
    modelKeyWinsOverModelString ()
    nestedSlashPreservesMiddlePath ()
    variantSuffixStrippedSingle ()
    variantSuffixStrippedNested ()
    variantOnlyModelIdEmpty ()
    variantWithSpecialChars ()
    nudgeMetadataCarriesVersionedSchema ()
    legacyFlatNonceStillDecodes ()
    continuationMetadataRoundTrips ()
