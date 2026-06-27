module Wanxiangshu.Tests.IntegrationMuxTransformSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ReviewPrompts
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Mux.BuiltinTools
open Wanxiangshu.Mux.SubagentTools
open Wanxiangshu.Shell.Dyn


let muxTopLevelPolicySpec () =
    promise {
        let managerPolicy = getPluginToolPolicy "x" (box "manager")
        let managerRemoves = unbox<string[]> (get managerPolicy "remove")
        check "mux top-level policy manager removes write" (managerRemoves |> Array.contains "write")
        check "mux top-level policy manager keeps submit_review" (not (managerRemoves |> Array.contains "submit_review"))
        check "mux top-level policy manager removes fuzzy_grep" (managerRemoves |> Array.contains "fuzzy_grep")
        let coderPolicy = getPluginToolPolicy "x" (box "coder")
        let coderRemoves = unbox<string[]> (get coderPolicy "remove")
        check "mux top-level policy coder keeps write" (not (coderRemoves |> Array.contains "write"))
        check "mux top-level policy coder removes submit_review" (coderRemoves |> Array.contains "submit_review")
        let defaultPolicy = getPluginToolPolicy "x" null
        let defaultRemoves = unbox<string[]> (get defaultPolicy "remove")
        check "mux top-level policy default manager removes write" (defaultRemoves |> Array.contains "write")
    }

let muxTopLevelDedupSpec () =
    promise {
        let mutable callIdCounter = 0
        let readMsg (content: string) : obj =
            callIdCounter <- callIdCounter + 1
            box {| parts = [| box {| ``type`` = "dynamic-tool"; toolName = "file_read"; state = "output-available"; output = box {| content = content |}; toolCallId = string callIdCounter |} |] |}
        let history = [| readMsg "seen" |]
        let window = [| readMsg "seen" |]
        let seen = collectReadOutputs history
        check "mux top-level collectReadOutputs returns content" (seen.Length = 1 && seen.[0] = "seen")
        let result = deduplicateReadOutputsWithSeen seen window
        check "mux top-level dedup returns array" (result.Length = 1)
        let output = get (unbox<obj[]> (get result.[0] "parts")).[0] "output"
        check "mux top-level dedup replaces repeat content" (str output "content" = Wanxiangshu.Kernel.ToolOutputInfo.noChangeEnvelope ())
    }

let muxSummarizationSpec () =
    check "mux summarization agent id is explore" (summarizationAgentId = "explore")
    check "mux summarization role is executor" (summarizationRole = "executor")
    check "mux summarization ai settings agent id is explore" (summarizationAiSettingsAgentId = "explore")

/// Locks the permission shape of the summary child workspace. Mirrors opencode's
/// `executor` agent: only `agent_report` survives — every other surface
/// (sub-agents, mutating tools, fuzzy, knowledge graph, write, etc.) must be stripped so
/// the child cannot re-enter the host tool surface (no `investigator`/`coder`/
/// `browser`/`meditator` re-spawn, no file edits, no further fetches).
let muxSummarizationToolPolicySpec () =
    let toolNames =
        [| "coder"; "investigator"; "meditator"; "browser"; "executor"
           "submit_review"; "return_reviewer"; "websearch"; "webfetch"; "fuzzy_grep"; "fuzzy_find"; "write"; "read"
           "knowledge_graph_fetch"; "return_bookkeeper" |]
    let opts = toolOptions toolNames summarizationRole summarizationAiSettingsAgentId
    check "toolOptions is provided" (Option.isSome opts)
    let payload = Option.get opts
    let experiments = get payload "experiments"
    check "subagentRole bound to executor" (str experiments "subagentRole" = "executor")
    check "aiSettingsAgentId routes to explore model" (str payload "aiSettingsAgentId" = "explore")
    let policy = get experiments "toolPolicy"
    let disabled = unbox<string[]> (get policy "disabledTools") |> Set.ofArray
    for removed in [ "coder"; "investigator"; "meditator"; "browser"; "executor"
                     "submit_review"; "return_reviewer"; "websearch"; "webfetch"
                     "fuzzy_grep"; "fuzzy_find"; "write"; "knowledge_graph_fetch"; "return_bookkeeper" ] do
        check $"summary child strips {removed}" (Set.contains removed disabled)

