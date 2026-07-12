module Wanxiangshu.Tests.FallbackMessageCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FallbackMessageCodec
open Wanxiangshu.Shell.FallbackMessageParser

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Shell.NudgeRuntimeTypes

let private mkTodoPart (todos: obj array) : obj =
    createObj
        [ "type", box "tool"
          "tool", box "task"
          "state", box (createObj [ "input", box (createObj [ "todos", box todos ]) ]) ]

let private mkTodoMsg (todos: obj array) : obj =
    createObj [ "parts", box [| mkTodoPart todos |] ]

let private mkTodo (status: string) : obj = createObj [ "status", box status ]

let private mkAssistantTextMsg (text: string) : obj =
    createObj
        [ "info", box (createObj [ "role", box "assistant" ])
          "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

let allTodosCompleted_emptyArray () =
    check "empty → false" (not (allTodosCompleted [||]))

let allTodosCompleted_allCompleted () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "completed" |]
    check "all completed → true" (allTodosCompleted [| m |])

let allTodosCompleted_mixedStatus () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "in_progress" |]
    check "mixed → false" (not (allTodosCompleted [| m |]))

let allTodosCompleted_withCancelled () =
    let m = mkTodoMsg [| mkTodo "completed"; mkTodo "cancelled" |]
    check "completed+cancelled → true" (allTodosCompleted [| m |])

let allTodosCompleted_noneCompleted () =
    let m = mkTodoMsg [| mkTodo "in_progress"; mkTodo "pending" |]
    check "none completed → false" (not (allTodosCompleted [| m |]))

let containsToolCallAsText_functionEqualsPattern () =
    check
        "<function=read> -> true"
        (containsToolCallAsText "<function=read>\n<parameter=filePath>\n/foo/bar.fs\n</parameter>\n</function>")

let containsToolCallAsText_noXml () =
    check "plain text → false" (not (containsToolCallAsText "just some normal text"))

let containsToolCallAsText_toolCallTag () =
    check "<tool_call> → true" (containsToolCallAsText "let me use <tool_call>name=\"read\"</tool_call> to read")

let containsToolCallAsText_functionTag () =
    check "<function> → true" (containsToolCallAsText "<function>read</function>")

let containsToolCallAsText_toolNameTag () =
    check "<read> → true" (containsToolCallAsText "<read file_path=\"/foo\">")

let containsToolCallAsText_nameAttribute () =
    check "name=\"write\" → true" (containsToolCallAsText "<invoke name=\"write\">content</invoke>")

let scanToolCallAsText_noMessages () =
    check "empty → None" (None = (scanToolCallAsText [||]))

let scanToolCallAsText_noAssistantText () =
    let userMsg =
        createObj
            [ "info", box (createObj [ "role", box "user" ])
              "parts", box [| createObj [ "type", box "text"; "text", box "<read>" ] |] ]

    check "user text → None" (None = (scanToolCallAsText [| userMsg |]))

let scanToolCallAsText_assistantTextWithToolCall () =
    let prompt =
        scanToolCallAsText [| mkAssistantTextMsg "I'll use <tool_call> to read" |]

    check "assistant with tool call → Some" (Option.isSome prompt)

let scanToolCallAsText_assistantTextClean () =
    let prompt =
        scanToolCallAsText [| mkAssistantTextMsg "I'll read this file properly." |]

    check "assistant clean text → None" (None = prompt)

let scanToolCallAsText_markdownOrTextDiscussionNotToolCall () =
    let prompt1 =
        scanToolCallAsText [| mkAssistantTextMsg "Please use `<read>` tool to read the file." |]

    check "markdown inline code should not trigger recovery" (None = prompt1)

    let prompt2 =
        scanToolCallAsText [| mkAssistantTextMsg "The concept of <read> and <write> in file system." |]

    check "discussion text should not trigger recovery" (None = prompt2)

let private mkAssistantMsg (parts: obj array) : obj =
    createObj [ "info", box (createObj [ "role", box "assistant" ]); "parts", box parts ]

let private mkToolPart (toolName: string) : obj =
    createObj [ "type", box "tool"; "tool", box toolName ]

let private mkTextPart (text: string) : obj =
    createObj [ "type", box "text"; "text", box text ]

let private mkReasoningPart (text: string) : obj =
    createObj [ "type", box "reasoning"; "text", box text ]

let isIdleNoContentAndNoTools_noAssistant () =
    let userMsg =
        createObj
            [ "info", box (createObj [ "role", box "user" ])
              "parts", box [| mkTextPart "hello" |] ]

    check "no assistant message → false" (not (isIdleNoContentAndNoTools [| userMsg |]))

let isIdleNoContentAndNoTools_emptyAssistant () =
    let m = mkAssistantMsg [||]
    check "assistant with no parts → true" (isIdleNoContentAndNoTools [| m |])

let isIdleNoContentAndNoTools_onlyReasoning () =
    let m = mkAssistantMsg [| mkReasoningPart "thinking..." |]
    check "assistant with only reasoning → true" (isIdleNoContentAndNoTools [| m |])

let isIdleNoContentAndNoTools_withText () =
    let m = mkAssistantMsg [| mkTextPart "here is help" |]
    check "assistant with text → false" (not (isIdleNoContentAndNoTools [| m |]))

let isIdleNoContentAndNoTools_withTool () =
    let m = mkAssistantMsg [| mkToolPart "read" |]
    check "assistant with tool → false" (not (isIdleNoContentAndNoTools [| m |]))

