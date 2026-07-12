module Wanxiangshu.Tests.MessageTransformPolicyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Kernel.BacklogProjectionCore

module Dyn = Wanxiangshu.Shell.Dyn

let defaultExcludedTrue () =
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

    agents
    |> List.iter (fun a -> check (sprintf "default excluded: %s" a) (shouldExcludeAgentFromProjection a false))

let defaultExcludedFalse () =
    let agents = [ "main"; "agent"; "manager"; "user" ]

    agents
    |> List.iter (fun a -> check (sprintf "not excluded: %s" a) (not (shouldExcludeAgentFromProjection a false)))

let childWorkspaceExtraExcluded () =
    let agents = [ "exec"; "explore" ]

    agents
    |> List.iter (fun a -> check (sprintf "child excluded: %s" a) (shouldExcludeAgentFromProjection a true))

let childWorkspaceNotExcluded () =
    let agents = [ "browser"; "investigator"; "executor"; "title"; "compaction" ]

    agents
    |> List.iter (fun a -> check (sprintf "child still excluded: %s" a) (shouldExcludeAgentFromProjection a true))

    check "main still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "main" true))
    check "agent still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "agent" true))

let testTransformO1Cache () =
    promise {
        pipelineRunCount <- 0
        let reviewStore = createReviewStore ()

        let plan =
            { SessionID = ""
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
              CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
              ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
              ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = Wanxiangshu.Shell.RuntimeScope.create ()
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift None) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) = [||]
        let injectFn (_policy: BacklogProjectionPolicy) (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) = arr

        let plan2 =
            { plan with
                Cleaned =
                    [ { info =
                          { id = "msg1"
                            sessionID = "test"
                            role = User
                            agent = "manager"
                            isError = false
                            toolName = ""
                            details = null
                            time = null }
                        parts = []
                        source = Native
                        raw = null } ] }

        let! _ =
            runHostMessagesTransform
                reviewStore
                ""
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan2
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "count after first call" 1 pipelineRunCount

        let! _ =
            runHostMessagesTransform
                reviewStore
                ""
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan2
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "count after second call should stay 1 (cache hit)" 1 pipelineRunCount
    }

let mkMsg id role parts =
    { info =
        { id = id
          sessionID = "test"
          role = role
          agent = "main"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = parts
      source = Native
      raw = null }

let testSingleToolCallPromptInjection () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let runTransform sessionID projectionPolicy msgs =
            let plan =
                { SessionID = sessionID
                  Agent = "main"
                  Directory = ""
                  ProjectionPolicy = projectionPolicy
                  BacklogProjectionPolicy =
                    (if projectionPolicy = ProjectionPolicy.IncludeProjection then
                         Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
                     else
                         Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Exclude)
                  CapsInjectionPolicy =
                    (if projectionPolicy = ProjectionPolicy.IncludeProjection then
                         Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
                     else
                         Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Exclude)
                  ParallelHintPolicy =
                    (if projectionPolicy = ProjectionPolicy.IncludeProjection then
                         Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
                     else
                         Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude)
                  ContextBudgetPolicy =
                    (if projectionPolicy = ProjectionPolicy.IncludeProjection then
                         Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
                     else
                         Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Disable)
                  IsSubagentSession = false
                  Cleaned = msgs
                  RawArray = None
                  SembleInjectEnabled = false
                  Scope = Wanxiangshu.Shell.RuntimeScope.create ()
                  MaxInputTokens = 200000
                  GetContextUsage = (fun _ -> Promise.lift None) }

            runHostMessagesTransform
                reviewStore
                sessionID
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        // Case 1: 单工具调用 + ToolResult -> 应当被附加
        let msgs1 =
            [ mkMsg "user" User []
              mkMsg "assist" Assistant [ ToolPart("read", "call-1", None, null) ]
              mkMsg "result" ToolResult [] ]

        let! res1 = runTransform "s1" ProjectionPolicy.IncludeProjection msgs1
        equal "Case 1 output length (should be 4)" 4 res1.Length
        let lastMsg = res1.[res1.Length - 1] :?> Message<obj>
        equal "last message role" User lastMsg.info.role

        check
            "last message id starts with parallel-tool-synth-"
            (lastMsg.info.id.StartsWith("parallel-tool-synth-")
             || lastMsg.info.id.StartsWith("parallel-tool-hint:"))

        let promptText =
            match lastMsg.parts |> List.tryHead with
            | Some(TextPart txt) -> txt
            | _ -> ""

        check "promptText contains 'parallel'" (promptText.Contains("parallel"))

        // Case 2: 双工具调用 + ToolResult -> 不应附加
        let msgs2 =
            [ mkMsg "user" User []
              mkMsg
                  "assist"
                  Assistant
                  [ ToolPart("read", "call-1", None, null)
                    ToolPart("write", "call-2", None, null) ]
              mkMsg "result" ToolResult [] ]

        let! res2 = runTransform "s2" ProjectionPolicy.IncludeProjection msgs2
        equal "Case 2 length" 3 res2.Length

        // Case 3: 伪造工具调用 (semble) + ToolResult -> 不应附加
        let msgs3 =
            [ mkMsg "user" User []
              mkMsg "assist" Assistant [ ToolPart("read", "semble-call-123", None, null) ]
              mkMsg "result" ToolResult [] ]

        let! res3 = runTransform "s3" ProjectionPolicy.IncludeProjection msgs3
        equal "Case 3 length" 3 res3.Length

        // Case 4: 混合场景 (1 个真工具 + 1 个 semble 工具) -> 不应附加
        let msgs4 =
            [ mkMsg "user" User []
              mkMsg
                  "assist"
                  Assistant
                  [ ToolPart("read", "call-1", None, null)
                    ToolPart("write", "semble-call-2", None, null) ]
              mkMsg "result" ToolResult [] ]

        let! res4 = runTransform "s4" ProjectionPolicy.IncludeProjection msgs4
        equal "Case 4 length" 4 res4.Length

        // Case 5: Excluded = true -> 不应附加
        let! res5 = runTransform "s5" ProjectionPolicy.ExcludeProjection msgs1
        equal "Case 5 length (excluded)" 3 res5.Length

        // Case 6: 多轮迭代不变性 (使用全新 sessionID 以绕过缓存，输入包含上一轮注入的 synth 消息，排除合成消息后应重新注入)
        let typedRes1 = res1 |> Array.toList |> List.map (fun x -> x :?> Message<obj>)
        let strippedRes1 = Wanxiangshu.Kernel.Messaging.stripSyntheticBySource typedRes1
        let! res6 = runTransform "s6" ProjectionPolicy.IncludeProjection strippedRes1
        equal "Case 6 length after second round" 4 res6.Length

        // Case 7: 边界情况 - 重复 ID 消息安全通过不崩溃
        let msgs7 =
            [ mkMsg "dup-id" User []
              mkMsg "dup-id" Assistant [ ToolPart("read", "call-1", None, null) ]
              mkMsg "dup-id" ToolResult [] ]

        let! res7 = runTransform "s7" ProjectionPolicy.IncludeProjection msgs7
        equal "Case 7 length with duplicate IDs" 4 res7.Length
    }

let run () =
    promise {
        defaultExcludedTrue ()
        defaultExcludedFalse ()
        childWorkspaceExtraExcluded ()
        childWorkspaceNotExcluded ()
        do! testTransformO1Cache ()
        do! testSingleToolCallPromptInjection ()
    }