let muxMessagesTransformDedupsRepeatedReadSpec () = promise {
    let reg = sharedMuxRegistration ()
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for read dedup" false
    else
        let messages =
            [| muxDynamicToolMessage "read-1" "read" "call-1" (createObj [ "path", box "same.ts" ]) (box "same bytes")
               muxDynamicToolMessage "read-2" "read" "call-2" (createObj [ "path", box "same.ts" ]) (box "same bytes") |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box "mux-read-dedup-session" ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let readMessages =
            transformed
            |> Array.filter (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts |> Array.exists (fun part -> str part "toolName" = "read"))
        check "mux messagesTransform keeps both plugin read messages" (readMessages.Length = 2)
        let secondOutput = muxFirstDynamicToolOutput readMessages.[1]
        check "mux messagesTransform dedups repeated plugin read" (string secondOutput = Wanxiangshu.Kernel.ToolOutputInfo.noChangeEnvelope ())
}

let muxMessagesTransformDedupsRepeatedFileReadSpec () = promise {
    let reg = sharedMuxRegistration ()
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for file_read dedup" false
    else
        let repeated = box {| content = "same bytes" |}
        let messages =
            [| muxDynamicToolMessage "read-1" "file_read" "call-1" (createObj [ "path", box "same.ts" ]) repeated
               muxDynamicToolMessage "read-2" "file_read" "call-2" (createObj [ "path", box "same.ts" ]) repeated |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box "mux-read-dedup-session" ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let readMessages =
            transformed
            |> Array.filter (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts |> Array.exists (fun part -> str part "toolName" = "file_read"))
        check "mux messagesTransform keeps both read messages" (readMessages.Length = 2)
        let secondOutput = muxFirstDynamicToolOutput readMessages.[1]
        check "mux messagesTransform dedups repeated file_read" (str secondOutput "content" = Wanxiangshu.Kernel.ToolOutputInfo.noChangeEnvelope ())
}

let muxMessagesTransformDedupsRepeatedReadForTopLevelExecSpec () = promise {
    let reg = sharedMuxRegistration ()
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for top-level exec read dedup" false
    else
        let messages =
            [| muxDynamicToolMessage "read-top-1" "read" "call-top-1" (createObj [ "path", box "same.ts" ]) (box {| content = "same bytes" |})
               muxDynamicToolMessage "read-top-2" "read" "call-top-2" (createObj [ "path", box "same.ts" ]) (box {| content = "same bytes" |}) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "exec"; "workspaceId", box "top-level-exec" ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let readMessages =
            transformed
            |> Array.filter (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts |> Array.exists (fun part -> str part "toolName" = "read"))
        check "mux messagesTransform keeps both top-level exec read messages" (readMessages.Length = 2)
        let secondOutput = muxFirstDynamicToolOutput readMessages.[1]
        check "mux messagesTransform dedups repeated read for top-level exec" (str secondOutput "content" = Wanxiangshu.Kernel.ToolOutputInfo.noChangeEnvelope ())
}

let muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec () = promise {
    let reg = sharedMuxRegistration ()
    let tf = muxMessageTransform reg
    let sessionID = "mux-review-accepted-history"
    muxActivateReviewForTest reg sessionID "Ship feature"
    if isNullish tf then
        check "mux messagesTransform exposed for accepted review replay" false
    else
        let accepted = formatReviewResult (Wanxiangshu.Kernel.ReviewSession.ReviewResult.Accepted "")
        let messages =
            [| muxTextMessage "loop-task" "assistant" "---\ntask: Ship feature\n---\nWith-Review Mode is active."
               muxDynamicToolMessage "submit-review" "submit_review" "call-review" (createObj []) (box accepted) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box sessionID ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        // With IfStoreEmpty (Defect 1 fix), transform does NOT clear an active review
        // when store is non-empty — verdict resolution is the tool path's job, not replay's.
        // This prevents the mid-session silent deactivation bug.
        check "mux transform preserves active review when store non-empty" (muxIsReviewActiveForTest reg sessionID)
}

let muxCompactingTransformProjectsBacklogSpec () = promise {
    let reg = sharedMuxRegistration ()
    let compactingTransform = get reg "compactingTransform"
    if isNullish compactingTransform then
        check "mux registration exposes compactingTransform" false
    else
        let todoInput report content status priority =
            createObj
                [ "completedWorkReport", box report
                  "todos", box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ]
        let todoOutput count = createObj [ "success", box true; "count", box count ]
        let messages =
            [| muxTextMessage "compact-user-1" "user" "plan phase"
               muxDynamicToolMessage "compact-1" "todo_write" "compact-call-a" (todoInput "planned compact phase" "Plan change" "in_progress" "high") (todoOutput 1)
               muxTextMessage "compact-user-2" "user" "implement phase"
               muxDynamicToolMessage "compact-2" "todo_write" "compact-call-b" (todoInput "implemented compact phase" "Implement change" "completed" "high") (todoOutput 1)
               muxTextMessage "compact-user-3" "user" "verify phase"
               muxDynamicToolMessage "compact-3" "todo_write" "compact-call-c" (todoInput "verified compact phase" "Verify change" "completed" "medium") (todoOutput 1) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box "mux-compacting-session" ]
        do! (compactingTransform $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let texts =
            transformed
            |> Array.collect (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts
                |> Array.choose (fun part -> if str part "type" = "text" then Some (str part "text") else None))
        check "mux compacting transform injects folded backlog text" (
            texts
            |> Array.exists (fun text ->
                text.Contains("Completed work from folded turns. File changes are already on disk.")
                && text.Contains("planned compact phase")))
}
