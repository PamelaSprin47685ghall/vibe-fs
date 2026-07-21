module Wanxiangshu.Tests.MessageTransformPolicyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.ReviewRuntime

module Dyn = Wanxiangshu.Runtime.Dyn

let defaultExcludedTrue () =
    let agents = [ "browser"; "inspector"; "executor"; "title"; "compaction" ]

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
    let agents = [ "browser"; "inspector"; "executor"; "title"; "compaction" ]

    agents
    |> List.iter (fun a -> check (sprintf "child still excluded: %s" a) (shouldExcludeAgentFromProjection a true))

    check "main still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "main" true))
    check "agent still not excluded even in child workspace" (not (shouldExcludeAgentFromProjection "agent" true))

let agentNormalizationTest () =
    check "Inspector (caps)" (getCapsInjectionPolicy "Inspector" false = CapsInjectionPolicy.Include)
    check "inspector  (caps)" (getCapsInjectionPolicy "inspector " false = CapsInjectionPolicy.Include)
    check "EXec (caps)" (getCapsInjectionPolicy "EXec" false = CapsInjectionPolicy.Exclude)

let testCapsSlotReuse () =
    promise {
        let reviewStore = createReviewStore ()

        let scope = Wanxiangshu.Runtime.RuntimeScope.create ()

        let capsObj =
            box (
                createObj
                    [ "info", box (createObj [ "id", box "caps-synth-user-test"; "role", box "user" ])
                      "parts", box [||] ]
            )

        let mkPlan cleanMsgs =
            { SessionID = "caps-slot-test"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
              ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
              IsSubagentSession = false
              Cleaned = cleanMsgs
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              ModelKey = "openai/gpt-4o:default"
              LimitSource = "openai-session-model"
              ObserveLatestUsage = (fun () -> promise { return () }) }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let injectFn (_policy: ProjectionPolicy) (arr: obj array) = promise { return arr }

        let loadCapsCount = ref 0

        let loadCaps () =
            loadCapsCount.Value <- loadCapsCount.Value + 1
            promise { return [] }

        let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) = Array.append [| capsObj |] arr

        let msg =
            { info =
                { id = "msg1"
                  sessionID = "caps-slot-test"
                  role = User
                  agent = "manager"
                  isError = false
                  toolName = ""
                  details = null
                  time = null }
              parts = [ TextPart "hello" ]
              source = Native
              raw = null }

        let plan = mkPlan [ msg ]

        let! res1 =
            runHostMessagesTransform reviewStore "caps-slot-test" plan encodeMessages injectFn loadCaps buildCaps

        equal "first call invokes loadCaps" 1 loadCapsCount.Value
        equal "first call prepends caps" 2 res1.Length

        let! res2 =
            runHostMessagesTransform reviewStore "caps-slot-test" plan encodeMessages injectFn loadCaps buildCaps

        equal "second call does NOT invoke loadCaps (CapsSlot hit)" 1 loadCapsCount.Value

        check "caps prefix reference stable across calls" (System.Object.ReferenceEquals(res1.[0], res2.[0]))
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
                  IsSubagentSession = false
                  Cleaned = msgs
                  RawArray = None
                  SembleInjectEnabled = false
                  Scope = Wanxiangshu.Runtime.RuntimeScope.create ()
                  MaxInputTokens = 200000
                  ModelKey = "openai/gpt-4o:default"
                  LimitSource = "openai-session-model"
                  ObserveLatestUsage = (fun () -> promise { return () }) }

            runHostMessagesTransform reviewStore sessionID plan encodeMessages injectFn loadCaps buildCaps

        // Case 1: 单工具调用 + ToolResult -> 应当被附加
        let msgs1 =
            [ mkMsg "user" User []
              mkMsg "assist" Assistant [ ToolPart("read", "call-1", None, null) ]
              mkMsg "call-1" ToolResult [] ]

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
              mkMsg "call-1" ToolResult [] ]

        let! res2 = runTransform "s2" ProjectionPolicy.IncludeProjection msgs2
        equal "Case 2 length" 3 res2.Length

        // Case 3: 伪造工具调用 (semble) + ToolResult -> 不应附加
        let msgs3 =
            [ mkMsg "user" User []
              mkMsg "assist" Assistant [ ToolPart("read", "semble-call-123", None, null) ]
              mkMsg "semble-call-123" ToolResult [] ]

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
              mkMsg "call-1" ToolResult [] ]

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
              mkMsg "call-1" ToolResult [] ]

        let! res7 = runTransform "s7" ProjectionPolicy.IncludeProjection msgs7
        equal "Case 7 length with duplicate IDs" 4 res7.Length
    }

let run () =
    promise {
        defaultExcludedTrue ()
        defaultExcludedFalse ()
        childWorkspaceExtraExcluded ()
        childWorkspaceNotExcluded ()
        agentNormalizationTest ()
        do! testCapsSlotReuse ()
        do! testSingleToolCallPromptInjection ()
    }
