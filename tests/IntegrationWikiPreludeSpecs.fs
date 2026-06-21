module VibeFs.Tests.IntegrationWikiPreludeSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.WikiRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let wikiPreludeWithoutCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-prelude-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-1"; "agent", box "manager"; "sessionID", box "wiki-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "wiki prelude injects synthetic messages without caps" (msgs.Length = 2)
    let firstParts = unbox<obj[]> (get msgs.[0] "parts")
    let firstText = str firstParts.[0] "text"
    check "wiki prelude is front matter" (firstText.Contains "---\nwiki:")
    check "wiki prelude includes question" (firstText.Contains "0a3f" && firstText.Contains "项目插件入口在哪里？")
    check "wiki prelude hides answers" (not (firstText.Contains "src/Opencode/Plugin.fs"))
    check "wiki prelude injects think-wrapped content" (firstText.Contains "<think>")
    check "wiki prelude preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let coderReceivesWikiPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-prelude-coder-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-coder-1"; "agent", box "coder"; "sessionID", box "wiki-coder-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "coder" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "coder wiki prelude injects synthetic messages" (msgs.Length = 2)
    check "coder wiki prelude has think-wrapped content" (firstText.Contains "<think>")
    check "coder wiki prelude is front matter" (firstText.Contains "---\nwiki:")
    check "coder wiki prelude includes question" (firstText.Contains "0a3f" && firstText.Contains "项目插件入口在哪里？")
    check "coder wiki prelude hides answers" (not (firstText.Contains "src/Opencode/Plugin.fs"))
    check "coder wiki prelude preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let browserDoesNotReceiveWikiPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-prelude-browser-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-browser-1"; "agent", box "browser"; "sessionID", box "wiki-browser-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "browser" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "browser still receives injection" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "browser injection omits wiki prelude" (not (firstText.Contains "[项目背景和历史]"))
    check "browser injection has think-wrapped content" (firstText.Contains "<think>")
    check "browser injection preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let executorChildSessionWithoutInputAgentDoesNotReceiveWikiPreludeSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-prelude-executor-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-executor-1"; "agent", box "executor"; "sessionID", box "child-executor-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "executor child without input agent still gets default prefix" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "executor child without input agent omits wiki prelude" (not (firstText.Contains "---\nwiki:"))
    check "executor child without input agent preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let fetchWikiSnapshotSpec () = promise {
    let! workspaceDir = mkdtempAsync "wiki-fetch-"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Old answer" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let managerMsg = box {| info = createObj [ "id", box "msg-fetch"; "agent", box "manager"; "sessionID", box "wiki-fetch-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| managerMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "New answer" ])
    let tools = get p "tool"
    let fetchTool = get tools "fetch_wiki"
    let context = createObj [ "directory", box workspaceDir; "sessionID", box "wiki-fetch-session" ]
    let! oldAnswer = (get fetchTool "execute") $ (createObj [ "id", box "0a3f" ], context) |> unbox<JS.Promise<string>>
    check "fetch_wiki returns snapshot answer" (oldAnswer = "Old answer")
    let! invalidId = (get fetchTool "execute") $ (createObj [ "id", box "nope" ], context) |> unbox<JS.Promise<string>>
    check "fetch_wiki validates id format" (invalidId.Contains "Invalid wiki id")
    let! missing = (get fetchTool "execute") $ (createObj [ "id", box "b912" ], context) |> unbox<JS.Promise<string>>
    check "fetch_wiki reports missing snapshot entry" (missing.Contains "Wiki entry not found in this session snapshot")
    do! rmAsync workspaceDir
}
