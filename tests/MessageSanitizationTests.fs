module Wanxiangshu.Tests.MessageSanitizationTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.ReviewRuntime

module Dyn = Wanxiangshu.Shell.Dyn

let private zeroWidth = "\u200B"

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

let testMessageSanitization () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) =
            [| createObj
                   [ "id", box "msg-1"
                     "role", box "user"
                     "parts",
                     box
                         [| createObj [ "type", box "text"; "text", box "" ]
                            createObj [ "type", box "tool"; "output", box "" ]
                            createObj [ "type", box "tool"; "error", box "" ] |]
                     "message", box ""
                     "content", box ""
                     "reasoning", box ""
                     "thought", box ""
                     "errorText", box "" ] |]

        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let runTransform sessionID msgs =
            let plan =
                { SessionID = sessionID
                  Agent = "main"
                  Directory = ""
                  Excluded = false
                  IsSubagentSession = false
                  Cleaned = msgs
                  RawArray = None
                  SembleInjectEnabled = false }

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

        let! res1 = runTransform "sanitize-session" [ mkMsg "user" User [] ]
        equal "sanitize result length" 1 res1.Length
        let msgObj = res1.[0]

        for prop in [| "message"; "content"; "reasoning"; "thought"; "errorText" |] do
            equal prop zeroWidth (string (Dyn.get msgObj prop))

        let parts = Dyn.get msgObj "parts" :?> obj array
        equal "parts length" 3 parts.Length
        equal "text changed to zero width" zeroWidth (string (Dyn.get parts.[0] "text"))
        equal "output changed to zero width" zeroWidth (string (Dyn.get parts.[1] "output"))
        equal "error changed to zero width" zeroWidth (string (Dyn.get parts.[2] "error"))
        let! res2 = runTransform "sanitize-session" [ mkMsg "user" User [] ]
        equal "cached message remains zero width" zeroWidth (string (Dyn.get res2.[0] "message"))
    }

let testEmptyArrayAndMissingContentSanitization () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let partsRef = [||]
        let contentRef = [||]
        let partsRef5 = [| box (createObj [ "type", box "text"; "text", box "hello" ]) |]

        let infoObj =
            createObj [ "id", box "msg-8"; "role", box "assistant"; "error", box "" ]

        let raw =
            [|
               // Case 1: parts is empty array
               createObj
                   [ "info", box (createObj [ "id", box "msg-1"; "role", box "assistant" ])
                     "parts", box partsRef
                     "content", box contentRef ]
               // Case 2: content is empty array
               createObj
                   [ "info", box (createObj [ "id", box "msg-2"; "role", box "assistant" ])
                     "content", box [||] ]
               // Case 3: content and parts are nullish/missing
               createObj [ "info", box (createObj [ "id", box "msg-3"; "role", box "assistant" ]) ]
               // Case 4: user + content empty string + parts empty array
               createObj
                   [ "info", box (createObj [ "id", box "msg-4"; "role", box "user" ])
                     "content", box ""
                     "parts", box [||] ]
               // Case 5: content missing, parts present (should sync content <- parts)
               createObj [ "id", box "msg-5"; "role", box "assistant"; "parts", box partsRef5 ]
               // Case 6: parts missing, content present as string (should sync parts <- content wrap)
               createObj [ "id", box "msg-6"; "role", box "assistant"; "content", box "world" ]
               // Case 7: role="user" with no properties (simplified message)
               createObj [ "role", box "user" ]
               // Case 8: nested info object (to verify info skipping)
               createObj [ "info", box infoObj; "parts", box [||] ] |]

        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let plan =
            { SessionID = "sanitize-session-2"
              Agent = "main"
              Directory = ""
              Excluded = false
              IsSubagentSession = false
              Cleaned = []
              RawArray = Some raw
              SembleInjectEnabled = false }

        let! res =
            runHostMessagesTransform
                reviewStore
                "sanitize-session-2"
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan
                backlogOps
                (fun _ -> [||])
                injectFn
                loadCaps
                buildCaps

        check "res should be same array reference as raw" (System.Object.ReferenceEquals(res, raw))
        equal "sanitize result length" 8 res.Length

        // Case 1 check
        let msg1 = res.[0]
        let parts1 = Dyn.get msg1 "parts"
        let content1 = Dyn.get msg1 "content"
        check "parts1 should be same array reference" (System.Object.ReferenceEquals(parts1, partsRef))
        check "content1 should be same array reference" (System.Object.ReferenceEquals(content1, contentRef))
        let parts1Arr = parts1 :?> obj array
        check "parts1 should not be empty" (parts1Arr.Length > 0)
        equal "parts1 first element text" zeroWidth (string (Dyn.get parts1Arr.[0] "text"))

        // Case 2 check
        let msg2 = res.[1]
        let content2 = Dyn.get msg2 "content"
        check "content2 should be array" (Dyn.isArray content2)
        let content2Arr = content2 :?> obj array
        check "content2 should not be empty" (content2Arr.Length > 0)
        equal "content2 first element text" zeroWidth (string (Dyn.get content2Arr.[0] "text"))

        // Case 3 check
        let msg3 = res.[2]
        let content3 = Dyn.get msg3 "content"
        let parts3 = Dyn.get msg3 "parts"

        let ok3 =
            (not (Dyn.isNullish content3) && (string content3 <> ""))
            || (not (Dyn.isNullish parts3)
                && Dyn.isArray parts3
                && (parts3 :?> obj array).Length > 0)

        check "msg3 should have non-empty content or parts" ok3

        // Case 4 check
        let msg4 = res.[3]
        let content4 = Dyn.get msg4 "content"
        equal "content4 should be zero width" zeroWidth (string content4)
        let parts4 = Dyn.get msg4 "parts" :?> obj array
        check "parts4 should not be empty" (parts4.Length > 0)
        equal "parts4 first element text" zeroWidth (string (Dyn.get parts4.[0] "text"))

        // Case 5 check
        let msg5 = res.[4]
        let content5 = Dyn.get msg5 "content"
        let parts5 = Dyn.get msg5 "parts"
        check "msg5 content should be same as parts reference" (System.Object.ReferenceEquals(content5, parts5))

        // Case 6 check
        let msg6 = res.[5]
        let content6 = Dyn.get msg6 "content"
        let parts6 = Dyn.get msg6 "parts" :?> obj array
        equal "msg6 content should be world" "world" (string content6)
        check "msg6 parts should be array" (parts6.Length > 0)
        equal "msg6 parts first element text" "world" (string (Dyn.get parts6.[0] "text"))

        // Case 7 check: role="user" with no properties was sanitized
        let msg7 = res.[6]
        let content7 = Dyn.get msg7 "content"
        equal "content7 should be zero width" zeroWidth (string content7)
        let parts7 = Dyn.get msg7 "parts" :?> obj array
        check "parts7 should not be empty" (parts7.Length > 0)
        equal "parts7 first element text" zeroWidth (string (Dyn.get parts7.[0] "text"))

        // Case 8 check: nested info object was skipped and NOT mutated
        let msg8 = res.[7]
        let infoObjRes = Dyn.get msg8 "info"
        equal "info role remains unchanged" "assistant" (string (Dyn.get infoObjRes "role"))
        equal "info error remains empty" "" (string (Dyn.get infoObjRes "error"))
    }

let run () =
    promise {
        do! testMessageSanitization ()
        do! testEmptyArrayAndMissingContentSanitization ()
    }
