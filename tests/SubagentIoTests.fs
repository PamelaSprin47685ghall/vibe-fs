module VibeFs.Tests.SubagentIoTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Shell.Dyn
open VibeFs.Shell.SubagentIo
module Dyn = VibeFs.Shell.Dyn

let private ctx (values: (string * obj) list) : obj =
    let o = createObj []
    for k, v in values do
        o?(k) <- v
    o

let firstStringPreferListed () =
    let c = ctx [ "sessionID", box "abc" ]
    equal "sessionID wins" (Some "abc") (firstString c [ "sessionID"; "sessionId" ])
    let c = ctx [ "sessionId", box "xyz" ]
    equal "sessionId fallback" (Some "xyz") (firstString c [ "sessionID"; "sessionId" ])
    let c = ctx []
    check "no value -> None" (firstString c [ "sessionID" ] |> Option.isNone)

let extractToolContextDirectoryFallback () =
    let c = ctx []
    let tc = extractToolContext c "/plugin"
    equal "directory fallback to plugin" "/plugin" tc.Directory
    check "empty sessionID" (tc.SessionID = "")

let extractToolContextHonoursCtx () =
    let c = ctx [ "cwd", box "/ws"; "sessionId", box "sid-1" ]
    let tc = extractToolContext c "/plugin"
    equal "directory from cwd" "/ws" tc.Directory
    equal "sessionId" "sid-1" tc.SessionID

let textPartsWrapsStrings () =
    let parts = textParts [ "a"; "b" ]
    equal "parts length" 2 parts.Length
    check "first text" (string (Dyn.get parts.[0] "text") = "a")
    check "first type" (string (Dyn.get parts.[0] "type") = "text")

let buildPromptBodyNoAiSettings () =
    let body = buildPromptBody "coder" "do it" null emptySettings
    equal "agent" "coder" (string (Dyn.get body "agent"))
    check "no model key" (Dyn.isNullish (Dyn.get body "model"))
    check "no variant key" (Dyn.isNullish (Dyn.get body "variant"))

let buildPromptBodyWithThinkingLevel () =
    let settings : SubagentAiSettings =
        { ModelString = None
          ThinkingLevel = Some "high"
          Variant = None }
    let body = buildPromptBody "coder" "x" null settings
    equal "variant set" "high" (string (Dyn.get body "variant"))

let signalAbortedFalseOnNull () =
    check "null not aborted" (not (signalAborted null))
    check "undefined-ish not aborted" (not (signalAborted (box null)))
