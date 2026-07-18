module Wanxiangshu.Tests.ParallelToolPromptTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.BacklogProjectionBuild
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.ReviewRuntime

module Dyn = Wanxiangshu.Runtime.Dyn

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

let testHostNativeToolsTrigger () =
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
                  Scope = Wanxiangshu.Runtime.RuntimeScope.create ()
                  MaxInputTokens = 200000
                  ModelKey = "openai/gpt-4o:default"
                  LimitSource = "openai-session-model"
                  ObserveLatestUsage = (fun () -> Promise.lift None) }

            runHostMessagesTransform reviewStore sessionID plan backlogOps encodeMessages injectFn loadCaps buildCaps

        let singleCallTriggers toolName sessionID =
            let msgs =
                [ mkMsg "u" User []
                  mkMsg "a" Assistant [ ToolPart(toolName, "c1", None, null) ]
                  mkMsg "c1" ToolResult [] ]

            runTransform sessionID ProjectionPolicy.IncludeProjection msgs

        // bash
        let! res8 = singleCallTriggers "bash" "s8"
        equal "bash triggers" 4 res8.Length
        let last8 = res8.[res8.Length - 1] :?> Message<obj>
        equal "bash agent=orchestrator" "orchestrator" last8.info.agent

        // edit
        let! res9 = singleCallTriggers "edit" "s9"
        equal "edit triggers" 4 res9.Length

        // glob
        let! res10 = singleCallTriggers "glob" "s10"
        equal "glob triggers" 4 res10.Length

        // grep
        let! res11 = singleCallTriggers "grep" "s11"
        equal "grep triggers" 4 res11.Length

        // patch
        let! res12 = singleCallTriggers "patch" "s12"
        equal "patch triggers" 4 res12.Length

        // list
        let! res13 = singleCallTriggers "list" "s13"
        equal "list triggers" 4 res13.Length

        // submit_review 也触发（任何单工具）
        let! res14 = singleCallTriggers "submit_review" "s14"
        equal "submit_review triggers" 4 res14.Length

        // return_reviewer
        let! res15 = singleCallTriggers "return_reviewer" "s15"
        equal "return_reviewer triggers" 4 res15.Length

        // todowrite
        let! res16 = singleCallTriggers "todowrite" "s16"
        equal "todowrite triggers" 4 res16.Length

        // coder
        let! res17 = singleCallTriggers "coder" "s17"
        equal "coder triggers" 4 res17.Length

        // inspector
        let! res18 = singleCallTriggers "inspector" "s18"
        equal "inspector triggers" 4 res18.Length

        // task
        let! res19 = singleCallTriggers "task" "s19"
        equal "task triggers" 4 res19.Length

        // prompt 文案校验
        let promptText =
            match last8.parts |> List.tryHead with
            | Some(TextPart txt) -> txt
            | _ -> ""

        check "promptText no scolding 严禁" (not (promptText.Contains "严禁"))
        check "promptText no scolding 杜绝" (not (promptText.Contains "杜绝"))
        check "promptText constructive (parallel)" (promptText.Contains "parallel")
    }

let testSynthCallIdExcluded () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let runTransform sessionID msgs =
            let plan =
                { SessionID = sessionID
                  Agent = "main"
                  Directory = ""
                  ProjectionPolicy = ProjectionPolicy.IncludeProjection
                  BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
                  CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
                  ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
                  ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
                  IsSubagentSession = false
                  Cleaned = msgs
                  RawArray = None
                  SembleInjectEnabled = false
                  Scope = Wanxiangshu.Runtime.RuntimeScope.create ()
                  MaxInputTokens = 200000
                  ModelKey = "openai/gpt-4o:default"
                  LimitSource = "openai-session-model"
                  ObserveLatestUsage = (fun () -> Promise.lift None) }

            runHostMessagesTransform reviewStore sessionID plan backlogOps encodeMessages injectFn loadCaps buildCaps

        // 合成 callID（semble-call-*）不触发——宿主内部注入
        let msgs20 =
            [ mkMsg "u" User []
              mkMsg "a" Assistant [ ToolPart("bash", "semble-call-123", None, null) ]
              mkMsg "semble-call-123" ToolResult [] ]

        let! res20 = runTransform "s20" msgs20
        equal "semble-call-* excluded" 3 res20.Length

        // 合成 callID（caps-call-*）不触发
        let msgs21 =
            [ mkMsg "u" User []
              mkMsg "a" Assistant [ ToolPart("read", "caps-call-fp-0", None, null) ]
              mkMsg "caps-call-fp-0" ToolResult [] ]

        let! res21 = runTransform "s21" msgs21
        equal "caps-call-* remains a native host message" 3 res21.Length
    }

let testCompletedToolPartInAssistantTriggers () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let runTransform sessionID msgs =
            let plan =
                { SessionID = sessionID
                  Agent = "main"
                  Directory = ""
                  ProjectionPolicy = ProjectionPolicy.IncludeProjection
                  BacklogProjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.BacklogProjectionPolicy.Include
                  CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Include
                  ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Include
                  ContextBudgetPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ContextBudgetPolicy.Include
                  IsSubagentSession = false
                  Cleaned = msgs
                  RawArray = None
                  SembleInjectEnabled = false
                  Scope = Wanxiangshu.Runtime.RuntimeScope.create ()
                  MaxInputTokens = 200000
                  ModelKey = "openai/gpt-4o:default"
                  LimitSource = "openai-session-model"
                  ObserveLatestUsage = (fun () -> Promise.lift None) }

            runHostMessagesTransform reviewStore sessionID plan backlogOps encodeMessages injectFn loadCaps buildCaps

        let state =
            { status = ToolExecutionStatus.Completed
              output = "done"
              error = ""
              input = null
              operationAction = "" }

        let msgs =
            [ mkMsg "u" User []
              mkMsg "a" Assistant [ ToolPart("bash", "c1", Some state, null) ] ]

        let! res = runTransform "s_term_assist" msgs
        equal "terminal in assistant triggers" 3 res.Length
        let last = res.[res.Length - 1] :?> Message<obj>
        equal "last message role is User for hint" User last.info.role
        check "ID starts with parallel-tool-synth-" (last.info.id.StartsWith("parallel-tool-synth-"))
    }

let run () =
    promise {
        do! testHostNativeToolsTrigger ()
        do! testSynthCallIdExcluded ()
        do! testCompletedToolPartInAssistantTriggers ()
    }
