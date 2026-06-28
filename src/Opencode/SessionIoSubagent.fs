module Wanxiangshu.Opencode.SessionIoSubagent

open Wanxiangshu.Opencode.SubagentTypes
open Wanxiangshu.Opencode.SubagentSpawn
open Wanxiangshu.Opencode.SubagentIo

type DOMException = SubagentTypes.DOMException
type SubagentLaunchOptions = SubagentTypes.SubagentLaunchOptions

let noOutputText = SubagentSpawn.noOutputText
let abortedPrefix = SubagentSpawn.abortedPrefix
let getAbortSignal = SubagentSpawn.getAbortSignal
let invoke1 = SubagentSpawn.invoke1
let buildPromptBody = SubagentSpawn.buildPromptBody
let extractSessionText = SubagentSpawn.extractSessionText
let promptWithAbort = SubagentSpawn.promptWithAbort
let startSubagentSession = SubagentSpawn.startSubagentSession
let runSubagentCoreResult = SubagentIo.runSubagentCoreResult
let runSubagentWithCleanup = SubagentIo.runSubagentWithCleanup
let runSubagent = SubagentIo.runSubagent
