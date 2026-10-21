namespace Wanxiangshu.Context.Prefix.OpenCode

/// Thin OpenCode adapter surface: suppression of host messages on the raw OpenCode wire.
module XWireHostSurface =

    val suppressHostMessagesByIds: rawMessages: obj array -> hostMessageIds: string array -> obj array
