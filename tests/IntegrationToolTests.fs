module VibeFs.Tests.IntegrationToolTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.IntegrationCapsSpecs
open VibeFs.Tests.IntegrationKnowledgeGraphPreludeSpecs
open VibeFs.Tests.IntegrationAfterHookSpecs
open VibeFs.Tests.IntegrationMaintenanceSpecs
open VibeFs.Tests.IntegrationSubmitKnowledgeGraphSpecs
open VibeFs.Tests.IntegrationBookkeeperSpecs
open VibeFs.Tests.IntegrationToolDefSpecs
open VibeFs.Tests.IntegrationSubagentSpecs
open VibeFs.Tests.IntegrationMiscSpecs
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Executor
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Mux.BuiltinTools
open VibeFs.Mux.Plugin
open VibeFs.Mux.SubagentTools
open VibeFs.Shell.MagicSessionStore
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.TempWorkspace
open VibeFs.Kernel.Prompts

let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let targets = wrappers |> Array.map (fun w -> str w "targetTool") |> Array.sort
    let expected = [| "agent_report"; "file_edit_insert"; "file_edit_replace_string"; "file_read"; "todo_write" |] |> Array.sort
    check "wrapper targets correct" (targets = expected)
    let ar = wrappers |> Array.find (fun w -> str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (isNullish ar))

let computeCountSpec (reg: obj) =
    let tools = unbox<obj[]> (get reg "tools")
    let names = tools |> Array.map (fun t -> str t "name")
    check "has coder tool" (names |> Array.contains "coder")
    check "has webfetch tool" (names |> Array.contains "webfetch")
    check "has write tool" (names |> Array.contains "write")
    check "has read tool" (names |> Array.contains "read")
    check "has submit_review tool" (names |> Array.contains "submit_review")
    check "has knowledge_graph_fetch tool" (names |> Array.contains "knowledge_graph_fetch")
    check "has return_bookkeeper tool" (names |> Array.contains "return_bookkeeper")
    check "has return_reviewer tool" (names |> Array.contains "return_reviewer")

let muxMessageTransformRegisteredSpec () =
    promise {
        let reg = createRegistration (minimalMuxDeps ())
        let tf = muxMessageTransform reg
        check "mux registration exposes messagesTransform" (not (isNullish tf))
        check "mux messagesTransform is callable" (typeIs tf "function")
    }

let muxKnowledgeGraphPreludeForManagerSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-manager-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let originalMsg = muxTextMessage "msg-manager" "user" "go"
    let out = createObj [ "messages", box [| originalMsg |] ]
    let input = createObj [ "agent", box "manager"; "directory", box workspaceDir; "sessionID", box "mux-kg-prelude-manager-session" ]
    if isNullish tf then
        check "mux messagesTransform exposed for manager" false
    else
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        check "mux manager knowledge graph prelude injects prefix messages" (msgs.Length >= 2)
        let firstText = firstTextPartText msgs.[0]
        check "mux manager knowledge graph prelude has Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
        check "mux manager knowledge graph prelude has knowledge graph front matter" (firstText.Contains "---\nknowledge_graph:")
        check "mux manager knowledge graph prelude lists entity" (firstText.Contains "项目" && firstText.Contains "插件入口" && not (firstText.Contains "0a3f"))
        check "mux manager knowledge graph prelude hides fact" (not (firstText.Contains "src/Mux/Plugin.fs"))
        check "mux manager knowledge graph prelude preserves original" (obj.ReferenceEquals(msgs.[msgs.Length - 1], originalMsg))
    do! rmAsync workspaceDir
}

let muxKnowledgeGraphPreludeForCoderSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-coder-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let originalMsg = muxTextMessage "msg-coder" "user" "go"
    let out = createObj [ "messages", box [| originalMsg |] ]
    let input = createObj [ "agent", box "coder"; "directory", box workspaceDir; "sessionID", box "mux-kg-prelude-coder-session" ]
    if isNullish tf then
        check "mux messagesTransform exposed for coder" false
    else
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        check "mux coder knowledge graph prelude injects prefix messages" (msgs.Length >= 2)
        let firstText = firstTextPartText msgs.[0]
        check "mux coder knowledge graph prelude has Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
        check "mux coder knowledge graph prelude has knowledge graph front matter" (firstText.Contains "---\nknowledge_graph:")
        check "mux coder knowledge graph prelude lists entity" (firstText.Contains "项目" && firstText.Contains "插件入口" && not (firstText.Contains "0a3f"))
        check "mux coder knowledge graph prelude hides fact" (not (firstText.Contains "src/Mux/Plugin.fs"))
        check "mux coder knowledge graph prelude preserves original" (obj.ReferenceEquals(msgs.[msgs.Length - 1], originalMsg))
    do! rmAsync workspaceDir
}

