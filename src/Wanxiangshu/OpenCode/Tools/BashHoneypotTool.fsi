namespace Wanxiangshu.OpenCode

/// Coder-visible bash honeypot: no parameters, no shell, only a hard denial.
/// Host's real `bash` stays denied for every managed role (AGENT-007); this tool
/// exists so a Coder that still reaches for a shell gets an explicit scolding
/// instead of a successful execution path.
module BashHoneypotTool =

    [<RequireQualifiedAccess>]
    module Path =
        [<Literal>]
        val Description: string = "tool/bash-honeypot/description"

        [<Literal>]
        val Denial: string = "tool/bash-honeypot/denial"

    val admission: ToolAdmission

    val spec: ToolSpec
