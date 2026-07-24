namespace Wanxiangshu.Next.Tools

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process

module NodeFs =
    [<Import("readFileSync", "fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("writeFileSync", "fs")>]
    let writeFileSync (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("existsSync", "fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("statSync", "fs")>]
    let statSync (path: string) : obj = jsNative

module NodeProcess =
    [<Import("platform", "process")>]
    let platform: string = jsNative

module StaticTools =

    /// The only values accepted by the OpenCode reviewer tool.  Keep this
    /// parser deliberately independent of assistant text: a verdict is a tool
    /// argument, never something inferred from a transcript.
    let reviewerVerdictOfString (value: string) : Result<ReviewGuardVerdict, string> =
        match value with
        | "PERFECT" -> Ok ReviewGuardVerdict.Perfect
        | "REVISE" -> Ok ReviewGuardVerdict.Revise
        | _ -> Error "verdict must be exactly PERFECT or REVISE"

    let reviewerVerdictSchemaJson =
        """{"type":"object","properties":{"verdict":{"type":"string","enum":["PERFECT","REVISE"]}},"required":["verdict"],"additionalProperties":false}"""

    let managerAgentConfig () : obj =
        createObj
            [ "mode", box "primary"
              "permission",
              box (
                  createObj
                      [ "*", box "deny"
                        "fork", box "allow"
                        "join", box "allow"
                        "list", box "allow"
                        "verdict", box "deny" ]
              ) ]

    let coderAgentConfig () : obj =
        createObj
            [ "mode", box "primary"
              "permission",
              box (
                  createObj
                      [ "*", box "deny"
                        "read", box "allow"
                        "write", box "allow"
                        "edit", box "allow"
                        "glob", box "allow"
                        "grep", box "allow"
                        "verdict", box "deny" ]
              ) ]

    let reviewerAgentConfig () : obj =
        createObj
            [ "mode", box "primary"
              "permission",
              box (
                  createObj
                      [ "*", box "deny"
                        "read", box "allow"
                        "glob", box "allow"
                        "grep", box "allow"
                        "inspector", box "allow"
                        "verdict", box "allow" ]
              ) ]

    let toollessAgentConfig () : obj =
        createObj [ "mode", box "primary"; "permission", box (createObj [ "*", box "deny" ]) ]

    let inspectorAgentConfig () : obj =
        createObj
            [ "mode", box "primary"
              "permission", box (createObj [ "*", box "deny"; "executor", box "allow" ]) ]

    let executorTool () : Tool =
        { Name = "executor"
          Description = "Execute shell command within timeout budget."
          SchemaJson = """{"type":"object","properties":{"command":{"type":"string"}},"required":["command"]}"""
          Execute =
            fun ctx input ->
                task {
                    ctx.Cancellation.ThrowIfCancellationRequested()

                    let cmdText =
                        try
                            let decoder = Decode.field "command" Decode.string

                            match Decode.fromString decoder input.Payload with
                            | Ok s -> s
                            | Error _ ->
                                match Decode.Auto.fromString<string> input.Payload with
                                | Ok s -> s
                                | Error _ -> input.Payload
                        with _ ->
                            input.Payload

                    let isWindows = NodeProcess.platform = "win32"
                    let fileName = if isWindows then "cmd.exe" else "sh"
                    let argFlag = if isWindows then "/c" else "-c"

                    let cmd: Command =
                        { FileName = fileName
                          Arguments = [ argFlag; cmdText ]
                          WorkingDirectory = None
                          Environment = None
                          Stdin = None
                          Deadline = None
                          PtyOptions = None }

                    let procCtx: ProcessContext =
                        { WorkingDirectory = None
                          DefaultTimeout = None }

                    let estimate: ProcessEstimate =
                        { EstimatedRuntime = RuntimeSeconds 30.0
                          EstimatedOutput = OutputBytes 200000L
                          EstimatedMemory = EstimatedMemory.Medium }

                    let! res = Runner.execute cmd estimate procCtx ctx.Cancellation

                    match res with
                    | Ok(RunnerOutcome.Completed(code, stdout, stderr, _)) ->
                        return
                            { Result = sprintf "Exit: %d\nStdout: %s\nStderr: %s" code stdout stderr
                              Truncated = false }
                    | Ok(RunnerOutcome.Spooled(code, path, totalBytes, chunks, _)) ->
                        return
                            { Result = sprintf "Exit: %d\nSpool: %s\nBytes: %d\nChunks: %d" code path totalBytes chunks
                              Truncated = false }
                    | Ok(RunnerOutcome.OutputExceeded(bytes, path)) ->
                        return
                            { Result = sprintf "Output exceeded budget: %d (%A)" bytes path
                              Truncated = true }
                    | Error err ->
                        return
                            { Result = sprintf "Error: %A" err
                              Truncated = false }
                } }
