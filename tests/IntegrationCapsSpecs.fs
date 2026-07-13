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
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.Dyn

module Dyn = Wanxiangshu.Shell.Dyn

open Wanxiangshu.Omp
open Wanxiangshu.Omp.MessageTransform

let buildCapsFileReadDataSpec () =
    promise {
        let! tmpDir = mkdtempAsync "caps-test-"
        do! writeFileAsync (unbox<string> (pathModule?join (tmpDir, "CAPS.md"))) "# Capabilities\nTest content"
        do! writeFileAsync (unbox<string> (pathModule?join (tmpDir, "AGENTS.md"))) "---\nimport:\n  - CAPS.md\n---\n"
        let! entries = buildCapsFileReadData tmpDir
        check "buildCapsFileReadData finds caps file" (entries.Length = 1)
        check "caps entry has path" (entries.[0].path = "CAPS.md")
        check "caps entry callId prefix" (entries.[0].callId.StartsWith "caps-fr-")
        check "caps entry output has content" (entries.[0].output.content.Contains "Test content")
        do! rmAsync tmpDir
    }

let capsTransformSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "caps-transform-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"

        do!
            writeFileAsync
                (unbox<string> (pathModule?join (workspaceDir, "AGENTS.md")))
                "---\nimport:\n  - CAPS.md\n---\n"

        let! p = plugin (box {| directory = workspaceDir |})
        let tf = get p "experimental.chat.messages.transform"

        let originalMsg =
            box
                {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]
                   parts = [||] |}

        let out = createObj [ "messages", box [| originalMsg |] ]
        do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        check "caps transform injects messages" (msgs.Length = 4)
        check "caps transform preserves original" (obj.ReferenceEquals(msgs.[3], originalMsg))

        let ackParts = unbox<obj[]> (get msgs.[1] "parts")
        check "caps ack assistant first part is text" ((str ackParts.[0] "type") = "text")
        check "caps ack assistant first part text non-empty" ((str ackParts.[0] "text").Length > 0)

        let readParts = unbox<obj[]> (get msgs.[2] "parts")
        check "caps read assistant first part is text" ((str readParts.[0] "type") = "text")
        check "caps read assistant first part text non-empty" ((str readParts.[0] "text").Length > 0)

        do! rmAsync workspaceDir
    }

let capsTransformInPlaceSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "caps-in-place-"

        let freshOut =
            createObj
                [ "messages",
                  box
                      [| box
                             {| info = createObj [ "id", box "msg-1"; "agent", box "manager" ]
                                parts = [||] |} |] ]

        let freshRef = get freshOut "messages"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"

        do!
            writeFileAsync
                (unbox<string> (pathModule?join (workspaceDir, "AGENTS.md")))
                "---\nimport:\n  - CAPS.md\n---\n"

        let! p = plugin (box {| directory = workspaceDir |})

        do!
            (get p "experimental.chat.messages.transform") $ (createObj [], freshOut)
            |> unbox<JS.Promise<unit>>

        check "caps transform mutates array in place" (obj.ReferenceEquals(get freshOut "messages", freshRef))
        do! rmAsync workspaceDir
    }

let defaultPreludeWithoutCapsSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "caps-default-prelude-"
        let! p = plugin (box {| directory = workspaceDir |})
        let tf = get p "experimental.chat.messages.transform"

        let originalMsg =
            box
                {| info =
                    createObj
                        [ "id", box "msg-default"
                          "agent", box "manager"
                          "sessionID", box "default-session" ]
                   parts = [||] |}

        let out = createObj [ "messages", box [| originalMsg |] ]
        do! tf $ (createObj [ "agent", box "manager" ], out) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        check "default prelude injects synthetic messages without caps" (msgs.Length = 3)
        let userParts = unbox<obj[]> (get msgs.[0] "parts")

        check
            "default prelude injects Kolmogorov prelude content"
            ((str userParts.[0] "text").StartsWith "# Kolmogorov 宝典")

        check "default prelude preserves original message" (obj.ReferenceEquals(msgs.[2], originalMsg))
        do! rmAsync workspaceDir
    }

let private mockUserMsg (id: string) (sessionID: string) (prompt: string) : obj =
    let info =
        createObj [ "id", box id; "role", box "user"; "sessionID", box sessionID ]

    let parts = [| createObj [ "type", box "text"; "text", box prompt ] |]
    createObj [ "info", box info; "parts", box parts ]

