module VibeFs.Tests.IntegrationKnowledgeGraphPreludeSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup

open VibeFs.Kernel.Message
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Kernel.KnowledgeGraph.Types
open VibeFs.Kernel.KnowledgeGraph.Codec
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Opencode.Plugin
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn

let knowledgeGraphPreludeWithoutCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-prelude-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-1"; "agent", box "manager"; "sessionID", box "kg-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "knowledge graph prelude injects synthetic messages without caps" (msgs.Length = 2)
    let firstParts = unbox<obj[]> (get msgs.[0] "parts")
    let firstText = str firstParts.[0] "text"
    check "knowledge graph prelude is front matter" (firstText.Contains "---\nknowledge_graph:")
    check "knowledge graph prelude includes entity" (firstText.Contains "项目" && firstText.Contains "插件入口" && not (firstText.Contains "0a3f"))
    check "knowledge graph prelude hides facts" (not (firstText.Contains "src/Opencode/Plugin.fs"))
    check "knowledge graph prelude injects Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
    check "knowledge graph prelude preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let coderReceivesKnowledgeGraphPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-prelude-coder-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-coder-1"; "agent", box "coder"; "sessionID", box "kg-coder-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "coder" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "coder knowledge graph prelude injects synthetic messages" (msgs.Length = 2)
    check "coder knowledge graph prelude has Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
    check "coder knowledge graph prelude is front matter" (firstText.Contains "---\nknowledge_graph:")
    check "coder knowledge graph prelude includes entity" (firstText.Contains "项目" && firstText.Contains "插件入口" && not (firstText.Contains "0a3f"))
    check "coder knowledge graph prelude hides facts" (not (firstText.Contains "src/Opencode/Plugin.fs"))
    check "coder knowledge graph prelude preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let browserDoesNotReceiveKnowledgeGraphPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-prelude-browser-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-browser-1"; "agent", box "browser"; "sessionID", box "kg-browser-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "browser" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "browser still receives injection" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "browser injection omits knowledge graph prelude" (not (firstText.Contains "knowledge_graph"))
    check "browser injection has Kolmolgorov prelude content" (firstText.Contains "# Kolmolgorov 宝典")
    check "browser injection preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let executorChildSessionWithoutInputAgentDoesNotReceiveKnowledgeGraphPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-prelude-executor-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目"; "插件入口"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-executor-1"; "agent", box "executor"; "sessionID", box "child-executor-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "executor child without input agent still gets default prefix" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "executor child without input agent omits knowledge graph prelude" (not (firstText.Contains "knowledge_graph"))
    check "executor child without input agent preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let fetchKnowledgeGraphSnapshotSpec () = promise {
    let! workspaceDir = mkdtempAsync "kg-fetch-"
    do! ensureKnowledgeGraphDir workspaceDir
    let dayFile = dayPath workspaceDir "2026-06-14"
    let entity = ["项目"; "入口"]
    do! writeFileAsync dayFile (renderNdjson (DayHeader("2026-06-14", true)) [
        knowledgeGraphEntry "0a3f" entity "Old answer"
        knowledgeGraphEntry "b912" entity "Old answer 2" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let managerMsg = box {| info = createObj [ "id", box "msg-fetch"; "agent", box "manager"; "sessionID", box "kg-fetch-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| managerMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    do! writeFileAsync dayFile (renderNdjson (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "c001" entity "New answer" ])
    let tools = get p "tool"
    let fetchTool = get tools "knowledge_graph_fetch"
    let context = createObj [ "directory", box workspaceDir; "sessionID", box "kg-fetch-session" ]
    let! oldAnswer = (get fetchTool "execute") $ (createObj [ "entity", box "项目 入口" ], context) |> unbox<JS.Promise<string>>
    check "knowledge_graph_fetch returns concatenated cached facts" (oldAnswer.Contains "Old answer" && oldAnswer.Contains "Old answer 2" && not (oldAnswer.Contains "New answer"))
    let! invalid = (get fetchTool "execute") $ (createObj [ "entity", box "" ], context) |> unbox<JS.Promise<string>>
    check "knowledge_graph_fetch validates entity" (invalid.Contains "Invalid knowledge graph entity")
    let! missing = (get fetchTool "execute") $ (createObj [ "entity", box "missing entity" ], context) |> unbox<JS.Promise<string>>
    check "knowledge_graph_fetch reports missing entity" (missing.Contains "Knowledge graph entity not found")
    do! rmAsync workspaceDir
}
