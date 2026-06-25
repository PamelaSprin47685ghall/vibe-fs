module VibeFs.Opencode.NudgeEventCodec

/// Opencode session event codec — now a thin re-export of the canonical
/// Shell boundary decoder. The original `Dyn.get` / `Dyn.str` payload
/// decoding lives in `VibeFs.Shell.OpencodeSessionEventCodec` so the wire
/// format has a single read site, while Opencode callers keep the historical
/// `VibeFs.Opencode.NudgeEventCodec` import path (no churn at call sites).
open VibeFs.Shell.OpencodeSessionEventCodec

let getSessionID = VibeFs.Shell.OpencodeSessionEventCodec.getSessionID
let getPartsText = VibeFs.Shell.OpencodeSessionEventCodec.getPartsText
let isCompletedAssistantMessage = VibeFs.Shell.OpencodeSessionEventCodec.isCompletedAssistantMessage
let decodeTodos = VibeFs.Shell.OpencodeSessionEventCodec.decodeTodos
let recoverOpenTodosFromMessages = VibeFs.Shell.OpencodeSessionEventCodec.recoverOpenTodosFromMessages
let decodeLastAssistant = VibeFs.Shell.OpencodeSessionEventCodec.decodeLastAssistant
let createPromptBody = VibeFs.Shell.OpencodeSessionEventCodec.createPromptBody
let decodeNudgeHostEvent = VibeFs.Shell.OpencodeSessionEventCodec.decodeNudgeHostEvent