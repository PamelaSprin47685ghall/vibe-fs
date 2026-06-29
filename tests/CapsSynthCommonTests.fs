module Wanxiangshu.Tests.CapsSynthCommonTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.CapsSynthCommon
open Wanxiangshu.Shell.Dyn

let private msg (id: string) : obj = createObj [ "id", box id ]

let private messageId (m: obj) : string = str m "id"

let stripLeadingCapsSynthRemovesPrefix () =
    let synth = msg $"{capsUserPrefix}fp"
    let ack = msg $"{capsAcknowledgePrefix}fp"
    let read = msg $"{capsAssistantPrefix}fp"
    let real = msg "msg-real"
    let stripped = stripLeadingCapsSynth messageId [| synth; ack; read; real |]
    equal "strip drops leading synth" 1 stripped.Length
    equal "strip keeps real id" "msg-real" (messageId stripped.[0])

let findFirstNonSynthMessageSkipsSynth () =
    let synth = msg $"{capsAssistantPrefix}x"
    let ack = msg $"{capsAcknowledgePrefix}x"
    let real = msg "anchor"
    match findFirstNonSynthMessage messageId [| synth; ack; real |] with
    | Some m -> equal "find anchor id" "anchor" (messageId m)
    | None -> check "expected anchor" false

let userCapsTextPreludeAndDefault () =
    let withPrelude = userCapsText (Some "kg prelude")
    check "prelude prefix" (withPrelude.StartsWith "kg prelude")
    check "prelude wraps think" (withPrelude.Contains thinkWrapped)
    equal "no prelude" thinkWrapped (userCapsText None)

let acknowledgeTextIsStable () =
    equal "ack text" "好的，我将遵守规则。" acknowledgeText

let classifySourceCoversAck () =
    match classifySource $"{capsAcknowledgePrefix}xyz" with
    | Synthetic kind -> equal "ack kind" capsAcknowledgePrefix kind
    | _ -> check "expected Synthetic" false

let run () =
    stripLeadingCapsSynthRemovesPrefix ()
    findFirstNonSynthMessageSkipsSynth ()
    userCapsTextPreludeAndDefault ()
    acknowledgeTextIsStable ()
    classifySourceCoversAck ()