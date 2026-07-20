module Wanxiangshu.Runtime.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.SubagentToolContext
open Wanxiangshu.Runtime.SubagentPromptBody
open Wanxiangshu.Runtime.SubagentAbort

type SubagentAiSettings = SubagentToolContext.SubagentAiSettings
type ToolContext = SubagentToolContext.ToolContext

let emptySettings = SubagentToolContext.emptySettings
let firstString = SubagentToolContext.firstString
let getAbortSignal = SubagentToolContext.getAbortSignal
let extractToolContext = SubagentToolContext.extractToolContext

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let noOutputText = SubagentPromptBody.noOutputText
let abortedPrefix = SubagentPromptBody.abortedPrefix
let noOutputMessage = SubagentPromptBody.noOutputMessage
let abortedPrefixMessage = SubagentPromptBody.abortedPrefixMessage
let textPart = SubagentPromptBody.textPart
let textParts = SubagentPromptBody.textParts
let buildPromptBody = SubagentPromptBody.buildPromptBody

let signalAborted = SubagentAbort.signalAborted
let raceWithAbortSignal = SubagentAbort.raceWithAbortSignal