let muxNoKnowledgeGraphPreludeForExcludedAgentsSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-excluded-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for excluded agents" false
    else
        for agent in [| "browser"; "bookkeeper" |] do
            let originalMsg = muxTextMessage ("msg-" + agent) "user" "go"
            let out = createObj [ "messages", box [| originalMsg |] ]
            let input = createObj [ "agent", box agent; "directory", box workspaceDir; "sessionID", box ("mux-kg-excl-" + agent) ]
            do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
            let msgs = unbox<obj[]> (get out "messages")
            check (agent + " still receives default prefix") (msgs.Length >= 2)
            let firstText = firstTextPartText msgs.[0]
            check (agent + " default prefix has Kolmolgorov prelude content") (firstText.Contains "# Kolmolgorov 宝典")
            check (agent + " omits knowledge graph prelude") (not (firstText.Contains "knowledge_graph"))
            check (agent + " preserves original") (obj.ReferenceEquals(msgs.[msgs.Length - 1], originalMsg))
    do! rmAsync workspaceDir
}

let muxCapsAndKnowledgeGraphPreludeOrderSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-caps-kg-order-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let originalMsg = muxTextMessage "msg-order" "user" "go"
    let out = createObj [ "messages", box [| originalMsg |] ]
    let input = createObj [ "agent", box "manager"; "directory", box workspaceDir; "sessionID", box "mux-caps-kg-session" ]
    if isNullish tf then
        check "mux messagesTransform exposed for caps+kg order" false
    else
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        check "mux caps+kg injects prefix messages" (msgs.Length >= 2)
        let firstText = firstTextPartText msgs.[0]
        check "mux caps+kg first message has Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
        check "mux caps+kg first message includes knowledge graph front matter" (firstText.Contains "---\nknowledge_graph:")
        let hasCapsAssistant = msgs.[..msgs.Length - 2] |> Array.exists hasDynamicToolReadPart
        check "mux caps+kg includes assistant caps read before original" hasCapsAssistant
        check "mux caps+kg preserves original" (obj.ReferenceEquals(msgs.[msgs.Length - 1], originalMsg))
    do! rmAsync workspaceDir
}

let muxFetchKnowledgeGraphSnapshotSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-fetch-"
    do! ensureKnowledgeGraphDir workspaceDir
    let entity = ["项目"; "入口"]
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [
        knowledgeGraphEntry "0a3f" entity "Old fact 1"
        knowledgeGraphEntry "b912" entity "Old fact 2" ]
    let reg = createRegistration (createObj [])
    let fetchTool = muxToolByName reg "knowledge_graph_fetch"
    if isNullish fetchTool then
        check "mux registration exposes knowledge_graph_fetch tool" false
    else
        let context = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-fetch-session" ]
        let! answer = ((get fetchTool "execute") $ (context, createObj [ "entity", box "项目 入口" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch returns concatenated facts" (answer.Contains "Old fact 1" && answer.Contains "Old fact 2")
        let! invalid = ((get fetchTool "execute") $ (context, createObj [ "entity", box "" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch validates entity" (invalid.Contains "Invalid knowledge graph entity")
        let! missing = ((get fetchTool "execute") $ (context, createObj [ "entity", box "missing entity" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch reports missing entity" (missing.Contains "Knowledge graph entity not found")
    do! rmAsync workspaceDir
}


let muxReturnBookkeeperAppendSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-submit-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Old answer" ]
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let runtime = muxKnowledgeGraphRuntime reg
        if isNullish runtime then
            check "mux registration exposes knowledge graph runtime for testing" false
        else
            let today = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
            registerMuxKnowledgeGraphJobForTest reg "mux-kg-job" workspaceDir "append" (createObj [ "today", box today ])
            let entries = [|
                knowledgeGraphDraftEntry (Some "0a3f") ["项目"; "插件入口"] "Updated answer"
                knowledgeGraphDraftEntry None ["新知识"] "Fresh answer"
            |]
            let submitArgs = createObj [ "entries", box entries ]
            let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-job" ]
            let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
            check "mux return_bookkeeper returns a response" (result <> "")
            let! projection = readKnowledgeGraphProjectionAsync workspaceDir
            let updated = Map.find (tryParseId "0a3f" |> Option.get) projection
            check "mux return_bookkeeper updates existing id" (updated.fact = "Updated answer" && updated.entity = ["项目"; "插件入口"])
            let fresh = projection |> Map.toList |> List.tryFind (fun (_, entry) -> entry.fact = "Fresh answer")
            check "mux return_bookkeeper allocates id for new entry" (
                match fresh with
                | Some (id, entry) -> idValue id <> "" && entry.fact = "Fresh answer" && entry.entity = ["新知识"]
                | None -> false)
            let! files = readAllKnowledgeGraphFiles workspaceDir
            let dayFile = files |> List.tryFind (fun file -> let (DayHeader(date, _)) = file.header in date = today)
            check "mux return_bookkeeper creates today file" (dayFile.IsSome)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperNoActiveJobSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-nojob-"
    do! ensureKnowledgeGraphDir workspaceDir
    let reg = createRegistration (createObj [])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let entries = [| knowledgeGraphDraftEntry None ["问题"] "答案。" |]
        let submitArgs = createObj [ "entries", box entries ]
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-nojob-session" ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper rejects unregistered job even with directory" (result = "No active knowledge graph job for this session.")
        let! files = readAllKnowledgeGraphFiles workspaceDir
        check "mux return_bookkeeper does not create files for unregistered job" (List.isEmpty files)
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperReconstructsJobFromHistorySpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-history-"
    do! ensureKnowledgeGraphDir workspaceDir
    let sessionID = "mux-kg-history-session"
    let marker = renderJobMarker { workspaceRoot = workspaceDir; kind = AppendAfterWork }
    let deps = muxDepsWithChatHistory sessionID [| box marker |]
    let reg = createRegistration deps
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box sessionID ]
        let submitArgs = createObj [ "entries", box [| knowledgeGraphDraftEntry None ["历史重建问题"] "历史重建答案" |] ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper reconstructs job from history" (result.Contains "Appended 1 knowledge graph entries")
        let! projection = readKnowledgeGraphProjectionAsync workspaceDir
        check "mux return_bookkeeper history reconstruction persists entry" (
            projection |> Map.toList |> List.exists (fun (_, entry) -> entry.entity = ["历史重建问题"] && entry.fact = "历史重建答案"))
    do! rmAsync workspaceDir
}

let muxReturnBookkeeperAppendDoesNotTriggerMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-no-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [
        for i in 1 .. 10 do yield knowledgeGraphEntry (sprintf "%04x" i) [sprintf "积压问题 %d" i] "Candidate" ]
    let today = System.DateTime.UtcNow.ToString("yyyy-MM-dd")
    let reg = createRegistration (minimalMuxDeps ())
    registerMuxKnowledgeGraphJobForTest reg "mux-kg-launch-job" workspaceDir "append" (createObj [ "today", box today ])
    let submitTool = muxToolByName reg "return_bookkeeper"
    if isNullish submitTool then
        check "mux registration exposes return_bookkeeper tool" false
    else
        let submitCtx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-kg-launch-job" ]
        let submitArgs = createObj [ "entries", box [| knowledgeGraphDraftEntry None ["触发问题"] "触发答案" |] ]
        let! result = ((get submitTool "execute") $ (submitCtx, submitArgs)) |> unbox<JS.Promise<string>>
        check "mux return_bookkeeper append accepted" (result <> "")
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux return_bookkeeper append does not trigger maintenance" (launches.Length = 0)
    do! rmAsync workspaceDir
}

let muxExecutorRwTriggersMaintenanceSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-maintenance-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [ knowledgeGraphEntry "0a3f" ["积压问题"] "Daily candidate" ]
    let deps = minimalMuxDeps ()
    deps?("directory") <- workspaceDir
    let reg = createRegistration deps
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-executor-maintenance"; "sessionID", box "mux-executor-maintenance" ]
        let args = createObj [ "language", box "shell"; "program", box "printf mux-maintenance"; "timeout_type", box "short"; "mode", box "rw" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "mux rw executor returns output" (result.Contains "mux-maintenance")
        let after = get reg "tool.execute.after"
        let afterInput =
            createObj
                [ "tool", box "executor"
                  "sessionID", box "mux-executor-maintenance"
                  "callID", box "mux-exec-after"
                  "args", box args
                  "directory", box workspaceDir ]
        let afterOutput = createObj [ "output", box result ]
        do! after $ (afterInput, afterOutput) |> unbox<JS.Promise<unit>>
        do! waitForBackgroundJobsForTesting reg
        let launches = takeBookkeeperLaunchesForTesting reg
        check "mux rw executor triggers maintenance" (
            launches |> Array.exists (fun launch ->
                let title = (str launch "title").ToLowerInvariant()
                let prompt = (str launch "prompt").ToLowerInvariant()
                title.Contains "daily" || prompt.Contains "daily" || title.Contains "rewrite" || prompt.Contains "rewrite"))
    do! rmAsync workspaceDir
}

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
        check "mux top-level dedup replaces repeat content" (str output "content" = "[No Change Since Previous Read/Write]")
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
    // Every host surface except the read-only escape hatches must be removed.
    for removed in [ "coder"; "investigator"; "meditator"; "browser"; "executor"
                     "submit_review"; "return_reviewer"; "websearch"; "webfetch"
                     "fuzzy_grep"; "fuzzy_find"; "write"; "knowledge_graph_fetch"; "return_bookkeeper" ] do
        check $"summary child strips {removed}" (Set.contains removed disabled)

let private muxDynamicToolMessage (id: string) (toolName: string) (toolCallId: string) (input: obj) (output: obj) : obj =
    box
        {| id = id
           role = "assistant"
           parts =
            [| box
                   {| ``type`` = "dynamic-tool"
                      toolName = toolName
                      toolCallId = toolCallId
                      state = "output-available"
                      input = input
                      output = output |} |] |}

let private muxFirstDynamicToolOutput (msg: obj) : obj =
    get (unbox<obj[]> (get msg "parts")).[0] "output"

let muxMessagesTransformDedupsRepeatedReadSpec () = promise {
    let reg = createRegistration (createObj [])
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
        check "mux messagesTransform dedups repeated plugin read" (string secondOutput = "[No Change Since Previous Read/Write]")
}

let muxMessagesTransformDedupsRepeatedFileReadSpec () = promise {
    let reg = createRegistration (createObj [])
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
        check "mux messagesTransform dedups repeated file_read" (str secondOutput "content" = "[No Change Since Previous Read/Write]")
}

let muxMessagesTransformDedupsRepeatedReadForTopLevelExecSpec () = promise {
    let reg = createRegistration (minimalMuxDeps ())
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
        check "mux messagesTransform dedups repeated read for top-level exec" (str secondOutput "content" = "[No Change Since Previous Read/Write]")
}

let muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec () = promise {
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let sessionID = "mux-review-accepted-history"
    muxActivateReviewForTest reg sessionID "Ship feature"
    if isNullish tf then
        check "mux messagesTransform exposed for accepted review replay" false
    else
        let accepted = formatReviewResult VibeFs.Kernel.ReviewSession.ReviewResult.Accepted
        let messages =
            [| muxTextMessage "loop-task" "assistant" "---\ntask: Ship feature\n---\nWith-Review Mode is active."
               muxDynamicToolMessage "submit-review" "submit_review" "call-review" (createObj []) (box accepted) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box sessionID ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        check "mux accepted submit_review history clears active review" (not (muxIsReviewActiveForTest reg sessionID))
}

let muxTodoWriteWrapperSchemaSpec () = promise {
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper" false
    else
        let fakeHostTodo =
            box
                {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let schema = get wrapped "parameters"
        let properties = get schema "properties"
        let todosProp = get properties "todos"
        let todoItem = get todosProp "items"
        let todoProps = get todoItem "properties"
        let required = unbox<string[]> (get schema "required")
        let todoRequired = unbox<string[]> (get todoItem "required")
        check "mux todo_write wrapper requires completedWorkReport" (required |> Array.contains "completedWorkReport")
        check "mux todo_write wrapper exposes priority" (not (isNullish (get todoProps "priority")))
        check "mux todo_write wrapper requires priority" (todoRequired |> Array.contains "priority")
}

let muxTodoWriteCapturesCompletedWorkReportSpec () = promise {
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write")
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper for capture" false
    else
        let mutable nativeArgs = null
        let fakeHostTodo =
            box
                {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _opts ->
                        nativeArgs <- args
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let execute = get wrapped "execute"
        let args =
            createObj
                [ "completedWorkReport", box "finished wrapper capture"
                  "todos",
                  box
                      [| createObj [ "content", box "Inspect wrapper"; "status", box "in_progress"; "priority", box "high" ] |] ]
        let! result = (execute $ (args, createObj [ "toolCallId", box "todo-call-1" ])) |> unbox<JS.Promise<obj>>
        let nativeTodos = unbox<obj[]> (get nativeArgs "todos")
        check "mux todo_write wrapper strips completedWorkReport before native execute" (isNullish (get nativeArgs "completedWorkReport"))
        check "mux todo_write wrapper strips priority before native execute" (isNullish (get nativeTodos.[0] "priority"))
        check "mux todo_write wrapper captures completedWorkReport" (tryGetReport opencode "todo-call-1" = Some "finished wrapper capture")
        check "mux todo_write wrapper keeps nudge behavior" ((str result "nudge").Contains "meditator")
}

let muxMagicTodoProjectionSpec () = promise {
    let reg = createRegistration (createObj [])
    let tf = muxMessageTransform reg
    if isNullish tf then
        check "mux messagesTransform exposed for magic todo projection" false
    else
        let todoInput report content status priority =
            createObj
                [ "completedWorkReport", box report
                  "todos", box [| createObj [ "content", box content; "status", box status; "priority", box priority ] |] ]
        let todoOutput count = createObj [ "success", box true; "count", box count ]
        let messages =
            [| muxTextMessage "todo-user-1" "user" "plan phase"
               muxDynamicToolMessage "todo-1" "todo_write" "todo-call-a" (todoInput "planned phase" "Plan change" "in_progress" "high") (todoOutput 1)
               muxTextMessage "todo-user-2" "user" "implemented phase"
               muxDynamicToolMessage "todo-2" "todo_write" "todo-call-b" (todoInput "implemented phase" "Implement change" "completed" "high") (todoOutput 1)
               muxTextMessage "todo-user-3" "user" "verified phase"
               muxDynamicToolMessage "todo-3" "todo_write" "todo-call-c" (todoInput "verified phase" "Verify change" "completed" "medium") (todoOutput 1) |]
        let out = createObj [ "messages", box messages ]
        let input = createObj [ "agent", box "manager"; "sessionID", box "mux-magic-todo-session" ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let transformed = unbox<obj[]> (get out "messages")
        let texts =
            transformed
            |> Array.collect (fun msg ->
                let parts = unbox<obj[]> (get msg "parts")
                parts
                |> Array.choose (fun part -> if str part "type" = "text" then Some (str part "text") else None))
        check "mux magic todo projection injects folded backlog text" (texts |> Array.exists (fun text -> text.Contains foldHeader && text.Contains "planned phase"))
}

let muxExecutorRoCatPrependsWarningSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-executor-ro-warning-"
    let reg = createRegistration (createObj [])
    let executor = muxToolByName reg "executor"
    if isNullish executor then
        check "mux registration exposes executor tool for ro warning" false
    else
        let ctx = createObj [ "directory", box workspaceDir; "workspaceId", box "mux-executor-ro-warning"; "sessionID", box "mux-executor-ro-warning" ]
        let args = createObj [ "language", box "shell"; "program", box "cat /etc/passwd"; "timeout_type", box "short"; "mode", box "ro" ]
        let! result = ((get executor "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        check "mux executor ro cat does not prepend warning" (not (result.StartsWith readOnlyWarning))
    do! rmAsync workspaceDir
}

let muxMeditatorReadsFilesFromCwdSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-meditator-files-"
    let filePath = pathModule?join(workspaceDir, "note.md")
    do! writeFileAsync filePath "deep context"
    let reg = createRegistration (minimalMuxDeps ())
    let meditator = muxToolByName reg "meditator"
    if isNullish meditator then
        check "mux registration exposes meditator tool" false
    else
        let prompts = ResizeArray<string>()
        let taskService =
            createObj
                [ "create",
                  box (System.Func<obj, JS.Promise<obj>>(fun input ->
                      promise {
                          prompts.Add(str input "prompt")
                          return box {| success = true; data = box {| taskId = "meditator-task-1"; kind = "agent" |} |}
                      }))
                  "waitForAgentReport",
                  box (System.Func<string, obj, JS.Promise<obj>>(fun _ _ ->
                      Promise.lift (box {| reportMarkdown = "meditated" |}))) ]
        let ctx = createObj [ "cwd", box workspaceDir; "workspaceId", box "mux-meditator-files"; "taskService", box taskService ]
        let args = createObj [ "intent", box "reason"; "files", box [| "note.md" |] ]
        let! result = ((get meditator "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
        let promptText = if prompts.Count > 0 then prompts.[0] else ""
        check "mux meditator returns subagent report" (result = "meditated")
        check "mux meditator prompt keeps requested relative file path" (promptText.Contains "note.md")
        check "mux meditator prompt includes file content" (promptText.Contains "deep context")
        check "mux meditator prompt does not mark readable file skipped" (not (promptText.Contains "(skipped)"))
    do! rmAsync workspaceDir
}

let muxExecutorModeSchemaSpec () = promise {
    let reg = createRegistration (createObj [])
    let modeSchema = muxExecutorModeSchema reg
    check "mux executor mode schema exists" (not (isNullish modeSchema))
    check "mux executor mode schema enum ro/rw" (enumValues modeSchema = [| "ro"; "rw" |])
    let executor = muxToolByName reg "executor"
    let required = muxToolSchemaRequired executor
    check "mux executor mode is required" (required |> Array.contains "mode")
}

let muxReadToolReturnsContentSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-read-content-"
    let filePath = pathModule?join(workspaceDir, "sample.txt")
    let fileContent = "line1\nline2\nline3\nline4\nline5"
    do! writeFileAsync filePath fileContent
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let fileReadWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")
    if isNullish fileReadWrapper then
        check "mux registration exposes file_read wrapper" false
    else
        let fakeHostRead =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                        promise {
                            let path = str args "path"
                            if path = filePath then
                                return box {| success = true; content = fileContent |}
                            else
                                return box {| success = false; error = "file not found" |}
                        }) |}
        (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
        let readTool = muxToolByName reg "read"
        if isNullish readTool then
            check "mux registration exposes read tool" false
        else
            let ctx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-read-content-session" ]
            let args = createObj [ "path", box filePath ]
            let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "mux read returns non-nullish content" (not (isNullish result))
            check "mux read returns expected line text" (result.Contains "line1" && result.Contains "line5")
            check "mux read does not stringify undefined" (result <> "undefined")
            check "mux read does not stringify object" (not (result.Contains "[object Object]"))
    do! rmAsync workspaceDir
}

let muxReadToolListsDirectoriesSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-read-directory-"
    do! writeFileAsync (pathModule?join(workspaceDir, "note.txt")) "alpha\nbeta"
    let reg = createRegistration (createObj [])
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let fileReadWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "file_read")
    if isNullish fileReadWrapper then
        check "mux registration exposes file_read wrapper for directory read" false
    else
        let fakeHostRead =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _config ->
                        promise {
                            let path = str args "path"
                            if path = workspaceDir then
                                return box {| success = false; error = $"Path is a directory, not a file: {workspaceDir}" |}
                            else
                                return box {| success = true; content = "1\talpha\n2\tbeta" |}
                        }) |}
        (get fileReadWrapper "wrapper") $ (fakeHostRead, createObj []) |> ignore
        let readTool = muxToolByName reg "read"
        if isNullish readTool then
            check "mux registration exposes read tool for directory read" false
        else
            let ctx = createObj [ "directory", box workspaceDir; "sessionID", box "mux-read-directory-session" ]
            let args = createObj [ "path", box workspaceDir ]
            let! result = ((get readTool "execute") $ (ctx, args)) |> unbox<JS.Promise<string>>
            check "mux read returns directory listing" (result.Contains "note.txt" && result.Contains "total 1")
            check "mux read directory does not return file-only error" (not (result.Contains "Path is a directory, not a file"))
    do! rmAsync workspaceDir
}

let run () : JS.Promise<unit> =
    promise {
        let reg = createRegistration (createObj [])
        wrapperSpec reg
        computeCountSpec reg
        let specs : (string * (unit -> JS.Promise<unit>)) list = [
            "buildCapsFileReadData", buildCapsFileReadDataSpec
            "capsTransform", capsTransformSpec
            "capsTransformInPlace", capsTransformInPlaceSpec
            "defaultPreludeWithoutCaps", defaultPreludeWithoutCapsSpec
            "capsAndMagicOrder", capsAndMagicOrderSpec
            "bookkeeperDoesNotReceiveCaps", bookkeeperDoesNotReceiveCapsSpec
            "knowledgeGraphPreludeWithoutCaps", knowledgeGraphPreludeWithoutCapsSpec
            "coderReceivesKnowledgeGraphPrelude", coderReceivesKnowledgeGraphPreludeSpec
            "browserDoesNotReceiveKnowledgeGraphPrelude", browserDoesNotReceiveKnowledgeGraphPreludeSpec
            "executorChildSessionWithoutInputAgentDoesNotReceiveKnowledgeGraphPrelude", executorChildSessionWithoutInputAgentDoesNotReceiveKnowledgeGraphPreludeSpec
            "fetchKnowledgeGraphSnapshot", fetchKnowledgeGraphSnapshotSpec
            "afterHookRecordsDirectWrite", afterHookRecordsDirectWriteSpec
            "afterHookSkipsChildSession", afterHookSkipsChildSessionSpec
            "afterHookSkipsFailedTool", afterHookSkipsFailedToolSpec
            "afterHookRecordsCoder", afterHookRecordsCoderSpec
            "afterHookRecordsExecutor", afterHookRecordsExecutorSpec
            "dailyMaintenanceLaunch", dailyMaintenanceLaunchSpec
            "heartbeatTriggersMaintenance", heartbeatTriggersMaintenanceSpec
            "heartbeatMaintenanceUsesParentSession", heartbeatMaintenanceUsesParentSessionSpec
            "heartbeatSchedulesOnlyEarliestDailyWhileAppendRuns", heartbeatSchedulesOnlyEarliestDailyWhileAppendRunsSpec
            "dailyRewriteTriggersNextDaily", dailyRewriteTriggersNextDailySpec
            "submitKnowledgeGraphAppend", submitKnowledgeGraphAppendSpec
            "submitKnowledgeGraphAppendEmpty", submitKnowledgeGraphAppendEmptySpec
            "submitKnowledgeGraphAppendDoesNotTriggerMaintenance", submitKnowledgeGraphAppendDoesNotTriggerMaintenanceSpec
            "submitKnowledgeGraphSchemaAllowsEmpty", submitKnowledgeGraphSchemaAllowsEmptySpec
            "submitKnowledgeGraphDailyRewrite", submitKnowledgeGraphDailyRewriteSpec
            "submitKnowledgeGraphReconstructsJobFromHistory", submitKnowledgeGraphReconstructsJobFromHistorySpec
            "bookkeeperLaunchCarriesAiSettings", bookkeeperLaunchCarriesAiSettingsSpec
            "bookkeeperFireAndForget", bookkeeperFireAndForgetSpec
            "websearchTriggersBookkeeper", websearchTriggersBookkeeperSpec
            "webfetchTriggersBookkeeper", webfetchTriggersBookkeeperSpec
            "bookkeeperSessionRegisteredInChildAgentRegistry", bookkeeperSessionRegisteredInChildAgentRegistrySpec
            "muxDailyMaintenanceLaunch", muxDailyMaintenanceLaunchSpec
            "muxDailyRewriteTriggersNext", muxDailyRewriteTriggersNextSpec
            "toolDefinition", toolDefinitionSpec
            "toolExecuteBefore", toolExecuteBeforeSpec
            "mimoApplyPatchExecuteBefore", mimoApplyPatchExecuteBeforeSpec
            "mimoTaskExecuteRoundTrip", mimoTaskExecuteRoundTripSpec
            "mimoTaskExecuteNestedReport", mimoTaskExecuteNestedReportSpec
            "mimoTaskExecuteInPlaceStrip", mimoTaskExecuteInPlaceStripSpec
            "mimoTaskExecuteStripsTaskId", mimoTaskExecuteStripsTaskIdSpec
            "mimoTaskDefinitionHandlesZodLikeParameters", mimoTaskDefinitionHandlesZodLikeParametersSpec
            "coderTool", coderToolSpec
            "investigatorTool", investigatorToolSpec
            "investigatorToolLateClientInjection", investigatorToolLateClientInjectionSpec
            "writeTool", (fun () -> writeToolSpec reg)
            "loopCommand", (fun () -> loopCommandSpec reg)
            "agentConfig", agentConfigSpec
            "bookkeeperAgentConfig", bookkeeperAgentConfigSpec
            "disableMimoMemoryAndCheckpoint", disableMimoMemoryAndCheckpointSpec
            "disableMimoMemoryAndCheckpointPreservesUserAgent", disableMimoMemoryAndCheckpointPreservesUserAgentSpec
            "applyAgentConfigForMimoDisablesWorkflow", applyAgentConfigForMimoDisablesWorkflowSpec
            "pluginConfigHookDisablesMimoMemoryAndCheckpoint", pluginConfigHookDisablesMimoMemoryAndCheckpointSpec
            "executorModeSchema", executorModeSchemaSpec
            "executorRejectsInvalidLanguage", executorRejectsInvalidLanguageSpec
            "executorActor", executorActorSpec
            "knowledgeGraphWorkspaceSerialization", knowledgeGraphWorkspaceSerializationSpec
            "knowledgeGraphPortLockTimeout", knowledgeGraphPortLockTimeoutSpec
            "muxFetchKnowledgeGraphSnapshot", muxFetchKnowledgeGraphSnapshotSpec
            "muxReturnBookkeeperAppend", muxReturnBookkeeperAppendSpec
            "muxReturnBookkeeperNoActiveJob", muxReturnBookkeeperNoActiveJobSpec
            "muxReturnBookkeeperReconstructsJobFromHistory", muxReturnBookkeeperReconstructsJobFromHistorySpec
            "muxReturnBookkeeperAppendDoesNotTriggerMaintenance", muxReturnBookkeeperAppendDoesNotTriggerMaintenanceSpec
            "muxDailyRewriteTriggersNext", muxDailyRewriteTriggersNextSpec
            "muxExecutorRwTriggersMaintenance", muxExecutorRwTriggersMaintenanceSpec
            "muxExecutorModeSchema", muxExecutorModeSchemaSpec
            "muxReadToolReturnsContent", muxReadToolReturnsContentSpec
            "muxReadToolListsDirectories", muxReadToolListsDirectoriesSpec
            "muxMessageTransformRegistered", muxMessageTransformRegisteredSpec
            "muxKnowledgeGraphPreludeForManager", muxKnowledgeGraphPreludeForManagerSpec
            "muxKnowledgeGraphPreludeForCoder", muxKnowledgeGraphPreludeForCoderSpec
            "muxNoKnowledgeGraphPreludeForExcludedAgents", muxNoKnowledgeGraphPreludeForExcludedAgentsSpec
            "muxCapsAndKnowledgeGraphPreludeOrder", muxCapsAndKnowledgeGraphPreludeOrderSpec
            "muxSummarization", (fun () -> promise { muxSummarizationSpec () })
            "muxSummarizationToolPolicy", (fun () -> promise { muxSummarizationToolPolicySpec () })
            "muxTopLevelPolicy", muxTopLevelPolicySpec
            "muxTopLevelDedup", muxTopLevelDedupSpec
            "muxMessagesTransformDedupsRepeatedRead", muxMessagesTransformDedupsRepeatedReadSpec
            "muxMessagesTransformDedupsRepeatedFileRead", muxMessagesTransformDedupsRepeatedFileReadSpec
            "muxMessagesTransformDedupsRepeatedReadForTopLevelExec", muxMessagesTransformDedupsRepeatedReadForTopLevelExecSpec
            "muxMessagesTransformAcceptedSubmitReviewEndsLoop", muxMessagesTransformAcceptedSubmitReviewEndsLoopSpec
            "muxTodoWriteWrapperSchema", muxTodoWriteWrapperSchemaSpec
            "muxTodoWriteCapturesCompletedWorkReport", muxTodoWriteCapturesCompletedWorkReportSpec
            "muxMagicTodoProjection", muxMagicTodoProjectionSpec
            "muxExecutorRoCatPrependsWarning", muxExecutorRoCatPrependsWarningSpec
            "muxMeditatorReadsFilesFromCwd", muxMeditatorReadsFilesFromCwdSpec
            "muxSubmitReviewNoActiveReview", muxSubmitReviewNoActiveReviewSpec
            "muxSubmitReviewPromptSuppliesCallId", muxSubmitReviewPromptSuppliesCallIdSpec
            "muxAgentReportWrapperResolvesReviewBySession", muxAgentReportWrapperResolvesReviewBySessionSpec
            "muxSubmitReviewUsesRolledBackHistoryTask", muxSubmitReviewUsesRolledBackHistoryTaskSpec
            "muxLoopReviewPromptUsesFrontMatter", muxLoopReviewPromptUsesFrontMatterSpec
            "muxReturnReviewerRegistered", muxReturnReviewerRegisteredSpec
            "muxReturnReviewerRejectsResolve", muxReturnReviewerRejectsResolveSpec
            "muxReturnReviewerRejectCleansReviewState", muxReturnReviewerRejectCleansReviewStateSpec
            "muxReturnReviewerFirstPassDoubleCheck", muxReturnReviewerFirstPassDoubleCheckSpec
            "muxReturnReviewerSecondPassResolves", muxReturnReviewerSecondPassResolvesSpec
            "muxSubmitReviewTerminatedCleansReviewState", muxSubmitReviewTerminatedCleansReviewStateSpec
            "muxExecutorFailureDoesNotBookkeep", muxExecutorFailureDoesNotBookkeepSpec
        ]
        for (label, spec) in specs do
            do! timedAsync ("IntegrationTool." + label) spec
    }
