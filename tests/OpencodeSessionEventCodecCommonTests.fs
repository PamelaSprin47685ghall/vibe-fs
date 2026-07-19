module Wanxiangshu.Tests.OpencodeSessionEventCodecCommonTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent

let sessionEventTypesContainsCreated () =
    check "session.created" (Set.contains "session.created" sessionEventTypes)

let sessionEventTypesContainsDeleted () =
    check "session.deleted" (Set.contains "session.deleted" sessionEventTypes)

let pluginObservedRejectsStreamingDelta () =
    check "message.part.delta" (not (isPluginObservedHostEvent "message.part.delta"))

let pluginObservedAcceptsSessionStatus () =
    check "session.status" (isPluginObservedHostEvent "session.status")

let getSessionIDFromTopLevel () =
    let props = unbox (createObj [ "sessionID", box "s1" ])
    equal "s1" "s1" (getSessionID "session.updated" props)

let getSessionIDFromPart () =
    let props = unbox (createObj [ "part", box (createObj [ "sessionID", box "s2" ]) ])
    equal "s2" "s2" (getSessionID "session.updated" props)

let getSessionIDFromInfoSessionID () =
    let props = unbox (createObj [ "info", box (createObj [ "sessionID", box "s3" ]) ])
    equal "s3" "s3" (getSessionID "session.updated" props)

let getSessionIDFromInfoIdForSessionLifecycle () =
    let props = unbox (createObj [ "info", box (createObj [ "id", box "s4" ]) ])
    equal "s4" "s4" (getSessionID "session.created" props)

let getSessionIDFallsBackToEmpty () =
    let props = unbox (createObj [])
    equal "empty" "" (getSessionID "session.updated" props)

let getPartsTextFromArray () =
    let parts =
        box
            [| box (createObj [ "type", box "text"; "text", box "hello" ])
               box (createObj [ "type", box "text"; "text", box "world" ]) |]

    equal "concat" "hello\nworld" (getPartsText parts)

let getPartsTextSkipsNonText () =
    let parts =
        box
            [| box (createObj [ "type", box "tool_use" ])
               box (createObj [ "type", box "text"; "text", box "only" ]) |]

    equal "skip" "only" (getPartsText parts)

let getPartsTextNonArray () =
    equal "empty" "" (getPartsText (box "not an array"))

let isCompletedAssistantMessageNull () =
    equal "false" false (isCompletedAssistantMessage null)

let isCompletedAssistantMessageAssistantWithTerminalFinish () =
    let info = box (createObj [ "role", box "assistant"; "finish", box "completed" ])
    check "true" (isCompletedAssistantMessage info)

let isCompletedAssistantMessageAssistantWithNonTerminalFinish () =
    let info = box (createObj [ "role", box "assistant"; "finish", box "tool_use" ])
    check "false" (not (isCompletedAssistantMessage info))

let isCompletedAssistantMessageErrorReturnsFalse () =
    let info = box (createObj [ "role", box "assistant"; "error", box "crash" ])
    check "false" (not (isCompletedAssistantMessage info))

let isCompletedAssistantMessageNonAssistant () =
    let info = box (createObj [ "role", box "user"; "finish", box "completed" ])
    check "false" (not (isCompletedAssistantMessage info))

let run () =
    sessionEventTypesContainsCreated ()
    sessionEventTypesContainsDeleted ()
    pluginObservedRejectsStreamingDelta ()
    pluginObservedAcceptsSessionStatus ()
    getSessionIDFromTopLevel ()
    getSessionIDFromPart ()
    getSessionIDFromInfoSessionID ()
    getSessionIDFromInfoIdForSessionLifecycle ()
    getSessionIDFallsBackToEmpty ()
    getPartsTextFromArray ()
    getPartsTextSkipsNonText ()
    getPartsTextNonArray ()
    isCompletedAssistantMessageNull ()
    isCompletedAssistantMessageAssistantWithTerminalFinish ()
    isCompletedAssistantMessageAssistantWithNonTerminalFinish ()
    isCompletedAssistantMessageErrorReturnsFalse ()
    isCompletedAssistantMessageNonAssistant ()
