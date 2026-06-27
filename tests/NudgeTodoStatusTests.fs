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
    check "isSyntheticAssistantAgent bookkeeper" (isSyntheticAssistantAgent "bookkeeper")
    check "isSyntheticAssistantAgent title" (isSyntheticAssistantAgent "title")
    check "not isSyntheticAssistantAgent orchestrator" (not (isSyntheticAssistantAgent "orchestrator"))

    // --- isNudgePrompt ---
    check "isNudgePrompt todoNudgePrompt" (isNudgePrompt todoNudgePrompt)
    check "isNudgePrompt loopNudgePrompt" (isNudgePrompt loopNudgePrompt)
    check "not isNudgePrompt other" (not (isNudgePrompt "hi"))