let private todoInput (report: string) : obj =
    createObj
        [ "ahaMoments", box report
          "changesAndReasons", box ""
          "gotchas", box ""
          "lessonsAndConventions", box ""
          "plan", box ""
          "todos", box [||] ]

let private todoState (report: string) : obj =
    createObj
        [ "status", box "completed"
          "input", box (todoInput report)
          "output", box "Todos updated." ]

let private todoMsg (id: string) (callID: string) (report: string) (created: int) (completed: int) : obj =
    let info =
        createObj
            [ "id", box id
              "role", box "assistant"
              "sessionID", box "test"
              "time", box (createObj [ "created", box created; "completed", box completed ]) ]

    let parts =
        [| createObj
               [ "type", box "tool"
                 "tool", box "todowrite"
                 "callID", box callID
                 "state", box (todoState report) ] |]

    createObj [ "info", box info; "parts", box parts ]

let capsAndBacklogOrderSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "caps-magic-order-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"

        do!
            writeFileAsync
                (unbox<string> (pathModule?join (workspaceDir, "AGENTS.md")))
                "---\nimport:\n  - CAPS.md\n---\n"

        let! p = plugin (box {| directory = workspaceDir |})
        let tf = get p "experimental.chat.messages.transform"

        let messages =
            createObj
                [ "messages",
                  box
                      [| mockUserMsg "u1" "test" "start"
                         todoMsg "m1" "c1" "R1" 123 456
                         mockUserMsg "u2" "test" "please fix this bug"
                         todoMsg "m2" "c2" "R2" 789 790
                         todoMsg "m3" "c3" "R3" 791 792 |] ]

        let todoInput report content status priority =
            { Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.AhaMoments = report
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.ChangesAndReasons = report + "_changes"
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Gotchas = report + "_gotchas"
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.LessonsAndConventions = report + "_lessons"
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Plan = report + "_plan"
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.Todos =
                [| { Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Content = content
                     Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Status =
                       Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.Completed
                     Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoItem.Priority =
                       Wanxiangshu.Kernel.ToolArgs.TodoItemPriority.High } |]
              Wanxiangshu.Shell.WorkBacklogToolsCodec.TodoWriteArgs.SelectMethodology = [] }

        do!
            Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail
                workspaceDir
                "test"
                (todoInput "R1" "t1" "completed" "high")

        do!
            Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail
                workspaceDir
                "test"
                (todoInput "R2" "t2" "completed" "high")

        do!
            Wanxiangshu.Shell.EventLogRuntime.appendWorkBacklogCommittedOrFail
                workspaceDir
                "test"
                (todoInput "R3" "t3" "completed" "high")

        do!
            tf
            $ (createObj [ "sessionID", box "test"; "directory", box workspaceDir ], messages)
            |> unbox<JS.Promise<unit>>

        let result = unbox<obj[]> (get messages "messages")
        let userParts = unbox<obj[]> (get result.[0] "parts")
        let ackInfo = get result.[1] "info"
        let capsAssistantInfo = get result.[2] "info"
        let magicInfo = get result.[3] "info"
        let magicId: string = str magicInfo "id"
        check "caps/backlog order: caps user first" ((str userParts.[0] "text").StartsWith "# Kolmogorov 宝典")

        check
            "caps/backlog order: caps ack assistant second"
            ((str ackInfo "id").StartsWith(capsAcknowledgePrefix: string))

        check
            "caps/backlog order: caps read assistant third"
            ((str capsAssistantInfo "id").StartsWith(capsSynthAssistantPrefix: string))

        check "caps/backlog order: backlog prefix fourth" (magicId.StartsWith(backlogPrefixIdPrefix: string))

        let ackParts = unbox<obj[]> (get result.[1] "parts")
        check "caps/backlog order: ack assistant first part is text" ((str ackParts.[0] "type") = "text")
        check "caps/backlog order: ack assistant first part text non-empty" ((str ackParts.[0] "text").Length > 0)

        let readParts = unbox<obj[]> (get result.[2] "parts")
        check "caps/backlog order: read assistant first part is text" ((str readParts.[0] "type") = "text")
        check "caps/backlog order: read assistant first part text non-empty" ((str readParts.[0] "text").Length > 0)

        do! rmAsync workspaceDir
    }

