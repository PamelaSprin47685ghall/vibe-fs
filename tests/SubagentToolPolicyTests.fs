module VibeFs.Tests.SubagentToolPolicyTests

open VibeFs.Tests.Assert
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Config
open VibeFs.Kernel.ToolPermission
open VibeFs.Kernel.SubagentToolPolicy

let private manualDeniedForMux (extraNames: string array) (role: string) : Set<string> =
    Seq.append extraNames muxSpawnToolUniverse
    |> Seq.distinct
    |> deniedToolsForHost mux role
    |> Set.ofList

let private policyDenied (extraNames: string array) (role: string) : Set<string> =
    disabledToolNamesForRole mux extraNames role muxSpawnToolUniverse
    |> Set.ofArray

let disabledMatchesManualDeniedForHost () =
    for role in [| "coder"; "investigator"; "reviewer"; "manager"; "browser" |] do
        for extra in [| [||]; [| "custom_plugin_tool" |]; [| "fuzzy_find"; "write" |] |] do
            let manual = manualDeniedForMux extra role
            let policy = policyDenied extra role
            check $"mux disabled set matches manual deniedToolsForHost role={role} extra={extra.Length}"
                (manual = policy)

let reviewerDisabledOmitsAgentReport () =
    let denied = policyDenied [||] "reviewer"
    check "reviewer disabledTools must not include agent_report" (not (Set.contains "agent_report" denied))

let reviewerDisabledIncludesFileEditReplaceString () =
    let denied = policyDenied [||] "reviewer"
    check "reviewer disabledTools includes file_edit_replace_string"
        (Set.contains "file_edit_replace_string" denied)

let run () =
    disabledMatchesManualDeniedForHost ()
    reviewerDisabledOmitsAgentReport ()
    reviewerDisabledIncludesFileEditReplaceString ()