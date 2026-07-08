module Wanxiangshu.Tests.SembleReviewerInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleSearch
open Wanxiangshu.Opencode.MessageTransform

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
            let sessionID = "session-test-reviewer"

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
