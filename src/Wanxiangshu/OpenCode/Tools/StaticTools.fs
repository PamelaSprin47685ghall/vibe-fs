namespace Wanxiangshu.OpenCode

open System
open Fable.Core.JsInterop
open Wanxiangshu.Ablation
open Wanxiangshu.Foundation


module StaticTools =

    /// capability-enforcement-012: permission → provider verb names. The single
    /// map lives in wanxiangli/capability (via OfficeCapability); an empty list
    /// means the duty lives on the programming surface only (this.devops).
    let toolNames (p: ToolPermission) : string list = OfficeCapability.toolNames p

    /// capability-enforcement-012: the sole reverse lookup for tool name →
    /// permission, answered by wanxiangli/capability. The schema projection, the
    /// dispatch tool map and the execute gate all read this. A verb shared by two
    /// permissions resolves to the one this role owns.
    let permissionOfToolName (role: Role) (name: string) : ToolPermission option =
        OfficeCapability.permissionOfToolName role name

    /// The office projection of one name: true only when the role's permission
    /// set owns the permission this exact name maps to.
    let admitsToolForRole (role: Role) (name: string) : bool =
        OfficeCapability.admitsToolForRole role name


    /// JS-001: the generated todowrite-for-ROLE tool name for a role.
    let jsToolName (role: Role) : string =
        "todowrite-for-" + (string role).ToLowerInvariant()

    /// Shared office boundary for lightweight cognitive utilities (`assume`) so
    /// schema visibility and execution admission cannot drift.
    let cognitiveUtilityRoleAllowed (role: Role) : bool = role <> Role.Distiller

    /// capability-enforcement-006/007: a Host-declared external (MCP) tool
    /// namespace is not an office capability, so it never enters
    /// `Roles.permissions` (capability-enforcement-006) and no per-source
    /// permission table is kept for it (capability-enforcement-007). It follows
    /// the Host utility surface (`skill`) instead: every projected office role
    /// keeps it.
    let hostExternalNamespaceAllowedForRole (role: Role) : bool = true

    /// JS-001: a role whose capability set includes any programming-surface
    /// permission gets its todowrite-for-* tool allowed in the permission
    /// matrix. Fork/Join count: the Orchestrator's only programming surface is
    /// delegation (this.fork/this.join).
    let private hasFsCapability (role: Role) : bool =
        let fsPermissions =
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.Fork
                  ToolPermission.Join ]

        Set.intersect (OfficeCapability.permissions role) fsPermissions
        |> Set.isEmpty
        |> not

    /// Single source: OfficeCapability.permissions → OpenCode agent permission object.
    /// Emits explicit allow/deny for the full known tool name set so host schema
    /// filters and contract tests see concrete denies (not only "*").
    /// The catalog holds exactly the host provider tool names. `handoff` is
    /// fully retired. `devops` left the catalog with its ToolSpec: the host
    /// devops tool is fully retired, and the verb survives only as a
    /// programming-surface member (this.devops) projected by `Capability.fs`.
    /// `fork` / `resume` / `commission` stay in the catalog because their
    /// ToolSpec constructors are retained — they are no longer registered as
    /// host provider tools (the delegation line runs on the programming
    /// surface), yet every ToolSpec owner name must appear here
    /// (tool-referential-integrity). Their permission entries are therefore
    /// harmless: they match no registered tool.
    let knownToolNames =
        [ "fork"
          "resume"
          "commission"
          "join"
          "horizon"
          "fission"
          "skill"
          "assume"
          "bash-honeypot"
          "review"
          "fetch"
          "deliver"
          "todowrite-for-engineer"
          "todowrite-for-manager"
          "todowrite-for-orchestrator"
          "todowrite-for-devops"
          "todowrite-for-bookkeeper"
          "todowrite-for-plan" ]

    /// PROMPT-012: an explicit complete allow/deny map for PromptInput.tools.
    /// The office half comes from the one reverse lookup; utility tools keep
    /// their own predicates.
    let requestToolMap (role: Role) (allowed: Set<ToolPermission>) : Map<string, bool> =
        let registry = AblationGate.registry ()

        knownToolNames
        |> List.map (fun name ->
            name,
            (name = "skill"
             || name = "assume"
             || (permissionOfToolName role name
                 |> Option.exists (fun permission -> Set.contains permission allowed))))
        |> Map.ofList
        |> AblationGate.filterToolPermissionMap registry

    let private defaultPermission (allowed: Set<ToolPermission>) (role: Role) name =
        let admitted =
            permissionOfToolName role name
            |> Option.exists (fun permission -> Set.contains permission allowed)

        if admitted then "allow" else "deny"

    let private jsPermission role name =
        if name = "todowrite-for-manager" && role = Role.Manager then
            "allow"
        elif name = "todowrite-for-plan" && role = Role.Plan then
            "allow"
        elif name = jsToolName role && hasFsCapability role then
            "allow"
        else
            "deny"

    let private permissionFor (registry: AblationRegistry) allowed role name =
        match not (AblationGate.toolSchemaDenied registry name), name, role with
        | false, _, _ -> "deny"
        // Host-registration exceptions the office matrix cannot express: the
        // Orchestrator's Fork duty runs on the programming surface, and its
        // host verb is `commission` (no office permission of its own).
        | true, "fork", Role.Orchestrator -> "deny"
        | true, "commission", Role.Orchestrator -> "allow"
        | true, "skill", _ -> "allow"
        | true, "assume", role when not (cognitiveUtilityRoleAllowed role) -> "deny"
        | true, "assume", _ -> "allow"
        | true, "todowrite-for-bookkeeper", _ -> "deny"
        | true, name, _ when name.StartsWith "todowrite-for-" -> jsPermission role name
        | true, _, _ -> defaultPermission allowed role name

    let permissionObj (role: Role) : obj =
        let registry = AblationGate.registry ()
        let allowed = OfficeCapability.permissions role

        // Host defaults set external_directory:* = ask (agent.ts). Rulesets merge by
        // flat concat + findLast, so this trailing allow cancels the Host ask and
        // stops permission.asked prompts on paths outside the project directory.
        let pairs =
            [ yield "*", box "deny"
              yield "external_directory", box "allow"
              for name in knownToolNames do
                  yield name, box (permissionFor registry allowed role name) ]

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

    let managerAgentConfig (prompt: string option) : obj = primaryAgent Role.Manager prompt

    let orchestratorAgentConfig (prompt: string option) : obj = primaryAgent Role.Orchestrator prompt

    let engineerAgentConfig (prompt: string option) : obj = primaryAgent Role.Engineer prompt



    /// InternalLeaf Bookkeeper Host stub (AGENT-002): hidden; ToolRegistry gates todowrite-for-bookkeeper by attachment.
    let bookkeeperAgentConfig (prompt: string) : obj =
        let pairs =
            [ yield "*", box "deny"
              yield "external_directory", box "allow"
              for name in knownToolNames do
                  yield name, box "deny" ]

        createObj
            [ "mode", box "primary"
              "hidden", box true
              "permission", box (createObj pairs)
              "prompt", box prompt
              "temperature", box 1.0
              "options", box (createObj [ "temperature", box 1.0 ]) ]

    let devopsAgentConfig (prompt: string option) : obj = primaryAgent Role.DevOps prompt

    let planAgentConfig (prompt: string option) : obj = primaryAgent Role.Plan prompt

    /// The advisor is an internal reviewer runtime: hidden, no tools, prose out.
    let advisorAgentConfig (prompt: string) : obj = hiddenAgent Role.Advisor prompt
