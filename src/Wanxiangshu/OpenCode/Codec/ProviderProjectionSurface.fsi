namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Participant.Provider.Projection

/// JS-native provider projection codec owner surface.
/// Raw Host objects are decoded here; tests never import internal codec modules
/// or observe Fable unions and records directly.
module ProviderProjectionSurface =
    val decodeWirePart: raw: obj -> obj
    val decodeWireParts: raw: obj array -> obj array
    val decodeMessage: raw: obj -> obj
    val decodeRequest: raw: obj -> obj
    val decodeMessageView: raw: obj array -> obj
    val decodeCapturedMessage: raw: obj -> obj
    val decodeHostPart: raw: obj -> obj
    val decodeHostParts: raw: obj array -> obj array
    val decodeIngress: input: obj -> output: obj -> obj

    val opencodeModel: providerId: string -> modelId: string -> variant: obj -> obj
    val opencodeTextPart: id: string -> kind: string -> text: string -> synthetic: bool -> obj
    val opencodeToolCallPart: id: string -> kind: string -> callId: string -> tool: string -> args: obj -> obj

    val opencodeCompactionPart: id: string -> kind: string -> auto: bool -> overflow: bool -> obj

    val opencodeUserMessage:
        id: string -> role: string -> sessionId: string -> agent: obj -> model: obj -> parts: obj array -> obj

    val opencodeAssistantMessage:
        id: string ->
        parentId: obj ->
        role: string ->
        sessionId: string ->
        agent: obj ->
        providerId: obj ->
        modelId: obj ->
        summary: bool ->
        error: obj ->
        parts: obj array ->
            obj

    val opencodeHookInput: sessionId: string -> messageId: string -> agent: string -> model: obj -> obj

    val opencodeToolExecuteInput: tool: string -> sessionId: string -> callId: string -> obj
    val opencodeToolExecuteOutput: args: obj -> obj

    val promptOriginOfMessage: raw: obj -> obj
    val semanticTurnOfHostMessageId: messageId: string -> raw: obj array -> obj

    val tryApplyRenderedMessages: sessionId: string -> sha256: (string -> string) -> rendered: obj -> obj

    val tryApplyRenderedInsertionsPreservingBase:
        sessionId: string -> sha256: (string -> string) -> rawMessages: obj array -> rendered: obj -> obj

    val messagesFromTransformOutput: output: obj -> obj array
    val hostMessageId: raw: obj -> string
    val projectionSessionIdFromMessages: raw: obj -> string
    val lastUserMessageId: raw: obj array -> string
    val lastUserPromptKey: raw: obj array -> string
    val formalText: raw: obj -> string
