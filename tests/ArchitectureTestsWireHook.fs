module VibeFs.Tests.ArchitectureTestsWireHook

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.ArchitectureTestsSupport

let opencodeHookExecuteUsesFromOpencode () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    check "arch: Opencode HookExecute opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode HookExecute opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode HookExecute uses fromOpencode for bookkeeper session"
        (code.Contains "fromOpencode input pluginDirectory")
    check "arch: Opencode HookExecute uses Execution.SessionId"
        (code.Contains "Execution.SessionId")
    check "arch: Opencode HookExecute uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode HookExecute uses argsFromHookInput"
        (code.Contains "argsFromHookInput")
    check "arch: Opencode HookExecute uses executorModeFromHookInput"
        (code.Contains "executorModeFromHookInput")
    check "arch: Opencode HookExecute uses hookOutputError and hookOutputText"
        ((code.Contains "hookOutputError") && (code.Contains "hookOutputText"))
    check "arch: Opencode HookExecute uses setHookOutputString"
        (code.Contains "setHookOutputString")
    check "arch: Opencode HookExecute uses hookOutputString"
        (code.Contains "hookOutputString")
    check "arch: Opencode HookExecute must not private setOutput"
        (not (code.Contains "let private setOutput"))
    check "arch: Opencode HookExecute must not Dyn.get output output"
        (not (code.Contains "Dyn.get output \"output\""))
    check "arch: Opencode HookExecute must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode HookExecute must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))

let opencodeHookExecuteUsesPatchToolsCodec () =
    let code = requireFile "src/Opencode/HookExecute.fs" |> nonCommentCode
    check "arch: Opencode HookExecute opens PatchToolsCodec" (code.Contains "PatchToolsCodec")
    check "arch: Opencode HookExecute uses decodeApplyPatchFields" (code.Contains "decodeApplyPatchFields")
    check "arch: Opencode HookExecute must not Dyn.str args patchText" (not (code.Contains "Dyn.str args \"patchText\""))
    check "arch: Opencode HookExecute must not Dyn.str args patch" (not (code.Contains "Dyn.str args \"patch\""))
    let patchIdx = code.IndexOf "decodeApplyPatchFields"
    let patchWindow =
        if patchIdx >= 0 then code.Substring(patchIdx, min 400 (code.Length - patchIdx))
        else ""
    check "arch: Opencode HookExecute patch decode failure sets output error"
        (patchWindow.Contains "setHookError")
    check "arch: Opencode HookExecute opens ToolExecute"
        (code.Contains "ToolExecute")
    check "arch: Opencode HookExecute patch decode failure uses wireEncodeToolError apply_patch"
        (patchWindow.Contains "wireEncodeToolError \"apply_patch\"")
    check "arch: Opencode HookExecute patch decode failure must not formatDomainError"
        (not (patchWindow.Contains "formatDomainError"))

let opencodePluginCoreUsesFromOpencode () =
    let code = requireFile "src/Opencode/PluginCore.fs" |> nonCommentCode
    check "arch: Opencode PluginCore opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode PluginCore uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode PluginCore must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode PluginCore must not fromOpencode ctx empty for directory"
        (not (code.Contains "(fromOpencode ctx \"\")"))

let opencodeCommandHooksUsesFromOpencode () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    check "arch: Opencode CommandHooks opens ToolRuntimeContext"
        (code.Contains "ToolRuntimeContext")
    check "arch: Opencode CommandHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode CommandHooks loop uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode CommandHooks uses commandNameFromHookInput"
        (code.Contains "commandNameFromHookInput")
    check "arch: Opencode CommandHooks uses commandArgumentsFromHookInput"
        (code.Contains "commandArgumentsFromHookInput")
    check "arch: Opencode CommandHooks loop-review uses pluginDirectoryFromCtx"
        (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode CommandHooks uses decodeHostEventEnvelope for KG cleanup"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode CommandHooks must not Dyn.str ctx directory"
        (not (code.Contains "Dyn.str ctx \"directory\""))
    check "arch: Opencode CommandHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode CommandHooks must not Dyn.str input command"
        (not (code.Contains "Dyn.str input \"command\""))
    check "arch: Opencode CommandHooks must not Dyn.str input arguments"
        (not (code.Contains "Dyn.str input \"arguments\""))

let commandHooksUsesRegisterLoopReviewCommands () =
    let code = requireFile "src/Opencode/CommandHooks.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/OpencodeHookInputCodec.fs" |> nonCommentCode
    check "arch: CommandHooks registerCommands calls registerLoopReviewCommands"
        (code.Contains "registerLoopReviewCommands")
    check "arch: CommandHooks must not Dyn.get cfg command"
        (not (code.Contains "Dyn.get cfg \"command\""))
    check "arch: OpencodeHookInputCodec defines registerLoopReviewCommands"
        (codec.Contains "let registerLoopReviewCommands")
    check "arch: OpencodeHookInputCodec defines ensureCommandTemplate"
        (codec.Contains "let ensureCommandTemplate")

let opencodeChatHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ChatHooks.fs" |> nonCommentCode
    check "arch: Opencode ChatHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ChatHooks uses resolveHookAgent"
        (code.Contains "resolveHookAgent")
    check "arch: Opencode ChatHooks uses sessionIdFromHookInput"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode ChatHooks must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))

