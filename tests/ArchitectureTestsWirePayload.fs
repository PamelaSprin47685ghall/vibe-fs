module Wanxiangshu.Tests.ArchitectureTestsWirePayload

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

/// `Shell/OpencodeSessionEventCodec.fs` is the canonical boundary decoder
/// for Opencode session event payloads. All Opencode consumers (NudgeEffect,
/// SessionLifecycleObserver, CommandHooks, EventHooks) open it directly.
/// The `recoverOpenTodosFromMessages` decoder lives in Shell, not scattered
/// as private `Dyn.get` soup in NudgeEffect.
///
/// Guarded by:
///  * the Shell codec must exist and declare every decoder
///  * consumers must use these decoders, not inline Dyn access
let opencodeSessionEventCodecExists () =
    let codec = requireFile "src/Shell/OpencodeSessionEventCodec.fs" |> nonCommentCode
    check "arch: OpencodeSessionEventCodec defines getSessionID" (codec.Contains "let getSessionID")
    check "arch: OpencodeSessionEventCodec defines getPartsText" (codec.Contains "let getPartsText")

    check
        "arch: OpencodeSessionEventCodec defines isCompletedAssistantMessage"
        (codec.Contains "let isCompletedAssistantMessage")

    check "arch: OpencodeSessionEventCodec defines decodeTodos" (codec.Contains "let decodeTodos")
    check "arch: OpencodeSessionEventCodec defines decodeLastAssistant" (codec.Contains "let decodeLastAssistant")
    check "arch: OpencodeSessionEventCodec defines createPromptBody" (codec.Contains "let createPromptBody")

    check
        "arch: OpencodeSessionEventCodec defines recoverOpenTodosFromMessages"
        (codec.Contains "let recoverOpenTodosFromMessages")


let nudgeEffectRecoversViaCodec () =
    let effect = requireFile "src/Opencode/NudgeEffect.fs" |> nonCommentCode
    check "arch: NudgeEffect calls recoverOpenTodosFromMessages" (effect.Contains "recoverOpenTodosFromMessages ")

    check
        "arch: NudgeEffect must not define recoverOpenTodosFromMessages locally"
        (not (effect.Contains "let private recoverOpenTodosFromMessages"))

    check "arch: NudgeEffect must not Dyn.get part state input todos" (not (effect.Contains "Dyn.get state \"input\""))

let sessionLifecycleObserverUsesCodecDecoders () =
    let observer =
        requireFile "src/Opencode/SessionLifecycleObserver.fs" |> nonCommentCode

    check
        "arch: SessionLifecycleObserver must not Dyn.str props sessionID"
        (not (observer.Contains "Dyn.str props \"sessionID\""))

let commandHooksUsesCodecSessionID () =
    let hooks = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode

    check
        "arch: CommandHooks must not Dyn.str info sessionID locally"
        (not (hooks.Contains "Dyn.str info \"sessionID\""))

let eventHooksUsesCodecSessionID () =
    let hooks = requireFile "src/Opencode/EventHooks.fs" |> nonCommentCode
    check "arch: EventHooks calls getSessionID" (hooks.Contains "getSessionID ")

    check
        "arch: EventHooks must not Dyn.str props sessionID locally"
        (not (hooks.Contains "Dyn.str props \"sessionID\""))
