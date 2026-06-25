module VibeFs.Tests.OpencodeSessionPromptCodecTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.OpencodeSessionPromptCodec

let private providerId (m: obj) = unbox<string> m?providerID
let private modelId (m: obj) = unbox<string> m?modelID

let modelObjectPassthroughFromPayload () =
    let hostModel = createObj [ "providerID", box "anthropic"; "modelID", box "claude-3" ]
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

let run () =
    modelObjectPassthroughFromPayload ()
    modelStringSlashDecodes ()
    modelStringViaPayload ()
    emptyModelStringNone ()
    invalidSlashNone ()
    modelKeyWinsOverModelString ()