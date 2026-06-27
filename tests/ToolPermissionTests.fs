module Wanxiangshu.Tests.ToolPermissionTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission

let classifyToolAgentReport () =
    equal "agent_report" AgentReport (classifyTool Opencode "agent_report")
let classifyToolBlockedShell () =
    equal "bash" BlockedShellTaskGrep (classifyTool Opencode "bash")
let classifyToolBlockedGrep () =
    equal "grep" BlockedShellTaskGrep (classifyTool Opencode "grep")
let classifyToolStealthBrowser () =
    equal "stealth-browser" StealthBrowser (classifyTool Opencode "stealth-browser-mcp_navigate")
let classifyToolReturnRole () =
    equal "return_coder" ReturnRoleEcho (classifyTool Opencode "return_coder")
let classifyToolTodoFamily () =
    equal "todowrite" TodoFamily (classifyTool Opencode "todowrite")
let classifyToolTodoFamilyTask () =
    equal "mimo task" TodoFamily (classifyTool Mimocode "task")
let classifyToolRead () =
    equal "read" Read (classifyTool Opencode "read")
let classifyToolWrite () =
    equal "write" WritePatchFamily (classifyTool Opencode "write")
let classifyToolEdit () =
    equal "edit" WritePatchFamily (classifyTool Opencode "edit")
let classifyToolSubagent () =
    equal "coder" SubagentWebSkillOrSubmit (classifyTool Opencode "coder")
let classifyToolWebsearch () =
    equal "websearch" SubagentWebSkillOrSubmit (classifyTool Opencode "websearch")
let classifyToolFuzzyGrep () =
    equal "fuzzy_grep" FuzzyGrep (classifyTool Opencode "fuzzy_grep")
let classifyToolOther () =
    equal "unknown" Other (classifyTool Opencode "unknown_tool")

let canUseSemanticAgentReport () =
    check "any agent can use agent_report" (canUseSemantic "manager" AgentReport "agent_report")
let canUseSemanticBlockedShell () =
    check "no agent can use bash" (not (canUseSemantic "manager" BlockedShellTaskGrep "bash"))
let canUseSemanticStealthBrowserOk () =
    check "browser can use stealth" (canUseSemantic "browser" StealthBrowser "stealth-browser-mcp_navigate")
let canUseSemanticStealthBrowserDenied () =
    check "coder cannot use stealth" (not (canUseSemantic "coder" StealthBrowser "stealth-browser-mcp_navigate"))
let canUseSemanticReturnRoleOk () =
    check "coder can use return_coder" (canUseSemantic "coder" ReturnRoleEcho "return_coder")
let canUseSemanticReturnRoleDenied () =
    check "coder cannot use return_browser" (not (canUseSemantic "coder" ReturnRoleEcho "return_browser"))
let canUseSemanticBookkeeper () =
    check "bookkeeper denied all" (not (canUseSemantic "bookkeeper" Read "read"))
let canUseSemanticMeditator () =
    check "meditator denied all" (not (canUseSemantic "meditator" Read "read"))
let canUseSemanticExecutor () =
    check "executor denied all" (not (canUseSemantic "executor" Read "read"))
let canUseSemanticReadReviewer () =
    check "reviewer can read (Read semantic is universal)" (canUseSemantic "reviewer" Read "read")
let canUseSemanticReadBrowser () =
    check "browser can read (Read semantic matches before browser exclusion)" (canUseSemantic "browser" Read "read")
let canUseSemanticReadCoder () =
    check "coder can read" (canUseSemantic "coder" Read "read")
let canUseSemanticTodoReviewer () =
    // reviewer not in defaultExcludedAgents, so reviewer CAN use todo
    check "reviewer can use todo" (canUseSemantic "reviewer" TodoFamily "todowrite")
let canUseSemanticTodoBrowser () =
    // browser IS in defaultExcludedAgents, so browser CANNOT use todo
    check "browser denied todo" (not (canUseSemantic "browser" TodoFamily "todowrite"))
let canUseSemanticTodoCoder () =
    check "coder can todo" (canUseSemantic "coder" TodoFamily "todowrite")
let canUseSemanticWriteInvestigator () =
    check "investigator denied write" (not (canUseSemantic "investigator" WritePatchFamily "write"))
let canUseSemanticWriteManager () =
    check "manager denied write" (not (canUseSemantic "manager" WritePatchFamily "write"))