let isIdleNoContentAndNoTools_shouldNotScanPastLatestAssistant () =
    let emptyMsg = mkAssistantMsg [||]

    let userMsg =
        createObj
            [ "info", box (createObj [ "role", box "user" ])
              "parts", box [| mkTextPart "new question" |] ]

    let toolMsg = mkAssistantMsg [| mkToolPart "read" |]
    let msgs = [| emptyMsg; userMsg; toolMsg |]

    check
        "latest assistant has tool, even if history has empty assistant -> false"
        (not (isIdleNoContentAndNoTools msgs))

let tryGetLastAssistantAbortInfo_shouldNotScanPastLatestAssistant () =
    let abortMsg =
        createObj
            [ "info", box (createObj [ "role", box "assistant"; "finish", box "abort" ])
              "parts", box [||] ]

    let userMsg =
        createObj
            [ "info", box (createObj [ "role", box "user" ])
              "parts", box [| mkTextPart "new question" |] ]

    let successMsg =
        createObj
            [ "info", box (createObj [ "role", box "assistant"; "finish", box "stop" ])
              "parts", box [| mkTextPart "success output" |] ]

    let msgs = [| abortMsg; userMsg; successMsg |]

    check
        "latest assistant is successful, even if history has aborted assistant -> None"
        (Option.isNone (tryGetLastAssistantAbortInfo msgs))

let private mkMsg (role: string) (text: string) (modelStr: string option) : obj =
    let info =
        let baseObj = createObj [ "role", box role ]

        match modelStr with
        | Some m ->
            let parts = m.Split('/')

            if parts.Length = 2 then
                Dyn.withKey
                    baseObj
                    "model"
                    (box
                        {| providerID = parts.[0]
                           modelID = parts.[1] |})
            else
                Dyn.withKey baseObj "model" (box m)
        | None -> baseObj

    createObj
        [ "info", box info
          "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

let testResolveNudgeModel () =
    let sid = "session-test-nudge-model"

    // Test case 1: Real user prompt with model specified
    let runtime1 = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

    let msgs1 =
        [| mkMsg "user" "some query" (Some "openai/gpt-4")
           mkMsg "assistant" "thinking..." (Some "openai/gpt-4") |]

    let res1 = resolveNudgeModel msgs1 runtime1 sid (Some "openai/gpt-3.5")
    equal "use last user model when real user prompt" (Some "openai/gpt-4") res1

    // Test case 2: Real user prompt without model specified -> fallback to lastAssistantModel
    let runtime2 = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

    let msgs2 =
        [| mkMsg "user" "some query" None
           mkMsg "assistant" "thinking..." (Some "openai/gpt-4") |]

    let res2 = resolveNudgeModel msgs2 runtime2 sid (Some "openai/gpt-3.5")
    equal "fallback to assistant model when user msg has no model" (Some "openai/gpt-4") res2

    // Test case 3: Fallback-injected user message -> use injected model
    let runtime3 = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

    let msgs3 =
        [| mkMsg "user" "​" (Some "openai/gpt-4")
           mkMsg "assistant" "thinking..." (Some "openai/gpt-4") |]

    runtime3.SetInjectedModel
        sid
        { ProviderID = "anthropic"
          ModelID = "claude-3-sonnet"
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

    let res3 = resolveNudgeModel msgs3 runtime3 sid (Some "openai/gpt-4")
    equal "use injected model from runtime state" (Some "anthropic/claude-3-sonnet") res3

    // Test case 4: Nudge message -> use same model
    let runtime4 = Wanxiangshu.Shell.FallbackRuntimeState.FallbackRuntimeState()

    let msgs4 =
        [| mkMsg
               "user"
               "There are still incomplete todos. Continue working through the remaining items."
               (Some "openai/gpt-3.5")
           mkMsg "assistant" "thinking..." (Some "openai/gpt-4") |]

    let res4 = resolveNudgeModel msgs4 runtime4 sid (Some "openai/gpt-4")
    equal "use nudge model or last assistant model for nudge prompt" (Some "openai/gpt-3.5") res4

let run () =
    allTodosCompleted_emptyArray ()
    allTodosCompleted_allCompleted ()
    allTodosCompleted_mixedStatus ()
    allTodosCompleted_withCancelled ()
    allTodosCompleted_noneCompleted ()
    containsToolCallAsText_functionEqualsPattern ()
    containsToolCallAsText_noXml ()
    containsToolCallAsText_toolCallTag ()
    containsToolCallAsText_functionTag ()
    containsToolCallAsText_toolNameTag ()
    containsToolCallAsText_nameAttribute ()
    scanToolCallAsText_noMessages ()
    scanToolCallAsText_noAssistantText ()
    scanToolCallAsText_assistantTextWithToolCall ()
    scanToolCallAsText_assistantTextClean ()
    scanToolCallAsText_markdownOrTextDiscussionNotToolCall ()
    isIdleNoContentAndNoTools_noAssistant ()
    isIdleNoContentAndNoTools_emptyAssistant ()
    isIdleNoContentAndNoTools_onlyReasoning ()
    isIdleNoContentAndNoTools_withText ()
    isIdleNoContentAndNoTools_withTool ()
    isIdleNoContentAndNoTools_shouldNotScanPastLatestAssistant ()
    tryGetLastAssistantAbortInfo_shouldNotScanPastLatestAssistant ()
    testResolveNudgeModel ()
