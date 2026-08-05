namespace Wanxiangshu.Tools

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Infrastructure.Resources
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process

module NodeFs =
    [<Import("readFileSync", "fs")>]
    let readFileSync (path: string, encoding: string) : string = jsNative

    [<Import("writeFileSync", "fs")>]
    let writeFileSync (path: string, data: string, encoding: string) : unit = jsNative

    [<Import("existsSync", "fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("statSync", "fs")>]
    let statSync (path: string) : obj = jsNative

    [<Import("readdirSync", "fs")>]
    let readdirSync (path: string) : obj = jsNative

    [<Import("renameSync", "fs")>]
    let renameSync (source: string, destination: string) : unit = jsNative

    [<Import("rmSync", "fs")>]
    let rmSync (path: string, options: obj) : unit = jsNative

module NodeProcess =
    [<Import("platform", "process")>]
    let platform: string = jsNative

module StaticTools =

    open Wanxiangshu.Kernel

    let private toolName (p: ToolPermission) =
        match p with
        | ToolPermission.Fork -> "fork"
        | ToolPermission.Join -> "join"
        | ToolPermission.List -> "list"
        | ToolPermission.Read -> "read"
        | ToolPermission.Write -> "write"
        | ToolPermission.Edit -> "edit"
        | ToolPermission.Glob -> "glob"
        | ToolPermission.Grep -> "grep"
        | ToolPermission.Move -> "mv"
        | ToolPermission.Remove -> "rm"
        | ToolPermission.Inspector -> "inspector"
        | ToolPermission.Coder -> "coder"
        | ToolPermission.Exec -> "executor"
        | ToolPermission.Pty -> "fork-pty"
        | ToolPermission.Network -> "network"
        | ToolPermission.Verdict -> "verdict"
        | ToolPermission.Blog -> "blog"

    /// Single source: Kernel.Roles.permissions → OpenCode agent permission object.
    /// Emits explicit allow/deny for the full known tool name set so host schema
    /// filters and contract tests see concrete denies (not only "*").
    let permissionObj (role: Role) : obj =
        let allowed = Roles.permissions role |> Set.map toolName

        let known =
            [ "fork"
              "fork-manager"
              "fork-pty"
              "join"
              "list"
              "read"
              "write"
              "edit"
              "glob"
              "grep"
              "mv"
              "rm"
              "inspector"
              "coder"
              "executor"
              "network"
              "verdict"
              "blog" ]

        let pairs =
            [ yield "*", box "deny"
              for name in known do
                  match name, role with
                  // Manager owns "fork"; must not see Orchestrator's narrow tool.
                  | "fork-manager", Role.Manager -> yield name, box "deny"
                  | "fork", Role.Orchestrator -> yield name, box "deny"
                  // Orchestrator owns "fork-manager" (maps from ToolPermission.Fork).
                  | "fork-manager", Role.Orchestrator -> yield name, box "allow"
                  // DevOps owns "fork-pty"; Manager/others never see PTY through fork.
                  | "fork-pty", Role.DevOps -> yield name, box "allow"
                  | "fork", Role.DevOps -> yield name, box "deny"
                  // DevOps may inspect and delegate edits, never write/edit directly.
                  | "write", Role.DevOps
                  | "edit", Role.DevOps -> yield name, box "deny"
                  | _ -> yield name, box (if Set.contains name allowed then "allow" else "deny") ]

        createObj pairs

    /// OpenCode AgentConfig: mode + permission + optional system prompt.
    /// `prompt` is the host agent system prompt, never a user message body.
    let private primaryAgent (role: Role) (systemPrompt: string option) : obj =
        match systemPrompt with
        | Some text when not (String.IsNullOrWhiteSpace text) ->
            createObj [ "mode", box "primary"; "permission", permissionObj role; "prompt", box text ]
        | _ -> createObj [ "mode", box "primary"; "permission", permissionObj role ]

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

    let private prompts () = RuntimeResources.current().Prompts

    let managerAgentConfig () : obj =
        primaryAgent Role.Manager (Some (prompts ()).ManagerSystemPrompt)

    let orchestratorAgentConfig () : obj =
        primaryAgent Role.Orchestrator (Some (prompts ()).OrchestratorSystemPrompt)

    let coderAgentConfig () : obj =
        primaryAgent Role.Coder (Some (prompts ()).CoderSystemPrompt)

    let reviewerAgentConfig () : obj =
        primaryAgent Role.Reviewer (Some (prompts ()).ReviewerSystemPrompt)

    /// Companion Session Y: tool set is exactly { blog } (ENFORCER-010).
    /// System prompt for B-record distillation with blog tool protocol.
    let bloggerAgentConfig () : obj =
        primaryAgent Role.Blogger (Some (prompts ()).BloggerSystemPrompt)

    /// AgentRole.Executor: no tools; system prompt for map/reduce output summarization.
    /// Distinct from Tool.executor (OS command tool used by Inspector/DevOps).
    let executorAgentConfig () : obj =
        primaryAgent Role.Executor (Some (prompts ()).ExecutorSystemPrompt)

    let meditatorAgentConfig () : obj =
        primaryAgent Role.Meditator (Some (prompts ()).MeditatorSystemPrompt)

    let browserAgentConfig () : obj =
        primaryAgent Role.Browser (Some (prompts ()).BrowserSystemPrompt)

    let inspectorAgentConfig () : obj =
        primaryAgent Role.Inspector (Some (prompts ()).InspectorSystemPrompt)

    let devopsAgentConfig () : obj =
        primaryAgent Role.DevOps (Some (prompts ()).DevopsSystemPrompt)

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
                          HardLimit = ProcessEstimate.DefaultHardLimit }

                    let estimate: ProcessEstimate =
                        { EstimatedRuntime = RuntimeSeconds 30.0
                          EstimatedOutput = OutputBytes 200000L
                          EstimatedMemory = EstimatedMemory.Medium }

                    let! res = ProcessRunner.run cmd estimate procCtx ctx.Cancellation

                    match res with
                    | Ok(ProcessOutcome.Completed(code, stdout, stderr, _)) ->
                        return
                            { Result = sprintf "Exit: %d\nStdout: %s\nStderr: %s" code stdout stderr
                              Truncated = false }
                    | Ok(ProcessOutcome.Spooled(code, path, totalBytes, chunks)) ->
                        return
                            { Result = sprintf "Exit: %d\nSpool: %s\nBytes: %d\nChunks: %d" code path totalBytes chunks
                              Truncated = false }
                    | Error err ->
                        return
                            { Result = sprintf "Error: %A" err
                              Truncated = false }
                } }
