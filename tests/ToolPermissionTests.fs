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

let classifyToolMethodologyFamily () =
    equal "methodology" MethodologyFamily (classifyTool Opencode "methodology")

let classifyToolMethodologyFamilyOmp () =
    equal "omp methodology" MethodologyFamily (classifyTool Omp "methodology")

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

let canUseSemanticMeditator () =
    check "meditator can read" (canUseSemantic "meditator" Read "read")

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

let canUseSemanticMethodologyBrowser () =
    check "browser denied methodology" (not (canUseSemantic "browser" MethodologyFamily "methodology"))

let canUseSemanticMethodologyInvestigator () =
    check "investigator denied methodology" (not (canUseSemantic "investigator" MethodologyFamily "methodology"))

let canUseSemanticMethodologyExecutor () =
    check "executor denied methodology" (not (canUseSemantic "executor" MethodologyFamily "methodology"))

let canUseSemanticMethodologyMeditator () =
    check "meditator denied methodology" (not (canUseSemantic "meditator" MethodologyFamily "methodology"))

let canUseSemanticMethodologyReviewer () =
    check "reviewer can methodology (same as todo)" (canUseSemantic "reviewer" MethodologyFamily "methodology")

let canUseSemanticMethodologyCoder () =
    check "coder can methodology" (canUseSemantic "coder" MethodologyFamily "methodology")

let canUseSemanticMethodologyManager () =
    check "manager can methodology" (canUseSemantic "manager" MethodologyFamily "methodology")

let canUseSemanticWriteInvestigator () =
    check "investigator denied write" (not (canUseSemantic "investigator" WritePatchFamily "write"))

let canUseSemanticWriteManager () =
    check "manager denied write" (not (canUseSemantic "manager" WritePatchFamily "write"))

let canUseSemanticWriteCoder () =
    check "coder can write" (canUseSemantic "coder" WritePatchFamily "write")

let canUseSemanticSubagentInvestigatorCoder () =
    check
        "investigator cannot use coder subagent (only executor)"
        (not (canUseSemantic "investigator" SubagentWebSkillOrSubmit "coder"))

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

let canUseSemanticMeditatorAll () =
    check "meditator can read" (canUseSemantic "meditator" Read "read")

let canUseSemanticExecutorAll () =
    check "executor denied all" (not (canUseSemantic "executor" Read "read"))

let canUseForHostOpenCode () =
    check "opencode coder read" (canUseForHost Opencode "coder" "read")

let canUseForHostNormalizedMux () =
    check "mux coder file_read→read" (canUseForHost Mux "coder" "file_read")

let deniedToolsForHostFilters () =
    let denied =
        deniedToolsForHost Opencode "browser" [ "read"; "stealth-browser-mcp_navigate" ]
    // Read is a universal semantic — browser can read
    check "browser allowed read" (not (List.contains "read" denied))
    check "browser allowed stealth" (not (List.contains "stealth-browser-mcp_navigate" denied))

let deniedToolsFilters () =
    let denied = deniedTools "coder" [ "bash"; "read"; "write" ]
    check "coder denied bash" (List.contains "bash" denied)
    check "coder allowed read" (not (List.contains "read" denied))
    check "coder allowed write" (not (List.contains "write" denied))

let canUseForHostMethodologyDenied () =
    check "browser denied methodology via host" (not (canUseForHost Opencode "browser" "methodology"))
    check "investigator denied methodology via host" (not (canUseForHost Opencode "investigator" "methodology"))
    check "executor denied methodology via host" (not (canUseForHost Omp "executor" "methodology"))
    check "meditator denied methodology via host" (not (canUseForHost Opencode "meditator" "methodology"))

let canUseForHostMethodologyAllowed () =
    check "coder allowed methodology via host" (canUseForHost Opencode "coder" "methodology")
    check "manager allowed methodology via host" (canUseForHost Mux "manager" "methodology")
    check "reviewer allowed methodology via host" (canUseForHost Opencode "reviewer" "methodology")

let deniedToolsForHostMethodology () =
    let denied =
        deniedToolsForHost Opencode "browser" [ "read"; "methodology"; "todowrite" ]

    check "browser denied methodology" (List.contains "methodology" denied)
    check "browser denied todowrite" (List.contains "todowrite" denied)
    check "browser allowed read" (not (List.contains "read" denied))

let testReviewerCanUseFuzzyGrep () =
    check "reviewer can use fuzzy_grep" (canUse "reviewer" "fuzzy_grep")

let testReviewerCanUseFuzzyFind () =
    check "reviewer can use fuzzy_find" (canUse "reviewer" "fuzzy_find")

let testReviewerCanSpawnInvestigatorButNotExecutor () =
    check "reviewer can spawn investigator" (canUse "reviewer" "investigator")
    check "reviewer cannot spawn executor" (not (canUse "reviewer" "executor"))
    check "reviewer cannot spawn coder" (not (canUse "reviewer" "coder"))

let testReviewerCannotWrite () =
    check "reviewer cannot write" (not (canUse "reviewer" "write"))
    check "reviewer cannot edit" (not (canUse "reviewer" "edit"))

let run () =
    classifyToolAgentReport ()
    classifyToolBlockedShell ()
    classifyToolBlockedGrep ()
    classifyToolStealthBrowser ()
    classifyToolReturnRole ()
    classifyToolTodoFamily ()
    classifyToolTodoFamilyTask ()
    classifyToolMethodologyFamily ()
    classifyToolMethodologyFamilyOmp ()
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
    canUseSemanticMethodologyBrowser ()
    canUseSemanticMethodologyInvestigator ()
    canUseSemanticMethodologyExecutor ()
    canUseSemanticMethodologyMeditator ()
    canUseSemanticMethodologyReviewer ()
    canUseSemanticMethodologyCoder ()
    canUseSemanticMethodologyManager ()
    canUseSemanticWriteInvestigator ()
    canUseSemanticWriteManager ()
    canUseSemanticWriteCoder ()
    canUseSemanticSubagentInvestigatorCoder ()
    canUseSemanticSubagentInvestigatorExecutor ()
    canUseSemanticSubagentCoder ()
    canUseSemanticSubagentManager ()
    canUseSemanticFuzzyGrepManager ()
    canUseSemanticFuzzyGrepCoder ()
    canUseSemanticMeditatorAll ()
    canUseSemanticExecutorAll ()
    canUseForHostOpenCode ()
    canUseForHostNormalizedMux ()
    deniedToolsForHostFilters ()
    deniedToolsFilters ()
    canUseForHostMethodologyDenied ()
    canUseForHostMethodologyAllowed ()
    deniedToolsForHostMethodology ()
    testReviewerCanUseFuzzyGrep ()
    testReviewerCanUseFuzzyFind ()
    testReviewerCanSpawnInvestigatorButNotExecutor ()
    testReviewerCannotWrite ()
