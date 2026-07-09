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

let run () =
    promise { do! testMessageSanitization () }
