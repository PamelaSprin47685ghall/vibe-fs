module Wanxiangshu.Tests.OpencodeSessionPromptCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.OpencodeSessionPromptCodec

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
