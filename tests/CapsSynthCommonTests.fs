module Wanxiangshu.Tests.CapsSynthCommonTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.CapsPrelude
open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Shell.CapsSynthCommon
open Wanxiangshu.Shell.Dyn

let private msg (id: string) : obj = createObj [ "id", box id ]

let private messageId (m: obj) : string = str m "id"

let stripLeadingCapsSynthRemovesPrefix () =
    let synth = msg $"{capsUserPrefix}fp"
    let real = msg "msg-real"
    let stripped = stripLeadingCapsSynth messageId [| synth; real |]
    equal "strip drops leading synth" 1 stripped.Length
    equal "strip keeps real id" "msg-real" (messageId stripped.[0])

let findFirstNonSynthMessageSkipsSynth () =
    let synth = msg $"{capsAssistantPrefix}x"
    let real = msg "anchor"
    match findFirstNonSynthMessage messageId [| synth; real |] with
    | Some m -> equal "find anchor id" "anchor" (messageId m)
    | None -> check "expected anchor" false

let userCapsTextPreludeAndDefault () =
    let withPrelude = userCapsText (Some "kg prelude")
    check "prelude prefix" (withPrelude.StartsWith "kg prelude")
    check "prelude wraps think" (withPrelude.Contains thinkWrapped)
    equal "no prelude" thinkWrapped (userCapsText None)

let run () =
    stripLeadingCapsSynthRemovesPrefix ()
    findFirstNonSynthMessageSkipsSynth ()
    userCapsTextPreludeAndDefault ()