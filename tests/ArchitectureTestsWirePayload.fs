module VibeFs.Tests.ArchitectureTestsWirePayload

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

/// `Opencode/NudgeEventCodec.fs` is no longer the implementation site — it is
/// a thin re-export of the Shell boundary decoder so callers don't churn.
/// Guarded by:
///  * the Shell codec must exist and declare every decoder
///  * the Opencode module must consume it (open + no local definitions)
///  * the `recoverOpenTodosFromMessages` decoder must live in Shell, not in
///    `Opencode/NudgeEffect.fs` where it used to be a private `Dyn.get` soup.
let opencodeSessionEventCodecExists () =
    let codec = requireFile "src/Shell/OpencodeSessionEventCodec.fs" |> nonCommentCode
    check "arch: OpencodeSessionEventCodec defines getSessionID"
        (codec.Contains "let getSessionID")
    check "arch: OpencodeSessionEventCodec defines getPartsText"
        (codec.Contains "let getPartsText")
    check "arch: OpencodeSessionEventCodec defines isCompletedAssistantMessage"
        (codec.Contains "let isCompletedAssistantMessage")
    check "arch: OpencodeSessionEventCodec defines decodeTodos"
        (codec.Contains "let decodeTodos")
    check "arch: OpencodeSessionEventCodec defines decodeLastAssistant"
        (codec.Contains "let decodeLastAssistant")
    check "arch: OpencodeSessionEventCodec defines createPromptBody"
        (codec.Contains "let createPromptBody")
    check "arch: OpencodeSessionEventCodec defines recoverOpenTodosFromMessages"
        (codec.Contains "let recoverOpenTodosFromMessages")
    check "arch: OpencodeSessionEventCodec defines decodeNudgeHostEvent"
        (codec.Contains "let decodeNudgeHostEvent")

let opencodeNudgeEventCodecIsShellAlias () =
    let alias = requireFile "src/Opencode/NudgeEventCodec.fs" |> nonCommentCode
    check "arch: Opencode.NudgeEventCodec opens Shell.OpencodeSessionEventCodec"
        (alias.Contains "open VibeFs.Shell.OpencodeSessionEventCodec")
    check "arch: Opencode.NudgeEventCodec must not re-implement getSessionID (with params)"
        (not (alias.Contains "let getSessionID ("))
    check "arch: Opencode.NudgeEventCodec must not re-implement decodeNudgeHostEvent (with params)"
        (not (alias.Contains "let decodeNudgeHostEvent ("))
    check "arch: Opencode.NudgeEventCodec must not re-implement recoverOpenTodosFromMessages (with params)"
        (not (alias.Contains "let recoverOpenTodosFromMessages ("))
    check "arch: Opencode.NudgeEventCodec must not Dyn.get props sessionID"
        (not (alias.Contains "Dyn.str props \"sessionID\""))

let nudgeEffectRecoversViaCodec () =
    let effect = requireFile "src/Opencode/NudgeEffect.fs" |> nonCommentCode
    check "arch: NudgeEffect calls recoverOpenTodosFromMessages"
        (effect.Contains "recoverOpenTodosFromMessages ")
    check "arch: NudgeEffect must not define recoverOpenTodosFromMessages locally"
        (not (effect.Contains "let private recoverOpenTodosFromMessages"))
    check "arch: NudgeEffect must not Dyn.get part state input todos"
        (not (effect.Contains "Dyn.get state \"input\""))

let sessionLifecycleObserverUsesCodecDecoders () =
    let observer = requireFile "src/Opencode/SessionLifecycleObserver.fs" |> nonCommentCode
    check "arch: SessionLifecycleObserver uses decodeNudgeHostEvent"
        (observer.Contains "decodeNudgeHostEvent ")
    check "arch: SessionLifecycleObserver must not Dyn.str props sessionID"
        (not (observer.Contains "Dyn.str props \"sessionID\""))

let commandHooksUsesCodecSessionID () =
    let hooks = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    check "arch: CommandHooks calls getSessionID"
        (hooks.Contains "getSessionID ")
    check "arch: CommandHooks must not Dyn.str info sessionID locally"
        (not (hooks.Contains "Dyn.str info \"sessionID\""))

let eventHooksUsesCodecSessionID () =
    let hooks = requireFile "src/Opencode/EventHooks.fs" |> nonCommentCode
    check "arch: EventHooks calls getSessionID"
        (hooks.Contains "getSessionID ")
    check "arch: EventHooks must not Dyn.str props sessionID locally"
        (not (hooks.Contains "Dyn.str props \"sessionID\""))