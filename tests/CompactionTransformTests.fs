module Wanxiangshu.Tests.CompactionTransformTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.Message
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.ReviewRuntime

module Dyn = Wanxiangshu.Shell.Dyn

let mkMsg id role parts =
    { info =
        { id = id
          sessionID = "test-compaction"
          role = role
          agent = "main"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = parts
      source = Native
      raw = null }

let testCompactionThresholdAndTransform () =
    promise {
        // 1. 测试 Threshold 判定
        let b = 100000L
        check "79% is not compacting" (not (Wanxiangshu.Kernel.ContextBudget.isCompactingRequired 79000L b))
        check "80% is compacting" (Wanxiangshu.Kernel.ContextBudget.isCompactingRequired 80000L b)
        check "81% is compacting" (Wanxiangshu.Kernel.ContextBudget.isCompactingRequired 81000L b)

        // 2. 测试 compactingTransform 转换
        let msgs = [
            mkMsg "user-1" User [ TextPart "hello" ]
            mkMsg "assist-1" Assistant [ TextPart "do something" ]
            mkMsg "user-2" User [ TextPart "do next" ]
        ]
        
        let backlog : BacklogEntry list = [
            { ahaMoments = "aha"
              changesAndReasons = "changes"
              gotchas = "gotchas"
              lessonsAndConventions = "lessons"
              plan = "my plan" }
        ]

        let compacted = Wanxiangshu.Kernel.BacklogProjectionCore.compactingTransform msgs backlog
        equal "Compacted should contain exactly 1 message" 1 compacted.Length
        let first = compacted.[0]
        equal "Compacted message role should be user" User first.info.role
        let content = 
            match first.parts.[0] with
            | TextPart t -> t
            | _ -> ""
        check "Should contain <do-not-exec>" (content.Contains("<do-not-exec>"))
        check "Should contain </do-not-exec>" (content.Contains("</do-not-exec>"))
        check "Should contain 'my plan'" (content.Contains("my plan"))
        check "Should contain 'hello'" (content.Contains("hello"))
        check "Should contain 'do next'" (content.Contains("do next"))
    }

let testContextBudgetF () =
    // F(a, b, c, s) = 2a >= b + s + c
    // 当 a=60000, b=100000, c=10000, s=10000 时，2*60000 >= 100000 + 10000 + 10000 (true)
    check "F should be true" (Wanxiangshu.Kernel.ContextBudget.F 60000L 100000L 10000L 10000L)
    // 当 a=59000 时，118000 < 120000 (false)
    check "F should be false" (not (Wanxiangshu.Kernel.ContextBudget.F 59000L 100000L 10000L 10000L))

let testApplyContextBudgetShortCircuit () =
    promise {
        let plan =
            { SessionID = "s-budget-short"
              Agent = "main"
              Directory = ""
              Excluded = false
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = Wanxiangshu.Shell.RuntimeScope.create()
              MaxInputTokens = 100000
              GetContextUsage = (fun _ -> Promise.lift (Some 90000)) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let msgs = [
            mkMsg "msg1" User []
        ]

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let! result = applyContextBudget plan backlogOps msgs [||] encodeMessages
        equal "applyContextBudget result length (should not have nudge)" 1 result.Length
    }

let testMuxCompactionTransform () =
    promise {
        let runtimeScope = Wanxiangshu.Shell.RuntimeScope.create()
        let backlogSession = Wanxiangshu.Mux.BacklogSession.BacklogSession(runtimeScope)
        
        let deps = createObj [
            "directory", box ""
            "maxInputTokens", box 100000
        ]
        
        let input = createObj [
            "sessionID", box "s-mux"
            "agent", box "main"
        ]

        let originalMsg = createObj [
            "id", box "msg-mux-1"
            "role", box "user"
            "agent", box "main"
            "parts", box [| box (createObj [ "type", box "text"; "text", box "hello mux" ]) |]
        ]
        
        let output = createObj [
            "messages", box [| originalMsg |]
        ]

        do! Wanxiangshu.Mux.MessageTransform.compactingTransform deps runtimeScope backlogSession input output
        
        let messages = Wanxiangshu.Shell.Dyn.get output "messages" :?> obj array
        equal "Mux compacted length should be 1" 1 messages.Length
        let first = messages.[0]
        let role = Wanxiangshu.Shell.Dyn.str first "role"
        equal "Mux compacted role should be user" "user" role
        let parts = Wanxiangshu.Shell.Dyn.get first "parts" :?> obj array
        let firstPart = parts.[0]
        let content = Wanxiangshu.Shell.Dyn.str firstPart "text"
        check "Mux compacted content contains <do-not-exec>" (content.Contains("<do-not-exec>"))
        check "Mux compacted content contains 'hello mux'" (content.Contains("hello mux"))
    }

let run () =
    promise {
        do! testCompactionThresholdAndTransform ()
        testContextBudgetF ()
        do! testApplyContextBudgetShortCircuit ()
        do! testMuxCompactionTransform ()
    }
