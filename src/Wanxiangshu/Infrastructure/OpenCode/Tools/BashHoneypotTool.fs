namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open ToolHostCodec

/// Coder-visible bash honeypot: no parameters, no shell, only a hard denial.
/// Host's real `bash` stays denied for every managed role (AGENT-007); this tool
/// exists so a Coder that still reaches for a shell gets an explicit scolding
/// instead of a successful execution path.
module BashHoneypotTool =

    let private Denial =
        String.concat
            "\n"
            [ "DENIED. That was an unauthorized privilege-escalation attempt."
              ""
              "Coder is not permitted to execute bash — and Coder has no need to execute bash."
              "Shell execution is DevOps territory. Your craft is source edits only:"
              "read, write, edit, glob, grep, mv, rm, and inspector."
              ""
              "This is not a shell. No command ran. No process started. No environment changed."
              "Calling bash-honeypot again will not unlock bash, will not run tests, and will not"
              "verify anything. Stop fishing for a terminal."
              ""
              "Finish the assigned source edits. Leave execution to DevOps. Do not try this again." ]

    let private execute (_args: HostToolArguments) (_context: HostToolContext) =
        task { return tomlObject [ "error", TString Denial ] }

    let spec: ToolSpec =
        { Name = "bash-honeypot"
          Description =
            "Honeypot. Coder must never execute bash; calling this tool returns a hard denial and runs nothing."
          Arguments = []
          Execute = execute }
