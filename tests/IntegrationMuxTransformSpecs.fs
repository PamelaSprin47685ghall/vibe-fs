module Wanxiangshu.Tests.IntegrationMuxTransformSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationMuxSetup
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Hosts.Mux.BuiltinTools
open Wanxiangshu.Hosts.Mux.SubagentTools
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Runtime.ReviewPrompts.Format


let muxTopLevelPolicySpec () =
    promise {
        let managerPolicy = getPluginToolPolicy "x" (box "manager")
        let managerRemoves = unbox<string[]> (get managerPolicy "remove")
        check "mux top-level policy manager removes write" (managerRemoves |> Array.contains "write")

        check
            "mux top-level policy manager keeps submit_review"
            (not (managerRemoves |> Array.contains "submit_review"))

        check "mux top-level policy manager removes fuzzy_grep" (managerRemoves |> Array.contains "fuzzy_grep")
        let coderPolicy = getPluginToolPolicy "x" (box "coder")
        let coderRemoves = unbox<string[]> (get coderPolicy "remove")
        check "mux top-level policy coder keeps write" (not (coderRemoves |> Array.contains "write"))
        check "mux top-level policy coder removes submit_review" (coderRemoves |> Array.contains "submit_review")
        let defaultPolicy = getPluginToolPolicy "x" null
        let defaultRemoves = unbox<string[]> (get defaultPolicy "remove")
        check "mux top-level policy default manager removes write" (defaultRemoves |> Array.contains "write")
    }

let muxSummarizationSpec () =
    check "mux summarization agent id is explore" (summarizationAgentId = "explore")
    check "mux summarization role is executor" (summarizationRole = "executor")
    check "mux summarization ai settings agent id is explore" (summarizationAiSettingsAgentId = "explore")

/// Locks the permission shape of the summary child workspace. Mirrors opencode's
/// `executor` agent: only `agent_report` survives — every other surface
/// (sub-agents, mutating tools, fuzzy, write, etc.) must be stripped so
/// the child cannot re-enter the host tool surface (no `inspector`/`coder`/
/// `browser`/`meditator` re-spawn, no file edits, no further fetches).
let muxSummarizationToolPolicySpec () =
    let toolNames =
        [| "coder"
           "inspector"
           "meditator"
           "browser"
           "executor"
           "submit_review"
           "return_reviewer"
           "websearch"
           "webfetch"
           "fuzzy_grep"
           "fuzzy_find"
           "write"
           "read" |]

    let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
    check "toolOptions is provided" (Option.isSome opts)
    let payload = Option.get opts
    let experiments = get payload "experiments"
    check "subagentRole bound to executor" (str experiments "subagentRole" = "executor")
    check "aiSettingsAgentId routes to explore model" (str payload "aiSettingsAgentId" = "explore")
    let policy = get experiments "toolPolicy"
    let disabled = unbox<string[]> (get policy "disabledTools") |> Set.ofArray

    for removed in
        [ "coder"
          "inspector"
          "meditator"
          "browser"
          "executor"
          "submit_review"
          "return_reviewer"
          "websearch"
          "webfetch"
          "fuzzy_grep"
          "fuzzy_find"
          "write"
          "read" ] do
        check $"summary child strips {removed}" (Set.contains removed disabled)

let muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let tf = muxMessageTransform reg
        let sessionID = "mux-review-accepted-history"

        if isNullish tf then
            check "mux messagesTransform exposed for accepted review replay" false
        else
            let accepted =
                formatReviewResult (Wanxiangshu.Kernel.ReviewSession.ReviewResult.Accepted "")

            let messages =
                [| muxTextMessage "loop-task" "assistant" "---\ntask: Ship feature\n---\nWith-Review Mode is active."
                   muxDynamicToolMessage "submit-review" "submit_review" "call-review" (createObj []) (box accepted) |]

            muxReplayReviewTaskForTest reg sessionID (Some "Ship feature")
            let out = createObj [ "messages", box messages ]
            let input = createObj [ "agent", box "manager"; "sessionID", box sessionID ]
            do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
            // With IfStoreEmpty (Defect 1 fix), transform does NOT clear an active review
            // when store is non-empty — verdict resolution is the tool path's job, not replay's.
            // This prevents the mid-session silent deactivation bug.
            check "mux transform preserves active review when store non-empty" (muxIsReviewActiveForTest reg sessionID)
    }