let chatHooksUsesChatHookOutputCodec () =
    let code = requireFile "src/Opencode/ChatHooks.fs" |> nonCommentCode
    check "arch: Opencode ChatHooks references ChatHookOutputCodec"
        (code.Contains "ChatHookOutputCodec")
    check "arch: Opencode ChatHooks must not Dyn.keys existingTools loop"
        (not (code.Contains "Dyn.keys existingTools"))
    check "arch: Opencode ChatHooks uses filterChatToolsForAgent"
        (code.Contains "filterChatToolsForAgent")
    check "arch: Opencode ChatHooks uses encodeToolsOverridesToMessage"
        (code.Contains "encodeToolsOverridesToMessage")
    check "arch: Opencode ChatHooks must not Dyn.get output parts"
        (not (code.Contains "Dyn.get output \"parts\""))
    check "arch: Opencode ChatHooks uses partsFromHookOutput"
        (code.Contains "partsFromHookOutput")

let opencodeSessionLifecycleObserverUsesHookInputCodec () =
    let code = requireFile "src/Opencode/SessionLifecycleObserver.fs" |> nonCommentCode
    check "arch: Opencode SessionLifecycleObserver opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode SessionLifecycleObserver uses sessionIdFromHookInput for command"
        (code.Contains "sessionIdFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses toolNameFromHookInput"
        (code.Contains "toolNameFromHookInput")
    check "arch: Opencode SessionLifecycleObserver uses selectMethodologiesFromHookArgs"
        (code.Contains "selectMethodologiesFromHookArgs")
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Opencode SessionLifecycleObserver must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))
    check "arch: Opencode SessionLifecycleObserver uses decodeHostEventEnvelope"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode SessionLifecycleObserver uses hookOutputString or setHookOutputString"
        (code.Contains "hookOutputString" || code.Contains "setHookOutputString")
    check "arch: Opencode SessionLifecycleObserver must not Dyn.get input event"
        (not (code.Contains "Dyn.get input \"event\""))

let opencodeEventHooksUsesEventEnvelopeCodec () =
    let code = requireFile "src/Opencode/EventHooks.fs" |> nonCommentCode
    check "arch: Opencode EventHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode EventHooks uses decodeHostEventEnvelope"
        (code.Contains "decodeHostEventEnvelope")
    check "arch: Opencode EventHooks uses getSessionID from NudgeEventCodec"
        (code.Contains "getSessionID")
    check "arch: Opencode EventHooks must not Dyn.str props sessionID"
        (not (code.Contains "Dyn.str props \"sessionID\""))
    check "arch: Opencode EventHooks must not inline Dyn.get event properties for stream-abort"
        (not (code.Contains "Dyn.get event \"properties\""))

let opencodeToolDefinitionHooksUsesHookInputCodec () =
    let code = requireFile "src/Opencode/ToolDefinitionHooks.fs" |> nonCommentCode
    check "arch: Opencode ToolDefinitionHooks opens OpencodeHookInputCodec"
        (code.Contains "OpencodeHookInputCodec")
    check "arch: Opencode ToolDefinitionHooks uses toolIdFromDefinitionHookInput"
        (code.Contains "toolIdFromDefinitionHookInput")
    check "arch: Opencode ToolDefinitionHooks must not Dyn.str input toolID"
        (not (code.Contains "Dyn.str input \"toolID\""))

let muxPluginToolExecuteAfterUsesMuxHookInputCodec () =
    let code = requireFile "src/Mux/Plugin.fs" |> nonCommentCode
    check "arch: Mux Plugin opens MuxHookInputCodec"
        (code.Contains "MuxHookInputCodec")
    check "arch: Mux Plugin toolExecuteAfter uses decodeMuxToolExecuteAfterInput"
        (code.Contains "decodeMuxToolExecuteAfterInput")
    check "arch: Mux Plugin toolExecuteAfter uses hookOutputErrorMux"
        (code.Contains "hookOutputErrorMux")
    check "arch: Mux Plugin toolExecuteAfter uses hookOutputTextMux"
        (code.Contains "hookOutputTextMux")
    check "arch: Mux Plugin toolExecuteAfter uses setHookOutputStringMux"
        (code.Contains "setHookOutputStringMux")
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input tool"
        (not (code.Contains "Dyn.str input \"tool\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input sessionID"
        (not (code.Contains "Dyn.str input \"sessionID\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input directory"
        (not (code.Contains "Dyn.str input \"directory\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str input workspaceId"
        (not (code.Contains "Dyn.str input \"workspaceId\""))
    check "arch: Mux Plugin toolExecuteAfter must not local setOutput"
        (not (code.Contains "let private setOutput"))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str output output"
        (not (code.Contains "Dyn.str output \"output\""))
    check "arch: Mux Plugin toolExecuteAfter must not Dyn.str output error"
        (not (code.Contains "Dyn.str output \"error\""))
    check "arch: Mux Plugin toolExecuteAfter must not direct write output output"
        (not (code.Contains "o?output <- v"))