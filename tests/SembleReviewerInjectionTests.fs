module Wanxiangshu.Tests.SembleReviewerInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell
open Wanxiangshu.Shell.SembleSearch
open Wanxiangshu.Shell.SembleSearchClient
open Wanxiangshu.Opencode.MessageTransform
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Shell.ReviewRuntime

let testSembleInjectsForReviewer () =
    promise {
        let mockClientObj =
            createObj
                [ "callTool",
                  box (fun req -> Promise.lift (box {| content = [| box {| text = "{\"results\": []}" |} |] |}))
                  "connect", box (fun _ -> Promise.lift ())
                  "close", box (fun () -> Promise.lift ()) ]

        setClientForTest (Some(mockClientObj :?> Client))

        try
            let sessionID = "session-test-reviewer-" + System.Guid.NewGuid().ToString("N")

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

            // Coder should be blocked -> breakpoint is updated to 2
            markBreakpoint sessionID 0
            let! _ = injectSembleIntoEncoded "dir" "coder" sessionID encoded
            let bpAfterCoder = breakpointStart sessionID
            equal "coder breakpoint is updated" (Some 2) bpAfterCoder

            // Reviewer should be allowed -> breakpoint remains 0 (since no actual search results)
            markBreakpoint sessionID 0
            let! _ = injectSembleIntoEncoded "dir" "reviewer" sessionID encoded
            let bpAfterReviewer = breakpointStart sessionID
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

        let backlogOps =
            { Host = Opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

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
              IsSubagentSession = false
              Cleaned = msgs
              RawArray = None
              SembleInjectEnabled = true
              Scope = Wanxiangshu.Shell.RuntimeScope.create ()
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift None) }

        let! res =
            runHostMessagesTransform
                reviewStore
                "s-amend-semble"
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "amend skipped: output should preserve all 4 messages" 4 res.Length
    }
