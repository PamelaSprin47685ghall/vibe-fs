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
            equal prop "." (string (Dyn.get msgObj prop))

        let parts = Dyn.get msgObj "parts" :?> obj array
        equal "parts length" 3 parts.Length
        equal "text changed to dot" "." (string (Dyn.get parts.[0] "text"))
        equal "output changed to dot" "." (string (Dyn.get parts.[1] "output"))
        equal "error changed to dot" "." (string (Dyn.get parts.[2] "error"))
        let! res2 = runTransform "sanitize-session" [ mkMsg "user" User [] ]
        equal "cached message remains dot" "." (string (Dyn.get res2.[0] "message"))
    }

let testEmptyArrayAndMissingContentSanitization () =
    promise {
        let reviewStore = createReviewStore ()

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) =
            [|
               // Case 1: parts is empty array
               createObj
                   [ "id", box "msg-1"
                     "role", box "assistant"
                     "parts", box [||]
                     "content", box [||] ]
               // Case 2: content is empty array
               createObj [ "id", box "msg-2"; "role", box "assistant"; "content", box [||] ]
               // Case 3: content and parts are nullish/missing
               createObj [ "id", box "msg-3"; "role", box "assistant" ]
               // Case 4: user + content empty string + parts empty array
               createObj [ "id", box "msg-4"; "role", box "user"; "content", box ""; "parts", box [||] ] |]

        let injectFn _ (arr: obj array) = promise { return arr }
        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let plan =
            { SessionID = "sanitize-session-2"
              Agent = "main"
              Directory = ""
              Excluded = false
              IsSubagentSession = false
              Cleaned = [ mkMsg "assistant" Assistant [] ]
              RawArray = None
              SembleInjectEnabled = false }

        let! res =
            runHostMessagesTransform
                reviewStore
                "sanitize-session-2"
                IfStoreEmpty
                (fun _ -> promise { return Seq.empty })
                plan
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "sanitize result length" 4 res.Length

        // Case 1 check
        let msg1 = res.[0]
        let parts1 = Dyn.get msg1 "parts" :?> obj array
        check "parts1 should not be empty" (parts1.Length > 0)
        equal "parts1 first element text" "." (string (Dyn.get parts1.[0] "text"))

        // Case 2 check
        let msg2 = res.[1]
        let content2 = Dyn.get msg2 "content"
        check "content2 should be array" (Dyn.isArray content2)
        let content2Arr = content2 :?> obj array
        check "content2 should not be empty" (content2Arr.Length > 0)
        equal "content2 first element text" "." (string (Dyn.get content2Arr.[0] "text"))

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
        equal "content4 should be dot" "." (string content4)
        let parts4 = Dyn.get msg4 "parts" :?> obj array
        check "parts4 should not be empty" (parts4.Length > 0)
        equal "parts4 first element text" "." (string (Dyn.get parts4.[0] "text"))
    }

let run () =
    promise {
        do! testMessageSanitization ()
        do! testEmptyArrayAndMissingContentSanitization ()
    }
