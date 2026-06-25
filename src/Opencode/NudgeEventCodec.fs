module VibeFs.Opencode.NudgeEventCodec

/// Opencode session event codec — thin re-export of the canonical Shell boundary decoder.
open VibeFs.Shell.OpencodeSessionEventCodec

let getSessionID = VibeFs.Shell.OpencodeSessionEventCodec.getSessionID
let getPartsText = VibeFs.Shell.OpencodeSessionEventCodec.getPartsText
let isCompletedAssistantMessage = VibeFs.Shell.OpencodeSessionEventCodec.isCompletedAssistantMessage
let decodeTodos = VibeFs.Shell.OpencodeSessionEventCodec.decodeTodos
let recoverOpenTodosFromMessages = VibeFs.Shell.OpencodeSessionEventCodec.recoverOpenTodosFromMessages
let decodeLastAssistant = VibeFs.Shell.OpencodeSessionEventCodec.decodeLastAssistant
let createPromptBody = VibeFs.Shell.OpencodeSessionEventCodec.createPromptBody
let decodeNudgeHostEvent = VibeFs.Shell.OpencodeSessionEventCodec.decodeNudgeHostEvent