let capsEpochIsolationAndStabilitySpecs () =
    promise {
        let! workspaceDir = mkdtempAsync "caps-epoch-isolation-"
        do! writeFileAsync (unbox<string> (pathModule?join (workspaceDir, "CAPS.md"))) "# Capabilities\nTest content"

        do!
            writeFileAsync
                (unbox<string> (pathModule?join (workspaceDir, "AGENTS.md")))
                "---\nimport:\n  - CAPS.md\n---\n"

        let! p = plugin (box {| directory = workspaceDir |})
        let tf = get p "experimental.chat.messages.transform"

        // 1. 隔离性测试：两个无 sessionID（均设为 ""），但有不同首个 native user message ID 的对话生成不同 CAPS epoch
        let messagesA =
            createObj [ "messages", box [| mockUserMsg "msg-user-a" "" "start a" |] ]

        let messagesB =
            createObj [ "messages", box [| mockUserMsg "msg-user-b" "" "start b" |] ]

        do!
            tf
            $ (createObj [ "sessionID", box ""; "directory", box workspaceDir ], messagesA)
            |> unbox<JS.Promise<unit>>

        do!
            tf
            $ (createObj [ "sessionID", box ""; "directory", box workspaceDir ], messagesB)
            |> unbox<JS.Promise<unit>>

        let resultA = unbox<obj[]> (get messagesA "messages")
        let resultB = unbox<obj[]> (get messagesB "messages")

        let ackIdA = str (get resultA.[1] "info") "id"
        let ackIdB = str (get resultB.[1] "info") "id"

        let epochA = ackIdA.Substring(capsAcknowledgePrefix.Length)
        let epochB = ackIdB.Substring(capsAcknowledgePrefix.Length)

        check "epochA is not empty" (epochA <> "")
        check "epochB is not empty" (epochB <> "")
        check "epochs for different conversation keys are isolated" (epochA <> epochB)

        // 验证业务 sessionID 没有被改写为 epoch-*。
        let nativeMsgAInfo = get resultA.[3] "info"
        let nativeSessionID_A = str nativeMsgAInfo "sessionID"
        check "native message sessionID remains empty" (nativeSessionID_A = "")

        // 2. 连续性测试：同一对话从无 sessionID 到后来出现真实 sessionID 时，synthetic CAPS ID 保持相同 epoch
        let messagesC_1 =
            createObj [ "messages", box [| mockUserMsg "msg-user-c" "" "start c" |] ]

        do!
            tf
            $ (createObj [ "sessionID", box ""; "directory", box workspaceDir ], messagesC_1)
            |> unbox<JS.Promise<unit>>

        let resultC_1 = unbox<obj[]> (get messagesC_1 "messages")
        let ackIdC_1 = str (get resultC_1.[1] "info") "id"
        let epochC_1 = ackIdC_1.Substring(capsAcknowledgePrefix.Length)

        // 模拟稍后 Host 传入了真实 sessionID "real-session-c"
        for msg in resultC_1 do
            let info = get msg "info"
            let role = str info "role"

            if role = "user" && not ((str info "id").StartsWith("caps-synth-")) then
                info?sessionID <- box "real-session-c"

        let messagesC_2 = createObj [ "messages", box resultC_1 ]

        do!
            tf
            $ (createObj [ "sessionID", box "real-session-c"; "directory", box workspaceDir ], messagesC_2)
            |> unbox<JS.Promise<unit>>

        let resultC_2 = unbox<obj[]> (get messagesC_2 "messages")
        let ackIdC_2 = str (get resultC_2.[1] "info") "id"
        let epochC_2 = ackIdC_2.Substring(capsAcknowledgePrefix.Length)

        check "epoch remains stable when session ID is later assigned" (epochC_1 = epochC_2)

        // 再次验证业务 sessionID 没有被改写为 epoch-*。
        let nativeMsgCInfo = get resultC_2.[3] "info"
        let nativeSessionID_C = str nativeMsgCInfo "sessionID"
        check "native message sessionID remains real-session-c" (nativeSessionID_C = "real-session-c")

        do! rmAsync workspaceDir
    }
