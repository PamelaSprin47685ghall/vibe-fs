module Wanxiangshu.Tests.SembleReviewerInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SembleSearch
open Wanxiangshu.Runtime.SembleSearchClient
open Wanxiangshu.Hosts.Opencode.SembleInjection
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ReviewRuntime

let testSembleCoderBlocked () =
    promise {
        let mockClientObj =
            createObj
                [ "callTool",
                  box (fun req -> Promise.lift (box {| content = [| box {| text = "{\"results\": []}" |} |] |}))
                  "connect", box (fun _ -> Promise.lift ())
                  "close", box (fun () -> Promise.lift ()) ]

        setClientForTest (Some(mockClientObj :?> Client))

        try
            let sessionID = "session-test-reviewer-coder-" + System.Guid.NewGuid().ToString("N")

            let encoded =
                [| box (
                       createObj
                           [ "info", box (createObj [ "id", box "m0"; "role", box "user"; "sessionID", box sessionID ])
                             "parts", box [| box (createObj [ "type", box "text"; "text", box "hello world" ]) |] ]
                   )
                   box (
                       createObj
                           [ "info",
                             box (createObj [ "id", box "m1"; "role", box "assistant"; "sessionID", box sessionID ])
                             "parts", box [||] ]
                   ) |]

            let scope = create ()
            markBreakpoint scope sessionID 0
            let! _ = injectSembleIntoEncoded scope "dir" "coder" sessionID encoded
            let bpAfterCoder = breakpointStart scope sessionID
            equal "coder breakpoint is updated" (Some 2) bpAfterCoder
        finally
            setClientForTest None
    }

let testSembleReviewerAllows () =
    promise {
        let mockClientObj =
            createObj
                [ "callTool",
                  box (fun req -> Promise.lift (box {| content = [| box {| text = "{\"results\": []}" |} |] |}))
                  "connect", box (fun _ -> Promise.lift ())
                  "close", box (fun () -> Promise.lift ()) ]

        setClientForTest (Some(mockClientObj :?> Client))

        try
            let sessionID =
                "session-test-reviewer-allows-" + System.Guid.NewGuid().ToString("N")

            let encoded =
                [| box (
                       createObj
                           [ "info", box (createObj [ "id", box "m0"; "role", box "user"; "sessionID", box sessionID ])
                             "parts", box [| box (createObj [ "type", box "text"; "text", box "hello world" ]) |] ]
                   )
                   box (
                       createObj
                           [ "info",
                             box (createObj [ "id", box "m1"; "role", box "assistant"; "sessionID", box sessionID ])
                             "parts", box [||] ]
                   ) |]

            let scope = create ()
            markBreakpoint scope sessionID 0
            let! _ = injectSembleIntoEncoded scope "dir" "reviewer" sessionID encoded
            let bpAfterReviewer = breakpointStart scope sessionID
            equal "reviewer breakpoint remains unchanged" (Some 0) bpAfterReviewer
        finally
            setClientForTest None
    }

let private mkMsg id role parts =
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

let testAmendSkippedWhenSembleInjectEnabled () =
    promise {
        let reviewStore = createReviewStore ()

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let msgs =
            [ mkMsg "user1" User []
              mkMsg "assist1" Assistant [ ToolPart("read", "call-1", None, null) ]
              mkMsg "result1" ToolResult []
              { info =
                  { id = "amend-msg"
                    sessionID = "test"
                    role = User
                    agent = "main"
                    isError = false
                    toolName = ""
                    details = null
                    time = null }
                parts = []
                source = Native
                raw = createObj [ "amend", box 1 ] } ]

        let plan =
            { SessionID = "s-amend-semble"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.ExcludeProjection
              CapsInjectionPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.CapsInjectionPolicy.Exclude
              ParallelHintPolicy = Wanxiangshu.Kernel.MessageTransformPolicy.ParallelHintPolicy.Exclude
              IsSubagentSession = false
              Cleaned = msgs
              RawArray = None
              SembleInjectEnabled = true
              Scope = create ()
              MaxInputTokens = 200000
              ModelKey = "openai/gpt-4o:default"
              LimitSource = "openai-session-model"
              ObserveLatestUsage = (fun () -> Promise.lift ()) }

        let! res = runHostMessagesTransform reviewStore "s-amend-semble" plan encodeMessages injectFn loadCaps buildCaps

        equal "amend skipped: output should preserve all 4 messages" 4 res.Length
    }
