module VibeFs.Tests.IntegrationCapsSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup

open VibeFs.Kernel.Message
open VibeFs.Kernel.KnowledgeGraph
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Opencode.KnowledgeGraphRuntime
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn

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
    check "caps transform injects messages" (msgs.Length = 3)
    check "caps transform preserves original" (obj.ReferenceEquals(msgs.[2], originalMsg))
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
    check "default prelude injects synthetic messages without caps or knowledge graph" (msgs.Length = 2)
    let userParts = unbox<obj[]> (get msgs.[0] "parts")
    check "default prelude injects Kolmolgorov prelude content" ((str userParts.[0] "text").StartsWith "# Kolmolgorov 宝典")
    check "default prelude preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
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
    let userParts = unbox<obj[]> (get result.[0] "parts")
    let capsAssistantInfo = get result.[1] "info"
    let magicInfo = get result.[2] "info"
    let magicId : string = str magicInfo "id"
    check "caps/magic order: caps user first" ((str userParts.[0] "text").StartsWith "# Kolmolgorov 宝典")
    check "caps/magic order: caps read assistant second" ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix : string))
    check "caps/magic order: magic prefix third" (magicId.StartsWith(magicTodoPrefixPrefix : string))
    do! rmAsync workspaceDir
}

let bookkeeperDoesNotReceiveCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-bookkeeper-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目插件入口在哪里？"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-bk-1"; "agent", box "bookkeeper"; "sessionID", box "caps-bk-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [ "agent", box "bookkeeper" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "bookkeeper still receives injection without caps files" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "bookkeeper injection omits knowledge graph prelude" (not (firstText.Contains "[项目背景和历史]"))
    check "bookkeeper injection has Kolmolgorov prelude content" (firstText.StartsWith "# Kolmolgorov 宝典")
    check "bookkeeper injection preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let compactionDoesNotReceiveCapsSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-compaction-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-14") (DayHeader("2026-06-14", true)) [ knowledgeGraphEntry "0a3f" ["项目插件入口在哪里？"] "Opencode 主入口是 src/Opencode/Plugin.fs。" ]
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let originalMsg = box {| info = createObj [ "id", box "msg-compaction-1"; "agent", box "compaction"; "sessionID", box "caps-compaction-session" ]; parts = [||] |}
    let out = createObj [ "messages", box [| originalMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    check "compaction receives only default prefix and original message" (msgs.Length = 2)
    let firstText = str (unbox<obj[]> (get msgs.[0] "parts")).[0] "text"
    check "compaction injection omits knowledge graph prelude" (not (firstText.Contains "knowledge_graph"))
    check "compaction injection has Kolmolgorov prelude content" (firstText.StartsWith "# Kolmolgorov 宝典")
    check "compaction injection preserves original message" (obj.ReferenceEquals(msgs.[1], originalMsg))
    do! rmAsync workspaceDir
}

let opencodeMethodologyProbeSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-methodology-probe-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    for agent in [| "manager"; "coder"; "reviewer"; "meditator"; "build"; "plan" |] do
        let userMsg =
            box {| info = createObj [ "id", box ("msg-user-" + agent); "agent", box agent; "role", box "user"; "sessionID", box ("probe-" + agent) ]
                   parts = [| box {| ``type`` = "text"; text = "do the task" |} |] |}
        let out = createObj [ "messages", box [| userMsg |] ]
        do! tf $ (createObj [ "agent", box agent; "sessionID", box ("probe-" + agent) ], out) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        let lastMsg = msgs.[msgs.Length - 1]
        let lastInfo = get lastMsg "info"
        let lastId = str lastInfo "id"
        check (agent + " receives opencode methodology probe") (lastId.StartsWith "methodology-probe-")
        let parts = get lastMsg "parts"
        let firstPart = (unbox<obj[]> parts).[0]
        let probeText = str firstPart "text"
        check (agent + " probe mentions select_methodology") (probeText.Contains "select_methodology")
    do! rmAsync workspaceDir
}

