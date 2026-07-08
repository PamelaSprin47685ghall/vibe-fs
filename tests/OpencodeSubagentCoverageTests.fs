module Wanxiangshu.Tests.OpencodeSubagentCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Opencode.SessionIoSubagent
open Wanxiangshu.Opencode.SubagentTypes
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

let sessionIoSubagentBuildPromptBodyMinimal () =
    let options =
        { agent = "executor"
          title = "T"
          prompt = "do it"
          directory = "/tmp"
          sessionID = ""
          tools = null
          aiSettings =
            { modelString = None
              thinkingLevel = None } }

    let body = buildPromptBody options "child-1"
    check "path.id present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "path") "id")))
    check "body.agent present" (Dyn.str (Dyn.get body "body") "agent" = "executor")

let sessionIoSubagentBuildPromptBodyTools () =
    let toolsObj = createObj []

    let options =
        { agent = "executor"
          title = "T"
          prompt = "do it"
          directory = "/tmp"
          sessionID = ""
          tools = toolsObj
          aiSettings =
            { modelString = None
              thinkingLevel = None } }

    let body = buildPromptBody options "child-2"
    check "body.tools present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "tools")))

let sessionIoSubagentBuildPromptBodyModel () =
    let options =
        { agent = "executor"
          title = "T"
          prompt = "do it"
          directory = "/tmp"
          sessionID = ""
          tools = null
          aiSettings =
            { modelString = Some "openai/gpt-4"
              thinkingLevel = None } }

    let body = buildPromptBody options "child-3"
    check "body.model present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "model")))

let sessionIoSubagentBuildPromptBodyThinkingLevel () =
    let options =
        { agent = "executor"
          title = "T"
          prompt = "do it"
          directory = "/tmp"
          sessionID = ""
          tools = null
          aiSettings =
            { modelString = None
              thinkingLevel = Some "high" } }

    let body = buildPromptBody options "child-4"
    check "body.variant present" (not (Dyn.isNullish (Dyn.get (Dyn.get body "body") "variant")))

let sessionIoSubagentInvoke1 () =
    let mutable receivedArg = ""

    let target =
        createObj
            [ "myMethod",
              box (fun (arg: obj) ->
                  receivedArg <- string arg
                  box "result") ]

    let p = invoke1 (box "hello") "myMethod" target
    equal "invoke1 result" "result" (string (unbox p))
    equal "invoke1 arg passed" "hello" receivedArg

let sessionIoSubagentExtractSessionText () =
    let fakeData =
        [| createObj
               [ "info",
                 box (
                     createObj
                         [ "id", box "msg-1"
                           "sessionID", box "sess-1"
                           "role", box "assistant"
                           "agent", box "a"
                           "isError", box false
                           "toolName", box ""
                           "details", box null
                           "time", box null ]
                 )
                 "parts", box [| createObj [ "type", box "text"; "text", box "Hello from assistant" ] |] ] |]

    let fakeSession =
        createObj [ "messages", box (fun (_arg: obj) -> Promise.lift (box (createObj [ "data", box fakeData ]))) ]

    let fakeClient = createObj [ "session", box fakeSession ]

    promise {
        let! text = extractSessionText fakeClient "sess-1" ""
        equal "assistant text" "Hello from assistant" text
    }

let run () =
    promise {
        sessionIoSubagentBuildPromptBodyMinimal ()
        sessionIoSubagentBuildPromptBodyTools ()
        sessionIoSubagentBuildPromptBodyModel ()
        sessionIoSubagentBuildPromptBodyThinkingLevel ()
        sessionIoSubagentInvoke1 ()
        do! sessionIoSubagentExtractSessionText ()
    }
