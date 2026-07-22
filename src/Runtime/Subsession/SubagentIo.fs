module Wanxiangshu.Runtime.SubagentIo

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.SubagentToolContext
open Wanxiangshu.Runtime.SubagentHostPayload
open Wanxiangshu.Runtime.SubagentAbort

type SubagentAiSettings = SubagentToolContext.SubagentAiSettings
type ToolContext = SubagentToolContext.ToolContext

let emptySettings = SubagentToolContext.emptySettings
let firstString = SubagentToolContext.firstString
let getAbortSignal = SubagentToolContext.getAbortSignal
let extractToolContext = SubagentToolContext.extractToolContext

let invoke1 (arg: obj) (method: string) (target: obj) : JS.Promise<obj> = unbox (target?(method) (arg))

let noOutputText = SubagentHostPayload.noOutputText
let abortedPrefix = SubagentHostPayload.abortedPrefix
let noOutputMessage = SubagentHostPayload.noOutputMessage
let abortedPrefixMessage = SubagentHostPayload.abortedPrefixMessage
let textPart = SubagentHostPayload.textPart
let textParts = SubagentHostPayload.textParts
let buildHostPayload = SubagentHostPayload.buildHostPayload

let signalAborted = SubagentAbort.signalAborted
let raceWithAbortSignal = SubagentAbort.raceWithAbortSignal