let opencodeMethodologyProbeBuildWithoutInputAgentSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-methodology-probe-build-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let userMsg =
        box {| info = createObj [ "id", box "msg-user-build"; "agent", box "build"; "role", box "user"; "sessionID", box "probe-build" ]
               parts = [| box {| ``type`` = "text"; text = "do the task" |} |] |}
    let out = createObj [ "messages", box [| userMsg |] ]
    do! tf $ (createObj [], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let lastMsg = msgs.[msgs.Length - 1]
    let lastInfo = get lastMsg "info"
    let lastId = str lastInfo "id"
    check "build without input agent receives methodology probe" (lastId.StartsWith "methodology-probe-")
    let parts = get lastMsg "parts"
    let firstPart = (unbox<obj[]> parts).[0]
    let probeText = str firstPart "text"
    check "build without input agent probe mentions select_methodology" (probeText.Contains "select_methodology")
    do! rmAsync workspaceDir
}

let opencodeMethodologyProbeSuppressedSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-methodology-suppressed-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let userMsg =
        box {| info = createObj [ "id", box "msg-user"; "agent", box "manager"; "role", box "user"; "sessionID", box "suppress-session" ]
               parts = [| box {| ``type`` = "text"; text = "do the task" |} |] |}
    let methodologyResult =
        box {| info = createObj [ "id", box "msg-method"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "suppress-session" ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "select_methodology"
                   "callID", box "call-1"
                   "state", box (createObj [ "status", box "completed"; "output", box "Continue using the selected methodologies." ])
               ] |] |}
    let out = createObj [ "messages", box [| userMsg; methodologyResult |] ]
    do! tf $ (createObj [ "agent", box "manager"; "sessionID", box "suppress-session" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let hasProbe = msgs |> Array.exists (fun m -> (str (get m "info") "id").StartsWith "methodology-probe-")
    check "opencode methodology probe suppressed after completed call" (not hasProbe)
    do! rmAsync workspaceDir
}

let opencodeMethodologyProbeExcludedAgentsSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-methodology-excluded-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    for agent in [| "compaction"; "title"; "browser"; "bookkeeper"; "investigator"; "executor" |] do
        let userMsg =
            box {| info = createObj [ "id", box ("msg-" + agent); "agent", box agent; "role", box "user"; "sessionID", box ("excl-" + agent) ]
                   parts = [| box {| ``type`` = "text"; text = "do the task" |} |] |}
        let out = createObj [ "messages", box [| userMsg |] ]
        do! tf $ (createObj [ "agent", box agent; "sessionID", box ("excl-" + agent) ], out) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        let hasProbe = msgs |> Array.exists (fun m -> (str (get m "info") "id").StartsWith "methodology-probe-")
        check ("opencode " + agent + " does not receive methodology probe") (not hasProbe)
    do! rmAsync workspaceDir
}

let opencodeMethodologyProbeStrippedSpec () = promise {
    let! workspaceDir = mkdtempAsync "opencode-methodology-stripped-"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let userMsg =
        box {| info = createObj [ "id", box "msg-user"; "agent", box "manager"; "role", box "user"; "sessionID", box "strip-session" ]
               parts = [| box {| ``type`` = "text"; text = "do the task" |} |] |}
    let methodologyResult =
        box {| info = createObj [ "id", box "msg-method"; "agent", box "manager"; "role", box "assistant"; "sessionID", box "strip-session" ]
               parts = [| createObj [
                   "type", box "tool"
                   "tool", box "select_methodology"
                   "callID", box "call-1"
                   "state", box (createObj [ "status", box "completed"; "output", box "Continue using the selected methodologies." ])
               ] |] |}
    let staleProbe =
        box {| info = createObj [ "id", box "methodology-probe-1"; "agent", box "manager"; "role", box "user"; "sessionID", box "strip-session" ]
               parts = [| box {| ``type`` = "text"; text = "stale probe" |} |] |}
    let out = createObj [ "messages", box [| userMsg; methodologyResult; staleProbe |] ]
    do! tf $ (createObj [ "agent", box "manager"; "sessionID", box "strip-session" ], out) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let hasProbe = msgs |> Array.exists (fun m -> (str (get m "info") "id").StartsWith "methodology-probe-")
    check "opencode methodology probe stripped on re-projection" (not hasProbe)
    do! rmAsync workspaceDir
}