let canUseSemanticWriteCoder () =
    check "coder can write" (canUseSemantic "coder" WritePatchFamily "write")
let canUseSemanticSubagentInvestigatorCoder () =
    check "investigator cannot use coder subagent (only executor)" (not (canUseSemantic "investigator" SubagentWebSkillOrSubmit "coder"))
let canUseSemanticSubagentInvestigatorExecutor () =
    check "investigator can use executor" (canUseSemantic "investigator" SubagentWebSkillOrSubmit "executor")
let canUseSemanticSubagentCoder () =
    check "coder cannot use coder subagent" (not (canUseSemantic "coder" SubagentWebSkillOrSubmit "coder"))
let canUseSemanticSubagentManager () =
    check "manager can use subagent (not excluded)" (canUseSemantic "manager" SubagentWebSkillOrSubmit "coder")
let canUseSemanticFuzzyGrepManager () =
    check "manager denied fuzzy_grep" (not (canUseSemantic "manager" FuzzyGrep "fuzzy_grep"))
let canUseSemanticFuzzyGrepCoder () =
    check "coder can fuzzy_grep" (canUseSemantic "coder" FuzzyGrep "fuzzy_grep")
let canUseSemanticBookkeeperAll () =
    check "bookkeeper denied all" (not (canUseSemantic "bookkeeper" Read "read"))
let canUseSemanticMeditatorAll () =
    check "meditator denied all" (not (canUseSemantic "meditator" Read "read"))
let canUseSemanticExecutorAll () =
    check "executor denied all" (not (canUseSemantic "executor" Read "read"))

let canUseForHostOpenCode () =
    check "opencode coder read" (canUseForHost Opencode "coder" "read")
let canUseForHostNormalizedMux () =
    check "mux coder file_read→read" (canUseForHost Mux "coder" "file_read")

let deniedToolsForHostFilters () =
    let denied = deniedToolsForHost Opencode "browser" [ "read"; "stealth-browser-mcp_navigate" ]
    // Read is a universal semantic — browser can read
    check "browser allowed read" (not (List.contains "read" denied))
    check "browser allowed stealth" (not (List.contains "stealth-browser-mcp_navigate" denied))

let deniedToolsFilters () =
    let denied = deniedTools "coder" [ "bash"; "read"; "write" ]
    check "coder denied bash" (List.contains "bash" denied)
    check "coder allowed read" (not (List.contains "read" denied))
    check "coder allowed write" (not (List.contains "write" denied))

let run () =
    classifyToolAgentReport ()
    classifyToolBlockedShell ()
    classifyToolBlockedGrep ()
    classifyToolStealthBrowser ()
    classifyToolReturnRole ()
    classifyToolTodoFamily ()
    classifyToolTodoFamilyTask ()
    classifyToolRead ()
    classifyToolWrite ()
    classifyToolEdit ()
    classifyToolSubagent ()
    classifyToolWebsearch ()
    classifyToolFuzzyGrep ()
    classifyToolOther ()
    canUseSemanticAgentReport ()
    canUseSemanticBlockedShell ()
    canUseSemanticStealthBrowserOk ()
    canUseSemanticStealthBrowserDenied ()
    canUseSemanticReturnRoleOk ()
    canUseSemanticReturnRoleDenied ()
    canUseSemanticReadReviewer ()
    canUseSemanticReadBrowser ()
    canUseSemanticReadCoder ()
    canUseSemanticTodoReviewer ()
    canUseSemanticTodoBrowser ()
    canUseSemanticTodoCoder ()
    canUseSemanticWriteInvestigator ()
    canUseSemanticWriteManager ()
    canUseSemanticWriteCoder ()
    canUseSemanticSubagentInvestigatorCoder ()
    canUseSemanticSubagentInvestigatorExecutor ()
    canUseSemanticSubagentCoder ()
    canUseSemanticSubagentManager ()
    canUseSemanticFuzzyGrepManager ()
    canUseSemanticFuzzyGrepCoder ()
    canUseSemanticBookkeeperAll ()
    canUseSemanticMeditatorAll ()
    canUseSemanticExecutorAll ()
    canUseForHostOpenCode ()
    canUseForHostNormalizedMux ()
    deniedToolsForHostFilters ()
    deniedToolsFilters ()
