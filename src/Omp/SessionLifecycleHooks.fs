module Wanxiangshu.Omp.SessionLifecycleHooks

open Wanxiangshu.Omp.NudgeHooks
open Wanxiangshu.Omp.TodoHooks

let applyActiveToolFilterForMainSession = NudgeHooks.applyActiveToolFilterForMainSession
let beforeAgentStartHandler = NudgeHooks.beforeAgentStartHandler
let toolCallHandler = NudgeHooks.toolCallHandler
let turnStartHandler = NudgeHooks.turnStartHandler
let agentEndHandler = NudgeHooks.agentEndHandler

let toolResultHandler = TodoHooks.toolResultHandler
let sessionStartHandler = TodoHooks.sessionStartHandler
let sessionPromptHandler = TodoHooks.sessionPromptHandler
let sessionShutdownHandler = TodoHooks.sessionShutdownHandler
