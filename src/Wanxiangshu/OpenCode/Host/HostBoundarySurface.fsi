namespace Wanxiangshu.OpenCode

module HostBoundarySurface =
    val sanitizeMessage: raw: obj -> obj
    val sanitizeMessages: raw: obj array -> obj array
    val roleOf: agent: string -> string
    val sessionContext: raw: obj -> obj
    val locateToolCall: toolCallId: string -> rawMessages: obj array -> obj
