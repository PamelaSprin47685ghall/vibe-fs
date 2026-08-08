namespace Wanxiangshu.Kernel

[<RequireQualifiedAccess>]
type AgentTier =
    | Fast
    | Deep

/// DSL-class: Vocabulary — the fixed set of managed agent roles (one
/// vocabulary, no control-flow reading).
[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Meditator
    | Reviewer
    | DevOps
    | Student
    | Teacher
    | Executor
    | Blogger

/// DSL-class: Vocabulary — the fixed tool-permission catalog keyed by Role.
[<RequireQualifiedAccess>]
type ToolPermission =
    | Fork
    | Join
    | List
    | Read
    | Write
    | Edit
    | Glob
    | Grep
    | Move
    | Remove
    | Inspector
    | Coder
    | Exec
    | Pty
    | Network
    | Verdict
    | Blog
    /// AGENT-020: Student learning request's only tool.
    | Teacher
    /// AGENT-020: Teacher response and Student compilation terminal tool.
    | Return
    /// GLORY-036: the Manager's own end-of-life tool (`suicide`).
    | Finality

module Roles =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager ->
            set
                [ ToolPermission.Fork
                  ToolPermission.Join
                  ToolPermission.List
                  ToolPermission.Finality ]
        | Role.Orchestrator -> set [ ToolPermission.Fork; ToolPermission.Join ]
        | Role.Coder ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.Inspector ]
        | Role.Inspector ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Exec ]
        | Role.Browser ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Network ]
        | Role.Meditator ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]
        | Role.Reviewer ->
            set
                [ ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Verdict ]
        | Role.DevOps ->
            set
                [ ToolPermission.Pty
                  ToolPermission.Exec
                  ToolPermission.Join
                  ToolPermission.List
                  ToolPermission.Read
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector
                  ToolPermission.Coder ]
        // AGENT-020: static HumanRoot face. StudentCompile overrides the whole
        // request permission set from AttemptExecutionProfile.RequestKind.
        | Role.Student -> set [ ToolPermission.Teacher ]
        | Role.Teacher ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Move
                  ToolPermission.Remove
                  ToolPermission.Inspector
                  ToolPermission.Coder
                  ToolPermission.Exec
                  ToolPermission.Network
                  ToolPermission.Return ]
        | Role.Executor -> Set.empty
        // ENFORCER-010: Blogger's tool set is exactly { blog }.
        | Role.Blogger -> set [ ToolPermission.Blog ]

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        permissions role |> Set.contains permission


/// RoleDefinition: combines tool permissions with system prompts for each agent role.
/// No `Companion` flag, and no role-keyed Companion predicate anywhere. COMPANION-001
/// gives every managed work session a Y regardless of role, so a role is not an input
/// to that decision — `SessionAssociationProjection.isCompanion` answers it from the
/// durable Session kind (HOST-008) instead.
type RoleDefinition =
    { Role: Role
      Prompt: string
      Tools: ToolPermission Set }

