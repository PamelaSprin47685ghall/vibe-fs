module Wanxiangshu.Tests.ContextBudgetRealApiSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ContextBudgetUsageCodec
open Wanxiangshu.Shell.OpencodeContextBudgetObservation

let private mockUserMessage (modelObj: obj) : Message<obj> =
    { info =
        { id = "u-1"
          sessionID = "s-1"
          role = User
          agent = "main"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = [ TextPart "hello" ]
      source = Native
      raw = createObj [ "model", modelObj ] }

let private mockProviderList (response: obj) =
    System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return response })

let private mockSessionContext (response: obj) =
    System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return response })

let spec_tryEffectiveLimit_extractsModelAndSubtracts5000 () =
    promise {
        let modelObj =
            createObj [ "providerID", box "anthropic"; "modelID", box "claude-3-5" ]

        let messages = [ mockUserMessage modelObj ]

        let listResponse =
            createObj
                [ "all",
                  box
                      [| createObj
                             [ "id", box "anthropic"
                               "models",
                               createObj [ "claude-3-5", createObj [ "limit", createObj [ "context", box 200000 ] ] ] ] |] ]

        let client =
            createObj [ "provider", createObj [ "list", box (mockProviderList listResponse) ] ]

        let! limit = tryEffectiveLimit client "" messages
        equal "limit is limit.context - 5000" (Some 195000) limit
    }

let spec_tryEffectiveLimit_idFallback () =
    promise {
        let modelObj = createObj [ "providerID", box "anthropic"; "id", box "claude-3-5" ]
        let messages = [ mockUserMessage modelObj ]

        let listResponse =
            createObj
                [ "all",
                  box
                      [| createObj
                             [ "id", box "anthropic"
                               "models",
                               createObj [ "claude-3-5", createObj [ "limit", createObj [ "context", box 100000 ] ] ] ] |] ]

        let client =
            createObj [ "provider", createObj [ "list", box (mockProviderList listResponse) ] ]

        let! limit = tryEffectiveLimit client "" messages
        equal "limit works with id fallback" (Some 95000) limit
    }

let spec_tryEffectiveLimit_missingModel_returnsNone () =
    promise {
        let messages =
            [ { info =
                  { id = "u-1"
                    sessionID = "s-1"
                    role = User
                    agent = "main"
                    isError = false
                    toolName = ""
                    details = null
                    time = null }
                parts = [ TextPart "hello" ]
                source = Native
                raw = null } ]

        let client = createObj []
        let! limit = tryEffectiveLimit client "" messages
        equal "no user model -> None" None limit
    }

let spec_tryEffectiveLimit_missingProviderList_returnsNone () =
    promise {
        let modelObj =
            createObj [ "providerID", box "anthropic"; "modelID", box "claude-3-5" ]

        let messages = [ mockUserMessage modelObj ]
        let client = createObj []

        let! limit = tryEffectiveLimit client "" messages
        equal "missing provider list -> None" None limit
    }

let spec_tryCurrentUsage_prefixBytesRatioCalculation () =
    promise {
        let contextMsgs =
            [| createObj [ "role", box "user"; "content", box "hello" ]
               createObj
                   [ "role", box "assistant"
                     "content", box "hi"
                     "tokens", createObj [ "input", box 1000 ] ] |]

        let contextResponse = createObj [ "data", box contextMsgs ]

        let encoded =
            [| box contextMsgs.[0]
               box contextMsgs.[1]
               box contextMsgs.[0]
               box contextMsgs.[1] |]

        let prefixBytes = utf8JsonBytes (box contextMsgs)
        let currentBytes = utf8JsonBytes (box encoded)
        let expected = int ((1000.0 * float currentBytes) / float prefixBytes)

        let client =
            createObj [ "session", createObj [ "context", box (mockSessionContext contextResponse) ] ]

        let! tokens = tryCurrentUsage client "sess-1" encoded
        equal "tokens estimated by prefix ratio" (Some expected) tokens
    }

let spec_tryCurrentUsage_noTokens_returnsNone () =
    promise {
        let contextMsgs =
            [| createObj [ "role", box "user"; "content", box "hello" ]
               createObj [ "role", box "assistant"; "content", box "hi" ] |]

        let contextResponse = createObj [ "data", box contextMsgs ]
        let encoded = [| box contextMsgs.[0] |]

        let client =
            createObj [ "session", createObj [ "context", box (mockSessionContext contextResponse) ] ]

        let! tokens = tryCurrentUsage client "sess-1" encoded
        equal "no tokens -> None" None tokens
    }

let spec_tryCurrentUsage_emptyData_returnsNone () =
    promise {
        let contextResponse = createObj [ "data", box [||] ]

        let client =
            createObj [ "session", createObj [ "context", box (mockSessionContext contextResponse) ] ]

        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "empty data -> None" None tokens
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_tryEffectiveLimit_extractsModelAndSubtracts5000 ()
        do! spec_tryEffectiveLimit_idFallback ()
        do! spec_tryEffectiveLimit_missingModel_returnsNone ()
        do! spec_tryEffectiveLimit_missingProviderList_returnsNone ()
        do! spec_tryCurrentUsage_prefixBytesRatioCalculation ()
        do! spec_tryCurrentUsage_noTokens_returnsNone ()
        do! spec_tryCurrentUsage_emptyData_returnsNone ()
    }
