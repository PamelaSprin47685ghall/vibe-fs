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

let capsPreludeRequiresFormalTestsNotAdHoc () =
    check "think: no ad-hoc tests" (thinkText.Contains "禁止临时测试")
    check "think: debug becomes tests" (thinkText.Contains "调试过程永久化")
    check "think: generic pipeline" (thinkText.Contains "标准测试入口")
    check "think: not one-repo npm" (not (thinkText.Contains "npm run build-and-test"))
    check "llm: no ad-hoc tests" (llmText.Contains "禁止临时测试")
    check "llm: debug becomes tests" (llmText.Contains "调试过程永久化")
    check "llm: generic pipeline" (llmText.Contains "常规测试管线")

let shellCapsPreludeReExportsKernel () =
    equal "shell thinkWrapped" thinkWrapped Wanxiangshu.Shell.CapsPrelude.thinkWrapped
    equal "shell llmText" llmText Wanxiangshu.Shell.CapsPrelude.llmText

let classifySourceCoversAck () =
    match classifySource $"{capsAcknowledgePrefix}xyz" with
    | Synthetic kind -> equal "ack kind" capsAcknowledgePrefix kind
    | _ -> check "expected Synthetic" false

let run () =
    stripLeadingCapsSynthRemovesPrefix ()
    findFirstNonSynthMessageSkipsSynth ()
    userCapsTextPreludeAndDefault ()
    acknowledgeTextIsStable ()
    capsPreludeRequiresFormalTestsNotAdHoc ()
    shellCapsPreludeReExportsKernel ()
    classifySourceCoversAck ()