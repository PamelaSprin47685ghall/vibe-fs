module Wanxiangshu.Hosts.Opencode.SubagentSpawn

open Wanxiangshu.Hosts.Opencode.SubagentSpawnInput
open Wanxiangshu.Hosts.Opencode.SubagentSpawnCleanup
open Wanxiangshu.Hosts.Opencode.SubagentSpawnTransport

let noOutputText = SubagentSpawnCleanup.noOutputText
let abortedPrefix = SubagentSpawnCleanup.abortedPrefix

let getAbortSignal = SubagentSpawnInput.getAbortSignal
let invoke1 = SubagentSpawnInput.invoke1
let buildPromptBody = SubagentSpawnInput.buildPromptBody

let extractSessionText = SubagentSpawnCleanup.extractSessionText
let physicalAbort = SubagentSpawnCleanup.physicalAbort

let createPromptAbortGate = SubagentSpawnTransport.createPromptAbortGate
let bumpPromptAbortEpoch = SubagentSpawnTransport.bumpPromptAbortEpoch
let closePromptAbortGate = SubagentSpawnTransport.closePromptAbortGate
let promptWithAbortOwned = SubagentSpawnTransport.promptWithAbortOwned
let promptWithAbort = SubagentSpawnTransport.promptWithAbort
let startSubagentSession = SubagentSpawnTransport.startSubagentSession
