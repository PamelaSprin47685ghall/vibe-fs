module VibeFs.Tests.IntegrationCapsSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.Message
open VibeFs.Kernel.Wiki
open VibeFs.Mux.Plugin
open VibeFs.Opencode.CapsPrelude
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.WikiRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles

let buildCapsFileReadDataSpec () = promise {
    let! tmpDir = mkdtempAsync "caps-test-"
    do! writeFileAsync (unbox<string> (pathModule?join(tmpDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(tmpDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    let! entries = buildCapsFileReadData tmpDir
    check "buildCapsFileReadData finds caps file" (entries.Length = 1)
    check "caps entry has path" (entries.[0].path = "CAPS.md")
    check "caps entry callId prefix" (entries.[0].callId.StartsWith "caps-fr-")
    check "caps entry output has content" (entries.[0].output.content.Contains "Test content")
    do! rmAsync tmpDir
}

let capsTransformSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-transform-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg =
        box {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]
               parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "caps transform injects four messages" (msgs.Length = 5)
    check "caps transform preserves original" (obj.ReferenceEquals(msgs.[4], originalMsg))
    do! rmAsync workspaceDir
}

let capsTransformInPlaceSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-in-place-"
    let freshOut = createObj [ "messages", box [| box {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]; parts = [||] |} |] ]
    let freshRef = get freshOut "messages"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    let! p = plugin (box {| directory = workspaceDir |})
    do! (get p "experimental.chat.messages.transform") $ (createObj [], freshOut) |> unbox<JS.Promise<unit>>
    check "caps transform mutates array in place" (obj.ReferenceEquals(get freshOut "messages", freshRef))
    do! rmAsync workspaceDir
}

let defaultPreludeWithoutCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-default-prelude-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg =
        box {| info = createObj [ "id", box "msg-default"; "agent", box "manager"; "sessionID", box "default-session" ]
               parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "default prelude injects synthetic messages without caps or wiki" (msgs.Length = 4)
    let thinkingParts = unbox<obj[]> (get msgs.[1] "parts")
    let contextParts = unbox<obj[]> (get msgs.[2] "parts")
    check "default prelude injects thinking without caps or wiki" (str thinkingParts.[0] "type" = "reasoning" && str thinkingParts.[0] "text" = thinkText)
    check "default prelude injects llm text without caps or wiki" (str contextParts.[0] "type" = "text" && str contextParts.[0] "text" = llmText)
    check "default prelude preserves original message" (obj.ReferenceEquals(msgs.[3], originalMsg))
    do! rmAsync workspaceDir
}

let capsAndMagicOrderSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-magic-order-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [|
        box {| info = createObj [ "id", box "u1"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "start" |} |] |}
        box {| info = createObj [ "id", box "m1"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 123; "completed", box 456 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c1"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R1"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
        box {| info = createObj [ "id", box "u2"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "please fix this bug" |} |] |}
        box {| info = createObj [ "id", box "m2"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 789; "completed", box 790 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c2"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R2"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
        box {| info = createObj [ "id", box "m3"; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box 791; "completed", box 792 ]) ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "todowrite"
                   "callID", box "c3"
                   "state", box (createObj [
                       "status", box "completed"
                       "input", box (createObj [ "completedWorkReport", box "R3"; "todos", box [||] ])
                       "output", box "Todos updated."
                   ])
               ] |] |}
    |] ]
    do! tf $ (createObj [], messages) |> unbox<JS.Promise<unit>>
    let result = unbox<obj[]> (get messages "messages")
    let capsParts = unbox<obj[]> (get result.[0] "parts")
    let thinkingParts = unbox<obj[]> (get result.[1] "parts")
    let contextParts = unbox<obj[]> (get result.[2] "parts")
    let capsAssistantInfo = get result.[3] "info"
    let magicInfo = get result.[4] "info"
    let magicId : string = str magicInfo "id"
    check "caps/magic order: caps user first" ((str capsParts.[0] "text").StartsWith "你好")
    check "caps/magic order: thinking second" (str thinkingParts.[0] "type" = "reasoning" && str thinkingParts.[0] "text" = thinkText)
    check "caps/magic order: llm text third" (str contextParts.[0] "type" = "text" && str contextParts.[0] "text" = llmText)
    check "caps/magic order: caps read assistant fourth" ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix : string))
    check "caps/magic order: magic prefix fifth" (magicId.StartsWith(magicTodoPrefixPrefix : string))
    do! rmAsync workspaceDir
}

let bookkeeperDoesNotReceiveCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-bookkeeper-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    do! unbox<JS.Promise<unit>> (fsAsync?mkdir(pathModule?join(workspaceDir, "wiki"), box {| recursive = true |}))
    let snapshotFile = unbox<string> (pathModule?join(workspaceDir, "wiki", "snapshot.ndjson"))
    do! writeFileAsync snapshotFile (renderNdjson (SnapshotHeader(Some "2026-06-14")) [ wikiEntry "0a3f" "项目插件入口在哪里？" "Opencode 主入口是 src/Opencode/Plugin.fs。" ])
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-bk-1"; "agent", box "bookkeeper"; "sessionID", box "caps-bk-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "bookkeeper" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "bookkeeper still receives thinking+assistant injection without caps files" (msgs.Length = 4)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "bookkeeper injection omits wiki prelude" (not (firstText.Contains "[项目背景和历史]"))
    check "bookkeeper injection has thinking" (str (unbox<obj[]> (get msgs.[1] "parts")).[0] "type" = "reasoning")
    check "bookkeeper injection has llm text" (str (unbox<obj[]> (get msgs.[2] "parts")).[0] "type" = "text")
    check "bookkeeper injection preserves original message" (obj.ReferenceEquals(msgs.[3], originalMsg))
    do! rmAsync workspaceDir
}
