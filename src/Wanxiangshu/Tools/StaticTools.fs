namespace Wanxiangshu.Tools

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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

module StaticTools =

    open Wanxiangshu.Kernel

    let toolName (p: ToolPermission) =
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
        | ToolPermission.Teacher -> "teacher"
        | ToolPermission.Return -> "return"
        | ToolPermission.Finality -> "suicide"

    /// Single source: Kernel.Roles.permissions → OpenCode agent permission object.
    /// Emits explicit allow/deny for the full known tool name set so host schema
    /// filters and contract tests see concrete denies (not only "*").
    let knownToolNames =
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
          "blog"
          "teacher"
          "return"
          "suicide" ]

    /// PROMPT-012: an explicit complete allow/deny map for PromptInput.tools.
    let requestToolMap (allowed: Set<ToolPermission>) : Map<string, bool> =
        let allowedNames = allowed |> Set.map toolName

        knownToolNames
        |> List.map (fun name -> name, Set.contains name allowedNames)
        |> Map.ofList

    let permissionObj (role: Role) : obj =
        let allowed = Roles.permissions role |> Set.map toolName

        // Host defaults set external_directory:* = ask (agent.ts). Rulesets merge by
        // flat concat + findLast, so this trailing allow cancels the Host ask and
        // stops permission.asked prompts on paths outside the project directory.
        let pairs =
            [ yield "*", box "deny"
              yield "external_directory", box "allow"
              for name in knownToolNames do
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

    let private hiddenAgent (role: Role) (systemPrompt: string) : obj =
        createObj
            [ "mode", box "primary"
              "hidden", box true
              "permission", permissionObj role
              "prompt", box systemPrompt ]

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

    let managerAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Manager prompt

    let orchestratorAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Orchestrator prompt

    let coderAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Coder prompt

    let reviewerAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Reviewer prompt

    /// Companion Session Y: tool set is exactly { blog } (ENFORCER-010).
    /// System prompt for B-record distillation with blog tool protocol.
    let bloggerAgentConfig (prompt: string) : obj =
        hiddenAgent Role.Blogger prompt

    /// Role.Executor: no tools; system prompt for map/reduce output summarization.
    /// Distinct from Tool.executor (OS command tool used by Inspector/DevOps).
    let executorAgentConfig (prompt: string) : obj =
        hiddenAgent Role.Executor prompt

    let studentAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Student prompt

    let teacherAgentConfig (prompt: string) : obj =
        hiddenAgent Role.Teacher prompt

    let meditatorAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Meditator prompt

    let browserAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Browser prompt

    let inspectorAgentConfig (prompt: string option) : obj =
        primaryAgent Role.Inspector prompt

    let devopsAgentConfig (prompt: string option) : obj =
        primaryAgent Role.DevOps prompt
