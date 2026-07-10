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

let testTryGetRealContextUsage () =
    promise {
        let mockGet = fun (arg: obj) ->
            promise {
                let path = Dyn.get arg "path"
                let id = if Dyn.isNullish path then "" else Dyn.str path "id"
                if id = "s-test-real-api" then
                    return createObj [
                        "data", createObj [
                            "tokens", createObj [
                                "total", box 54321
                            ]
                        ]
                    ]
                else
                    return null
            }
        let mockSession = createObj [
            "get", box (System.Func<obj, JS.Promise<obj>>(mockGet))
        ]
        let mockClient = createObj [
            "session", mockSession
        ]
        
        let getUsageOpt = Wanxiangshu.Shell.ContextBudgetUsageCodec.tryGetRealContextUsage mockClient "s-test-real-api"
        check "tryGetRealContextUsage should return Some" getUsageOpt.IsSome
        let getUsage = getUsageOpt.Value
        let! tokens = getUsage [||]
        equal "tokens should be Some 54321" (Some 54321) tokens
    }

let testApplyContextBudgetBacklogContentChange () =
    promise {
        let scope = Wanxiangshu.Shell.RuntimeScope.create()
        let sessionID = "s-budget-backlog-change"
        let plan =
            { SessionID = sessionID
              Agent = "main"
              Directory = ""
              Excluded = false
              IsSubagentSession = false
              Cleaned = []
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 100000
              GetContextUsage = (fun _ -> Promise.lift (Some 35000)) }

        let backlog1 = [
            { ahaMoments = "aha1"
              changesAndReasons = "changes1"
              gotchas = "gotchas1"
              lessonsAndConventions = "lessons1"
              plan = "plan1" }
        ]
        let backlog2 = [
            { ahaMoments = "aha1"
              changesAndReasons = "changes1"
              gotchas = "gotchas1"
              lessonsAndConventions = "lessons1"
              plan = "plan2" // 内容改变，数量不变
            }
        ]

        let mutable currentBacklog = backlog1
        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> currentBacklog }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        
        let messages = [ mkMsg "msg1" User [] ]
        let encoded = [| box "msg" |]

        // 第一次调用，会触发初始化
        let! _ = applyContextBudget plan backlogOps messages encoded encodeMessages
        let storeAfter1 = Wanxiangshu.Shell.ContextBudgetStore.get scope sessionID
        equal "LastBacklog length should be 1" 1 storeAfter1.LastBacklog.Length
        equal "LastBacklog plan should be plan1" "plan1" storeAfter1.LastBacklog.[0].plan

        // 修改内容
        currentBacklog <- backlog2
        
        // 再次调用，虽然数量没变，但因为内容变化应该引起 LastBacklog 的更新
        let! _ = applyContextBudget plan backlogOps messages encoded encodeMessages
        let storeAfter2 = Wanxiangshu.Shell.ContextBudgetStore.get scope sessionID
        equal "LastBacklog plan after change should be plan2" "plan2" storeAfter2.LastBacklog.[0].plan
    }

let run () =
    promise {
        do! testCompactionThresholdAndTransform ()
        testContextBudgetF ()
        do! testApplyContextBudgetShortCircuit ()
        do! testMuxCompactionTransform ()
        do! testTryGetRealContextUsage ()
        do! testApplyContextBudgetBacklogContentChange ()
    }
