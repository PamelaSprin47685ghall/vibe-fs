module Wanxiangshu.Tests.IntegrationCapsSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup

open Wanxiangshu.Kernel.CapsSynthPolicy
open Wanxiangshu.Kernel.Message
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Mux.AiSettings
open Wanxiangshu.Shell.ChildAgentRegistry
open Wanxiangshu.Shell.Dyn

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
    check "caps transform injects messages" (msgs.Length = 4)
    check "caps transform preserves original" (obj.ReferenceEquals(msgs.[3], originalMsg))
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
    check "default prelude injects synthetic messages without caps" (msgs.Length = 3)
    let userParts = unbox<obj[]> (get msgs.[0] "parts")
    check "default prelude injects Kolmolgorov prelude content" ((str userParts.[0] "text").StartsWith "# Kolmolgorov 宝典")
    check "default prelude preserves original message" (obj.ReferenceEquals(msgs.[2], originalMsg))
    do! rmAsync workspaceDir
}

let private todoInput (report: string) : obj =
    createObj [ "ahaMoments", box report; "changesAndReasons", box ""; "gotchas", box ""; "lessonsAndConventions", box ""; "plan", box ""; "todos", box [||] ]

let private todoState (report: string) : obj =
    createObj [ "status", box "completed"; "input", box (todoInput report); "output", box "Todos updated." ]

let private todoMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : obj =
    box {| info = createObj [ "id", box id; "role", box "assistant"; "sessionID", box "test"; "time", box (createObj [ "created", box created; "completed", box completed ]) ]
           parts = [| createObj [ "type", box "tool"; "tool", box "todowrite"; "callID", box callID; "state", box (todoState report) ] |] |}

let capsAndBacklogOrderSpec () = promise {
    let! workspaceDir = mkdtempAsync "caps-magic-order-"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"
    do! writeFileAsync (unbox<string> (pathModule?join(workspaceDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
    let! p = plugin (box {| directory = workspaceDir |})
    let tf = get p "experimental.chat.messages.transform"
    let messages = createObj [ "messages", box [|
        box {| info = createObj [ "id", box "u1"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "start" |} |] |}
        todoMsg "m1" "c1" "R1" 123 456
        box {| info = createObj [ "id", box "u2"; "role", box "user"; "sessionID", box "test" ]
               parts = [| box {| ``type`` = "text"; text = "please fix this bug" |} |] |}
        todoMsg "m2" "c2" "R2" 789 790
        todoMsg "m3" "c3" "R3" 791 792
    |] ]
    let todoInput report content status priority =
        { Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.AhaMoments = report
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.ChangesAndReasons = report + "_changes"
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Gotchas = report + "_gotchas"
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.LessonsAndConventions = report + "_lessons"
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Plan = report + "_plan"
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Todos = [| { Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Content = content; Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Status = status; Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Priority = priority } |]
          Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.SelectMethodology = [] }
    do! Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail workspaceDir "test" (todoInput "R1" "t1" "completed" "high")
    do! Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail workspaceDir "test" (todoInput "R2" "t2" "completed" "high")
    do! Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail workspaceDir "test" (todoInput "R3" "t3" "completed" "high")
    do! tf $ (createObj [ "sessionID", box "test"; "directory", box workspaceDir ], messages) |> unbox<JS.Promise<unit>>
    let result = unbox<obj[]> (get messages "messages")
    let userParts = unbox<obj[]> (get result.[0] "parts")
    let ackInfo = get result.[1] "info"
    let capsAssistantInfo = get result.[2] "info"
    let magicInfo = get result.[3] "info"
    let magicId : string = str magicInfo "id"
    check "caps/backlog order: caps user first" ((str userParts.[0] "text").StartsWith "# Kolmolgorov 宝典")
    check "caps/backlog order: caps ack assistant second" ((str ackInfo "id").StartsWith(capsAcknowledgePrefix : string))
    check "caps/backlog order: caps read assistant third" ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix : string))
    check "caps/backlog order: backlog prefix fourth" (magicId.StartsWith(backlogPrefixIdPrefix : string))
    do! rmAsync workspaceDir
}


