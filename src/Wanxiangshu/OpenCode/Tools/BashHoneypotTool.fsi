namespace Wanxiangshu.OpenCode

open Wanxiangshu.Host

/// Coder-visible bash honeypot: no parameters, no shell, only a hard denial.
/// Host's real `bash` stays denied for every managed role (AGENT-007); this tool
/// exists so a Coder that still reaches for a shell gets an explicit scolding
/// instead of a successful execution path.
module BashHoneypotTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/bash-honeypot/description"


    val spec: ToolSpec
