module Wanxiangshu.Tests.IntegrationMuxPreludeSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.Dyn


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
    check "mux does not expose return_reviewer tool" (not (names |> Array.contains "return_reviewer"))

let muxMessageTransformRegisteredSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let tf = muxMessageTransform reg
        check "mux registration exposes messagesTransform" (not (isNullish tf))
        check "mux messagesTransform is callable" (typeIs tf "function")
    }

let muxKnowledgeGraphPreludeForManagerSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-manager-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = sharedMuxRegistration ()
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
        check "mux manager knowledge graph prelude preserves original" (msgs |> Array.exists (fun m -> obj.ReferenceEquals(m, originalMsg)))
    do! rmAsync workspaceDir
}

let muxKnowledgeGraphPreludeForCoderSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-coder-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = sharedMuxRegistration ()
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
        check "mux coder knowledge graph prelude preserves original" (msgs |> Array.exists (fun m -> obj.ReferenceEquals(m, originalMsg)))
    do! rmAsync workspaceDir
}

let muxNoKnowledgeGraphPreludeForExcludedAgentsSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-prelude-excluded-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Mux 主入口是 src/Mux/Plugin.fs。" ]
    let reg = sharedMuxRegistration ()
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
    let reg = sharedMuxRegistration ()
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
        check "mux caps+kg preserves original" (msgs |> Array.exists (fun m -> obj.ReferenceEquals(m, originalMsg)))
    do! rmAsync workspaceDir
}

let muxFetchKnowledgeGraphSnapshotSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-kg-fetch-"
    do! ensureKnowledgeGraphDir workspaceDir
    let entity = ["项目"; "入口"]
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [
        knowledgeGraphEntry "0a3f" entity "Old fact 1"
        knowledgeGraphEntry "b912" entity "Old fact 2" ]
    let reg = sharedMuxRegistration ()
    let fetchTool = muxToolByName reg "knowledge_graph_fetch"
    if isNullish fetchTool then
        check "mux registration exposes knowledge_graph_fetch tool" false
    else
        let context = muxToolConfig workspaceDir "mux-kg-fetch-session"
        let! answer = ((get fetchTool "execute") $ (context, createObj [ "entity", box "项目 入口" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch returns concatenated facts" (answer.Contains "Old fact 1" && answer.Contains "Old fact 2")
        let! invalid = ((get fetchTool "execute") $ (context, createObj [ "entity", box "" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch validates entity" (invalid.Contains "Invalid knowledge graph entity")
        let! missing = ((get fetchTool "execute") $ (context, createObj [ "entity", box "missing entity" ])) |> unbox<JS.Promise<string>>
        check "mux knowledge_graph_fetch reports missing entity" (missing.Contains "Knowledge graph entity not found")
    do! rmAsync workspaceDir
}
