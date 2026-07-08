module Wanxiangshu.Tests.SubagentIoTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert

module DynModule = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SubagentIo

let noOutputMessageIsNoOutputText () =
    equal "no output" noOutputText (noOutputMessage ())

let abortedPrefixMessageIsAbortedPrefix () =
    equal "aborted" abortedPrefix (abortedPrefixMessage ())

let textPartReturnsCorrectShape () =
    let part = textPart "hello"
    let t = DynModule.str part "type"
    let txt = DynModule.str part "text"
    equal "type" "text" t
    equal "text" "hello" txt

let textPartsReturnsArrayOfTextParts () =
    let parts = textParts [ "a"; "b" ]
    equal "count" 2 parts.Length

    for p in parts do
        let t = DynModule.str p "type"
        equal "type" "text" t

let emptySettingsHasNone () =
    equal "model" None emptySettings.ModelString
    equal "thinking" None emptySettings.ThinkingLevel
    equal "variant" None emptySettings.Variant

let extractToolContextUsesDirectory () =
    let ctx = box (createObj [ "directory", box "/tmp" ])
    let tc = extractToolContext ctx "/plugin"
    equal "dir" "/tmp" tc.Directory

let extractToolContextFallsBackToPluginDirectory () =
    let ctx = box (createObj [])
    let tc = extractToolContext ctx "/plugin"
    equal "dir" "/plugin" tc.Directory

let extractToolContextFindsSessionID () =
    let ctx = box (createObj [ "sessionID", box "s123" ])
    let tc = extractToolContext ctx "/tmp"
    equal "sessionID" "s123" tc.SessionID

let extractToolContextSessionIDFallsBack () =
    let ctx = box (createObj [])
    let tc = extractToolContext ctx "/tmp"
    equal "empty" "" tc.SessionID

let firstStringFindsFirst () =
    let ctx = box (createObj [ "a", box "1"; "b", box "2" ])
    equal "a" (Some "1") (firstString ctx [ "a"; "b" ])

let firstStringFindsFallback () =
    let ctx = box (createObj [ "x", box "3" ])
    equal "x" (Some "3") (firstString ctx [ "a"; "x" ])

let firstStringNoneWhenNotFound () =
    let ctx = box (createObj [])
    equal "none" None (firstString ctx [ "a" ])

let signalAbortedNullIsFalse () =
    check "false" (not (signalAborted null))

let signalAbortedNullishIsFalse () =
    check "false" (not (signalAborted (box (createObj []))))

let buildPromptBodyBasic () =
    let body = buildPromptBody "coder" "do it" null emptySettings
    let agent = DynModule.str body "agent"
    equal "agent" "coder" agent

let buildPromptBodyWithThinkingLevel () =
    let settings =
        { emptySettings with
            ThinkingLevel = Some "high" }

    let body = buildPromptBody "coder" "do it" null settings
    let variant = DynModule.str body "variant"
    equal "variant" "high" variant

let run () =
    noOutputMessageIsNoOutputText ()
    abortedPrefixMessageIsAbortedPrefix ()
    textPartReturnsCorrectShape ()
    textPartsReturnsArrayOfTextParts ()
    emptySettingsHasNone ()
    extractToolContextUsesDirectory ()
    extractToolContextFallsBackToPluginDirectory ()
    extractToolContextFindsSessionID ()
    extractToolContextSessionIDFallsBack ()
    firstStringFindsFirst ()
    firstStringFindsFallback ()
    firstStringNoneWhenNotFound ()
    signalAbortedNullIsFalse ()
    signalAbortedNullishIsFalse ()
    buildPromptBodyBasic ()
    buildPromptBodyWithThinkingLevel ()
