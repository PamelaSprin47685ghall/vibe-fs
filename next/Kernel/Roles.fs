namespace Wanxiangshu.Next.Kernel

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
    | Executor
    | Blogger

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
    | Inspector
    | Coder
    | Exec
    | Pty
    | Network
    | Verdict

module Roles =

    let permissions (role: Role) : ToolPermission Set =
        match role with
        | Role.Manager -> set [ ToolPermission.Fork; ToolPermission.Join; ToolPermission.List ]
        | Role.Orchestrator -> set [ ToolPermission.Fork; ToolPermission.Join ]
        | Role.Coder ->
            set
                [ ToolPermission.Read
                  ToolPermission.Write
                  ToolPermission.Edit
                  ToolPermission.Glob
                  ToolPermission.Grep
                  ToolPermission.Inspector ]
        | Role.Inspector -> set [ ToolPermission.Exec ]
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
                  ToolPermission.Inspector
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
        | Role.Executor
        | Role.Blogger -> Set.empty

    let isAllowed (role: Role) (permission: ToolPermission) : bool =
        permissions role |> Set.contains permission


/// RoleDefinition: combines tool permissions with system prompts for each agent role.
type RoleDefinition =
    { Role: Role
      Prompt: string
      Companion: bool
      Tools: ToolPermission Set }

module RoleDefinitions =

    let managerPrompt =
        "You are the Manager. You coordinate; you do not read or edit files directly.\n\n"
        + "Available operations:\n"
        + "- fork(role, prompt): create Coder, Inspector, Browser, Meditator, Reviewer, DevOps\n"
        + "- fork(agentId, prompt): nudge or continue an existing child\n"
        + "- join(): receive the next completed agent result\n"
        + "- list(): inspect live agent resources\n\n"
        + "Rules:\n"
        + "1. Use Inspector for command-based investigation.\n"
        + "2. Use DevOps for PTY, builds, tests, and long-running processes.\n"
        + "3. Use Browser for repository/web evidence.\n"
        + "4. Use Meditator for architecture and tradeoffs.\n"
        + "5. Only Coder may modify files.\n"
        + "6. You never operate PTY or executor yourself.\n"
        + "7. A completed join result must contain non-empty finalText.\n"
        + "8. Treat workRecord as supporting context, not proof of completion.\n"
        + "9. If a child fails or returns a protocol error, nudge or replace it explicitly.\n"
        + "10. After code changes, fork Reviewer.\n"
        + "11. REVISE means continue the same Coder with the review report.\n"
        + "12. Do not finish until the current tree has a confirmed review witness."

    let coderPrompt =
        "You are the Coder. You implement changes.\n\n"
        + "Instructions:\n"
        + "1. Read relevant files before making changes.\n"
        + "2. You may call the inspector tool once to gather command-line evidence.\n"
        + "3. Modify code and run tests.\n"
        + "4. Do not call fork.\n"
        + "5. Return a final report with:\n"
        + "   Result:\n   Files changed:\n   Tests run:\n   Evidence:\n   Remaining risks:\n   Blockers:"

    let inspectorPrompt =
        "You are a long-lived Inspector child of a Manager.\n"
        + "Your only tool is executor.\n"
        + "Use commands to gather evidence.\n"
        + "Do not modify repository files.\n"
        + "Return a final report with:\n"
        + "- commands run\n- relevant output\n- findings\n- confidence/uncertainty"

    let devopsPrompt =
        "You are DevOps, the terminal and operations specialist.\n"
        + "You own PTY and executor. You may gather evidence and delegate code edits.\n\n"
        + "Available operations:\n"
        + "- fork-pty(agent=pty, prompt): create a PTY with the given command\n"
        + "- fork-pty(ptyId, prompt): write to an existing PTY; empty prompt reads buffered output\n"
        + "- fork-pty(ptyId, signal): send TERM|KILL|INT|HUP|QUIT|USR1|USR2\n"
        + "- executor(command): run a non-interactive command with the process budget\n"
        + "- read/glob/grep: inspect repository evidence\n"
        + "- inspector(prompt): one-shot command investigation\n"
        + "- coder(prompt): one-shot Coder to modify files (you never write/edit yourself)\n"
        + "- join(): wait for the next PTY exit completion\n"
        + "- list(): inspect live PTY resources\n\n"
        + "Rules:\n"
        + "1. Execute terminal work and gather evidence as requested.\n"
        + "2. Never use write/edit yourself; delegate file changes via coder.\n"
        + "3. Do not plan products, declare tasks complete, or act as Reviewer.\n"
        + "4. Return concrete evidence: cwd, exit, signals, key output, files changed via coder."


    let browserPrompt =
        "You are the Browser. Read-only evidence gathering.\n\n"
        + "Use read/glob/grep for repository evidence and the approved web tools for external evidence.\n"
        + "Never edit.\n"
        + "Return sources, relevant excerpts, and uncertainty."

    let meditatorPrompt =
        "You are the Meditator. Analyze architecture, tradeoffs, and implementation sequencing.\n"
        + "May read/search and use one-shot Inspector.\n"
        + "Do not modify files.\n"
        + "Return a concrete recommendation with risks and rejected alternatives."

    let reviewerPrompt =
        "You are the Reviewer. Read-only.\n"
        + "Review the current tree, tests, and risks.\n"
        + "Use inspector when command evidence is necessary.\n"
        + "REVISE immediately when defects exist.\n"
        + "PERFECT only when no blocking defect remains.\n"
        + "After confirmed verdict, produce a final review report for Manager."

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

        [ { Role = Role.Manager
            Prompt = managerPrompt
            Companion = false
            Tools = mgrTools }
          { Role = Role.Coder
            Prompt = coderPrompt
            Companion = false
            Tools = cdrTools }
          { Role = Role.Inspector
            Prompt = inspectorPrompt
            Companion = false
            Tools = inspTools }
          { Role = Role.DevOps
            Prompt = devopsPrompt
            Companion = false
            Tools = devopsTools }
          { Role = Role.Browser
            Prompt = browserPrompt
            Companion = false
            Tools = brwTools }
          { Role = Role.Meditator
            Prompt = meditatorPrompt
            Companion = false
            Tools = medTools }
          { Role = Role.Reviewer
            Prompt = reviewerPrompt
            Companion = false
            Tools = revTools }
          { Role = Role.Orchestrator
            Prompt = ""
            Companion = false
            Tools = orchTools }
          { Role = Role.Executor
            Prompt = ""
            Companion = false
            Tools = execTools }
          { Role = Role.Blogger
            Prompt = ""
            Companion = true
            Tools = blgTools } ]

    let forRole (role: Role) : RoleDefinition option =
        all |> List.tryFind (fun def -> def.Role = role)

    let promptFor (role: Role) : string =
        match forRole role with
        | Some def -> def.Prompt
        | None -> ""

    ()
