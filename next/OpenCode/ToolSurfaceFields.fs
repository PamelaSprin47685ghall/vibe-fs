namespace Wanxiangshu.Next.OpenCode

/// Field/Kind literal constants for the fork/list tool surface. Split out of
/// ToolSurface.fs so that file stays within the architecture line gate.
module ToolSurfaceFields =
    [<RequireQualifiedAccess>]
    module ForkField =
        [<Literal>]
        let Agent = "agent"

        [<Literal>]
        let Prompt = "prompt"

        [<Literal>]
        let Signal = "signal"

    [<RequireQualifiedAccess>]
    module ListKind =
        [<Literal>]
        let Agent = "agent"

        [<Literal>]
        let Pty = "pty"
