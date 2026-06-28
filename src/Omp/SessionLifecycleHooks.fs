module Wanxiangshu.Omp.SessionLifecycleHooks

open Wanxiangshu.Omp.NudgeHooks
open Wanxiangshu.Omp.TodoHooks

let applyActiveToolFilterForMainSession = NudgeHooks.applyActiveToolFilterForMainSession
let beforeAgentStartHandler = NudgeHooks.beforeAgentStartHandler
let toolCallHandler = NudgeHooks.toolCallHandler
let turnStartHandler = NudgeHooks.turnStartHandler
let agentEndHandler = NudgeHooks.agentEndHandler

let bookkeepingSubagentTools = TodoHooks.bookkeepingSubagentTools
let recordsToBookkeeper = TodoHooks.recordsToBookkeeper
let isReadOnlyExecutor = TodoHooks.isReadOnlyExecutor
let toolResultHandler = TodoHooks.toolResultHandler
let sessionStartHandler = TodoHooks.sessionStartHandler
let sessionShutdownHandler = TodoHooks.sessionShutdownHandler
