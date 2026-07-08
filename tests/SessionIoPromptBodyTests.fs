module Wanxiangshu.Tests.SessionIoPromptBodyTests

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.OpencodeSessionPromptCodec

let private isNullish (o: obj) : bool = isNull o || jsTypeof o = "undefined"

let private withKey (o: obj) (key: string) (v: obj) : obj =
    let copy = createObj []

    for k in Constructors.Object.keys (o) |> Seq.toArray do
        copy?(k) <- o?(k)

    copy?(key) <- v
    copy

/// Mirrors `SessionIo.buildPromptBody` model branch: `createObj` payload + payload codec SSOT.
let private applyPromptModelFromModelString (body: obj) (modelString: string) : obj =
    let payload = createObj [ "modelString", box modelString ]

    match tryDecodePromptModelFromPayload payload with
    | Some model -> withKey body "model" model
    | None -> body

let private providerId (m: obj) = unbox<string> m?providerID
let private modelId (m: obj) = unbox<string> m?modelID

let modelStringAddsModelToBody () =
    let body = createObj [ "agent", box "coder" ]
    let out = applyPromptModelFromModelString body "openai/gpt-4o"
    let model = out?model
    check "body has model key" (not (isNullish model))
    check "providerID on body.model" (providerId model = "openai")
    check "modelID on body.model" (modelId model = "gpt-4o")

let invalidModelStringLeavesBodyUnchanged () =
    let body = createObj [ "agent", box "coder" ]
    let out = applyPromptModelFromModelString body "no-slash-model"
    check "invalid modelString no model key" (isNullish (out?model))
    check "agent preserved" (unbox<string> out?agent = "coder")

let emptyModelStringLeavesBodyUnchanged () =
    let body = createObj [ "agent", box "coder" ]
    let out = applyPromptModelFromModelString body ""
    check "empty modelString no model key" (isNullish (out?model))

let run () =
    modelStringAddsModelToBody ()
    invalidModelStringLeavesBodyUnchanged ()
    emptyModelStringLeavesBodyUnchanged ()
