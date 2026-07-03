module Wanxiangshu.Tests.NudgeTodoStatusTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.PromptFragments

let run () =
    // --- todoStatusOfString: 6 string mappings ---
    equal "completed -> Completed" (Some Completed) (todoStatusOfString "completed")
    equal "cancelled -> Cancelled" (Some Cancelled) (todoStatusOfString "cancelled")
    equal "abandoned -> Abandoned" (Some Abandoned) (todoStatusOfString "abandoned")
    equal "in_progress -> InProgress" (Some InProgress) (todoStatusOfString "in_progress")
    equal "inprogress -> InProgress" (Some InProgress) (todoStatusOfString "inprogress")
    equal "pending -> Pending" (Some Pending) (todoStatusOfString "pending")

    // --- todoStatusOfString: case insensitivity ---
    equal "Completed uppercase" (Some Completed) (todoStatusOfString "COMPLETED")
    equal "Cancelled mixed" (Some Cancelled) (todoStatusOfString "CaNcElLeD")
    equal "InProgress underscore upper" (Some InProgress) (todoStatusOfString "IN_PROGRESS")

    // --- isTerminal: true for Completed/Cancelled/Abandoned ---
    check "isTerminal Completed" (isTerminal Completed)
    check "isTerminal Cancelled" (isTerminal Cancelled)
    check "isTerminal Abandoned" (isTerminal Abandoned)

    // --- isTerminal: false for InProgress/Pending ---
    check "not isTerminal InProgress" (not (isTerminal InProgress))
    check "not isTerminal Pending" (not (isTerminal Pending))

    // --- isTerminalAssistantFinish: stop/end -> true ---
    check "isTerminalAssistantFinish stop" (isTerminalAssistantFinish "stop")
    check "isTerminalAssistantFinish end" (isTerminalAssistantFinish "end")

    // --- isTerminalAssistantFinish: tool_calls/abort/tool_abort -> false ---
    check "not isTerminalAssistantFinish tool_calls" (not (isTerminalAssistantFinish "tool_calls"))
    check "not isTerminalAssistantFinish abort" (not (isTerminalAssistantFinish "abort"))
    check "not isTerminalAssistantFinish tool_abort" (not (isTerminalAssistantFinish "tool_abort"))

    // --- isSyntheticAssistantAgent ---
    check "isSyntheticAssistantAgent compaction" (isSyntheticAssistantAgent "compaction")
    check "isSyntheticAssistantAgent title" (isSyntheticAssistantAgent "title")
    check "not isSyntheticAssistantAgent bookkeeper" (not (isSyntheticAssistantAgent "bookkeeper"))
    check "not isSyntheticAssistantAgent orchestrator" (not (isSyntheticAssistantAgent "orchestrator"))

    // --- isNudgePrompt ---
    check "isNudgePrompt todoNudgePrompt" (isNudgePrompt todoNudgePrompt)
    check "isNudgePrompt loopNudgePrompt" (isNudgePrompt loopNudgePrompt)
    check "not isNudgePrompt other" (not (isNudgePrompt "hi"))

    // --- todoNudgePromptFor with front matter ---
    let todoPromptWithFm = todoNudgePromptFor ["todo1"; "todo2"]
    check "todoNudgePromptFor contains front matter" (todoPromptWithFm.Contains("---"))
    check "todoNudgePromptFor contains todos list" (todoPromptWithFm.Contains("todos"))
    check "todoNudgePromptFor contains todo1" (todoPromptWithFm.Contains("todo1"))
    check "todoNudgePromptFor contains original prose" (todoPromptWithFm.Contains("There are still incomplete todos"))

    // --- loopNudgePromptFor with front matter ---
    let loopPromptWithFm = loopNudgePromptFor ["todo1"]
    check "loopNudgePromptFor contains front matter" (loopPromptWithFm.Contains("---"))
    check "loopNudgePromptFor contains original prose" (loopPromptWithFm.Contains("You are in loop mode"))

    // --- isNudgePrompt recognizes new format ---
    check "isNudgePrompt recognizes todoNudgePromptFor" (isNudgePrompt todoPromptWithFm)
    check "isNudgePrompt recognizes loopNudgePromptFor" (isNudgePrompt loopPromptWithFm)
