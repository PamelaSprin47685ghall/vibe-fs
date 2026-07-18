module Wanxiangshu.Hosts.Omp.SessionLifecycleHooks

open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.NudgeHooks
open Wanxiangshu.Hosts.Omp.TodoHooks

let applyActiveToolFilterForMainSession =
    NudgeToolFilter.applyActiveToolFilterForMainSession

let beforeAgentStartHandler = NudgeHooks.beforeAgentStartHandler
let toolCallHandler = NudgeHooks.toolCallHandler
let turnStartHandler = NudgeHooks.turnStartHandler
let agentEndHandler = NudgeHooks.agentEndHandler

let toolResultHandler = TodoHooks.toolResultHandler
let sessionStartHandler = TodoHooks.sessionStartHandler
let sessionPromptHandler = TodoHooks.sessionPromptHandler
let sessionShutdownHandler = TodoHooks.sessionShutdownHandler