module RoleDefinitions =

    /// Full Manager system prompt lives in prompts/manager-system.md and is
    /// loaded into OpenCode AgentConfig.prompt (host system prompt). Keep this
    /// domain stub short so Kernel stays free of filesystem I/O.
    let managerPrompt =
        "Manager system prompt SSOT: prompts/manager-system.md\n"
        + "Tools: fork / join / list / suicide.\n"
        + "Manager owns verification. Coder edits then stops. DevOps executes and owns operational closure for mechanical repair objectives."

    /// Full Coder system prompt lives in prompts/coder-system.md and is loaded
    /// into OpenCode AgentConfig.prompt (host system prompt).
    let coderPrompt =
        "Coder system prompt SSOT: prompts/coder-system.md\n"
        + "Tools: read / write / edit / glob / grep / inspector / mv / rm.\n"
        + "Coder edits then stops. Inspector is only for narrow static facts; never compile, test, diagnose failures, or delegate verification."

    /// Full Inspector system prompt lives in prompts/inspector-system.md and is
    /// loaded into OpenCode AgentConfig.prompt (host system prompt).
    let inspectorPrompt =
        "Inspector system prompt SSOT: prompts/inspector-system.md\n"
        + "Tools: read / glob / grep / executor.\n"
        + "Read-only static queries only. Never mutate, compile, build, typecheck, lint, test, run project code, or spawn sub-agents."

    /// Full DevOps system prompt lives in prompts/devops-system.md and is loaded
    /// into OpenCode AgentConfig.prompt (host system prompt).
    let devopsPrompt =
        "DevOps system prompt SSOT: prompts/devops-system.md\n"
        + "Tools: fork-pty / executor / read / glob / grep / inspector / coder / join / list.\n"
        + "DevOps executes and autonomously closes mechanical operational failures through Coder; it does not make product/architecture decisions. Never write/edit directly."


    /// Full Browser system prompt lives in prompts/browser-system.md and is
    /// loaded into OpenCode AgentConfig.prompt (host system prompt).
    let browserPrompt =
        "Browser system prompt SSOT: prompts/browser-system.md\n"
        + "Tools: read / glob / grep / network.\n"
        + "Browser-only web research. Host local-read permissions serve webpage access only; never inspect repository files."

    /// Full Meditator system prompt lives in prompts/meditator-system.md and is
    /// loaded into OpenCode AgentConfig.prompt (host system prompt).
    let meditatorPrompt =
        "Meditator system prompt SSOT: prompts/meditator-system.md\n"
        + "Tools: read / glob / grep / inspector.\n"
        + "Read-only architecture reasoning. Compare options; recommend one path."

    /// Full Reviewer system prompt lives in prompts/reviewer-system.md and is
    /// loaded into OpenCode AgentConfig.prompt (host system prompt).
    let reviewerPrompt =
        "Reviewer system prompt SSOT: prompts/reviewer-system.md\n"
        + "Tools: read / glob / grep / verdict.\n"
        + "Read-only. Re-inspect the current tree; PERFECT only for flawless work and REVISE for any defect."

    let studentPrompt =
        "Student system prompt SSOT: resources/prompts/student-system.md\n"
        + "StudentLearn tools: teacher only. StudentCompile tools are request-specific."

    let teacherPrompt =
        "Teacher system prompt SSOT: resources/prompts/teacher-system.md\n"
        + "Investigate with ordinary execution tools; answer only through return."

    let all =
        let mgrTools = Roles.permissions Role.Manager
        let cdrTools = Roles.permissions Role.Coder
        let inspTools = Roles.permissions Role.Inspector
        let brwTools = Roles.permissions Role.Browser
        let medTools = Roles.permissions Role.Meditator
        let revTools = Roles.permissions Role.Reviewer
        let devopsTools = Roles.permissions Role.DevOps
        let orchTools = Roles.permissions Role.Orchestrator
        let execTools = Roles.permissions Role.Executor
        let blgTools = Roles.permissions Role.Blogger
        let studentTools = Roles.permissions Role.Student
        let teacherTools = Roles.permissions Role.Teacher

        [ { Role = Role.Manager
            Prompt = managerPrompt
            Tools = mgrTools }
          { Role = Role.Coder
            Prompt = coderPrompt
            Tools = cdrTools }
          { Role = Role.Inspector
            Prompt = inspectorPrompt
            Tools = inspTools }
          { Role = Role.DevOps
            Prompt = devopsPrompt
            Tools = devopsTools }
          { Role = Role.Browser
            Prompt = browserPrompt
            Tools = brwTools }
          { Role = Role.Meditator
            Prompt = meditatorPrompt
            Tools = medTools }
          { Role = Role.Reviewer
            Prompt = reviewerPrompt
            Tools = revTools }
          { Role = Role.Orchestrator
            Prompt =
              "Orchestrator system prompt SSOT: prompts/orchestrator-system.md\n"
              + "Tools: fork-manager / join.\n"
              + "Parallel ManagerJobs, serial integration, host-owned dual PERFECT."
            Tools = orchTools }
          { Role = Role.Student
            Prompt = studentPrompt
            Tools = studentTools }
          { Role = Role.Teacher
            Prompt = teacherPrompt
            Tools = teacherTools }
          { Role = Role.Executor
            Prompt =
              "Executor agent system prompt SSOT: prompts/executor-system.md\n"
              + "Tools: none. Distill command output; preserve paths/errors/exit codes."
            Tools = execTools }
          { Role = Role.Blogger
            Prompt =
              "Blogger system prompt SSOT: prompts/blogger-system.md\n"
              + "Tools: none. Dense work log for Session X prefix memory."
            Tools = blgTools } ]

    let forRole (role: Role) : RoleDefinition option =
        all |> List.tryFind (fun def -> def.Role = role)

    let promptFor (role: Role) : string =
        match forRole role with
        | Some def -> def.Prompt
        | None -> ""

    ()
