namespace Wanxiangshu.OpenCode

open Wanxiangshu.Sphinx
open Wanxiangshu.Composition.Durable

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Mission.Review
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

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

    open Wanxiangshu.Foundation

    /// One permission may expand to several provider verb names (Pty, Behavior, Exec).
    let toolNames (p: ToolPermission) : string list =
        match p with
        | ToolPermission.Fork -> [ "fork" ]
        | ToolPermission.Join -> [ "join" ]
        | ToolPermission.Horizon -> [ "horizon" ]
        | ToolPermission.TodoWrite -> [ "todowrite" ]
        | ToolPermission.Fission -> [ "fission" ]
        | ToolPermission.Read -> [ "read" ]
        | ToolPermission.Write -> [ "write" ]
        | ToolPermission.Edit -> [ "edit" ]
        | ToolPermission.Glob -> [ "glob" ]
        | ToolPermission.Grep -> [ "grep" ]
        | ToolPermission.Move -> [ "mv" ]
        | ToolPermission.Remove -> [ "rm" ]
        | ToolPermission.BashHoneypot -> [ "bash-honeypot" ]
        | ToolPermission.Inspect -> [ "inspect" ]
        | ToolPermission.Behavior -> [ "establish-behavior"; "repair-behavior" ]
        | ToolPermission.Exec -> [ "run"; "query-shell" ]
        | ToolPermission.Pty -> [ "open-terminal"; "send-terminal"; "read-terminal"; "signal-terminal" ]
        | ToolPermission.Network -> [ StealthBrowserMcp.permissionKey ]
        | ToolPermission.Sphinx -> [ SphinxMcp.permissionKey ]
        | ToolPermission.Judge -> [ "judge" ]
        | ToolPermission.Chronicle -> [ "chronicle" ]
        | ToolPermission.Fetch -> [ "fetch" ]
        | ToolPermission.Finality -> [ "suicide" ]

    /// Primary name for permissions with a single verb (tests / simple maps).
    let toolName (p: ToolPermission) =
        match toolNames p with
        | name :: _ -> name
        | [] -> invalidOp "ToolPermission must expand to at least one provider name"

    /// JS-001: the generated js-ROLE tool name for a role.
    let jsToolName (role: Role) : string =
        "js-" + (string role).ToLowerInvariant()

    /// JS-001: a role whose capability set includes any filesystem permission
    /// gets its js-* tool allowed in the permission matrix.
    let private hasFsCapability (role: Role) : bool =
        let fsPermissions =
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep ]

        Set.intersect (Roles.permissions role) fsPermissions |> Set.isEmpty |> not

    /// Single source: Kernel.Roles.permissions → OpenCode agent permission object.
    /// Emits explicit allow/deny for the full known tool name set so host schema
    /// filters and contract tests see concrete denies (not only "*").
    let knownToolNames =
        [ "fork"
          "commission"
          "open-terminal"
          "send-terminal"
          "read-terminal"
          "signal-terminal"
          "join"
          "horizon"
          "todowrite"
          "fission"
          "read"
          "write"
          "edit"
          "glob"
          "grep"
          "skill"
          "mv"
          "rm"
          "bash-honeypot"
          "inspect"
          "establish-behavior"
          "repair-behavior"
          "run"
          "query-shell"
          StealthBrowserMcp.permissionKey
          SphinxMcp.permissionKey
          "judge"
          "chronicle"
          "fetch"
          "suicide"
          "js-manager"
          "js-orchestrator"
          "js-coder"
          "js-inspector"
          "js-browser"
          "js-inquiry"
          "js-reviewer"
          "js-devops"
          "js-distiller"
          "js-blogger"
          "js-bookkeeper" ]

    let private namesForPermissions (allowed: Set<ToolPermission>) : Set<string> =
        allowed |> Set.toList |> List.collect toolNames |> Set.ofList

    /// PROMPT-012: an explicit complete allow/deny map for PromptInput.tools.
    let requestToolMap (allowed: Set<ToolPermission>) : Map<string, bool> =
        let allowedNames = namesForPermissions allowed

        knownToolNames
        |> List.map (fun name -> name, name = "skill" || Set.contains name allowedNames)
        |> Map.ofList

    let private defaultPermission allowed name =
        if Set.contains name allowed then "allow" else "deny"

    let private jsPermission role name =
        if name = jsToolName role && hasFsCapability role then
            "allow"
        else
            "deny"

    let private permissionFor allowed role name =
        match name, role with
        | "commission", Role.Manager -> "deny"
        | "fork", Role.Orchestrator -> "deny"
        | "commission", Role.Orchestrator -> "allow"
        | "open-terminal", Role.DevOps
        | "send-terminal", Role.DevOps
        | "read-terminal", Role.DevOps
        | "signal-terminal", Role.DevOps -> "allow"
        | "fork", Role.DevOps -> "deny"
        | "query-shell", Role.Inspector -> "allow"
        | "run", Role.Inspector -> "deny"
        | "run", Role.DevOps -> "allow"
        | "query-shell", Role.DevOps -> "deny"
        | "write", Role.DevOps
        | "edit", Role.DevOps -> "deny"
        | "skill", _ -> "allow"
        | "js-bookkeeper", _ -> "deny"
        | name, _ when name.StartsWith "js-" -> jsPermission role name
        | _ -> defaultPermission allowed name

    let permissionObj (role: Role) : obj =
        let allowed = Roles.permissions role |> namesForPermissions

        // Host defaults set external_directory:* = ask (agent.ts). Rulesets merge by
        // flat concat + findLast, so this trailing allow cancels the Host ask and
        // stops permission.asked prompts on paths outside the project directory.
        let pairs =
            [ yield "*", box "deny"
              yield "external_directory", box "allow"
              for name in knownToolNames do
                  yield name, box (permissionFor allowed role name) ]

        createObj pairs

    /// OpenCode AgentConfig: mode + permission + optional system prompt.
    /// `prompt` is the host agent system prompt, never a user message body.
    let private primaryAgent (role: Role) (systemPrompt: string option) : obj =
        match systemPrompt with
        | Some text when not (String.IsNullOrWhiteSpace text) ->
            createObj
                [ "mode", box "primary"
                  "permission", permissionObj role
                  "prompt", box text
                  "temperature", box 1.0
                  "options", box (createObj [ "temperature", box 1.0 ]) ]
        | _ ->
            createObj
                [ "mode", box "primary"
                  "permission", permissionObj role
                  "temperature", box 1.0
                  "options", box (createObj [ "temperature", box 1.0 ]) ]

    let private hiddenAgent (role: Role) (systemPrompt: string) : obj =
        createObj
            [ "mode", box "primary"
              "hidden", box true
              "permission", permissionObj role
              "prompt", box systemPrompt
              "temperature", box 1.0
              "options", box (createObj [ "temperature", box 1.0 ]) ]

    /// The only values accepted by the OpenCode judge tool.  Keep this
    /// parser deliberately independent of assistant text: a verdict is a tool
    /// argument, never something inferred from a transcript.
    let reviewerVerdictOfString (value: string) : Result<ReviewGuardVerdict, string> =
        match value with
        | "PERFECT" -> Ok ReviewGuardVerdict.Perfect
        | "REVISE" -> Ok ReviewGuardVerdict.Revise
        | _ -> Error "verdict must be exactly PERFECT or REVISE"

    let reviewerVerdictSchemaJson =
        """{"type":"object","properties":{"verdict":{"type":"string","enum":["PERFECT","REVISE"]}},"required":["verdict"],"additionalProperties":false}"""

    let managerAgentConfig (prompt: string option) : obj = primaryAgent Role.Manager prompt

    let orchestratorAgentConfig (prompt: string option) : obj = primaryAgent Role.Orchestrator prompt

    let coderAgentConfig (prompt: string option) : obj = primaryAgent Role.Coder prompt

    let reviewerAgentConfig (prompt: string option) : obj = primaryAgent Role.Reviewer prompt

    /// Companion Session Y: tool set is exactly { chronicle } (ENFORCER-010).
    /// System prompt for B-record distillation with chronicle tool protocol.
    let bloggerAgentConfig (prompt: string) : obj = hiddenAgent Role.Blogger prompt

    /// Role.Distiller: no tools; system prompt for map/reduce output summarization.
    /// Distinct from Tool.run (OS command tool used by DevOps).
    let distillerAgentConfig (prompt: string) : obj = hiddenAgent Role.Distiller prompt

    let inquiryAgentConfig (prompt: string option) : obj = primaryAgent Role.Inquiry prompt

    /// InternalLeaf Bookkeeper Host stub (AGENT-002): hidden; ToolRegistry gates js-bookkeeper by attachment.
    let bookkeeperAgentConfig (prompt: string) : obj =
        createObj
            [ "mode", box "primary"
              "hidden", box true
              "permission", permissionObj Role.Distiller
              "prompt", box prompt
              "temperature", box 1.0
              "options", box (createObj [ "temperature", box 1.0 ]) ]

    let browserAgentConfig (prompt: string option) : obj = primaryAgent Role.Browser prompt

    let inspectorAgentConfig (prompt: string option) : obj = primaryAgent Role.Inspector prompt

    let devopsAgentConfig (prompt: string option) : obj = primaryAgent Role.DevOps prompt
