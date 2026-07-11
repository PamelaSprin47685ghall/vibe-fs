module Wanxiangshu.Tests.ContextBudgetRealApiSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell
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

let private mkModel id limitContext : obj =
    createObj
        [ "id", box id
          "name", box (id + " name")
          "limit", box (createObj [ "context", box limitContext; "output", box limitContext ]) ]

let private mkProvider id models : obj =
    createObj [ "id", box id; "name", box (id + " provider"); "models", box models ]

let private mockProviderList (response: obj) =
    System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return response })

// --- tryEffectiveLimit ---

let spec_tryEffectiveLimit_usesQueryDirectoryAndUnwrapsData () =
    promise {
        let modelObj =
            createObj [ "providerID", box "anthropic"; "modelID", box "claude-3-5" ]

        let messages = [ mockUserMessage modelObj ]

        let models = createObj [ "claude-3-5", box (mkModel "claude-3-5" 200000) ]

        let listResponse =
            createObj
                [ "all", box [| mkProvider "anthropic" models |]
                  "default", box (createObj [])
                  "connected", box [| "anthropic" |] ]

        let listCallArgs = ResizeArray<obj>()

        let listFn =
            System.Func<obj, JS.Promise<obj>>(fun args ->
                listCallArgs.Add args
                promise { return createObj [ "data", box listResponse ] })

        let client = createObj [ "provider", createObj [ "list", box listFn ] ]

        let! limit = tryEffectiveLimit client "/tmp/work" messages
        equal "limit is context - 5000" (Some 195000) limit

        let argsUsed = listCallArgs.[0]
        let query = Dyn.get argsUsed "query"
        check "provider.list called with query.directory" (Dyn.str query "directory" = "/tmp/work")
        check "provider.list NOT called with location" (Dyn.isNullish (Dyn.get argsUsed "location"))
    }

let spec_tryEffectiveLimit_idFallback () =
    promise {
        let modelObj = createObj [ "providerID", box "anthropic"; "id", box "claude-3-5" ]
        let messages = [ mockUserMessage modelObj ]

        let models = createObj [ "claude-3-5", box (mkModel "claude-3-5" 100000) ]

        let listResponse = createObj [ "all", box [| mkProvider "anthropic" models |] ]

        let listFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return createObj [ "data", box listResponse ] })

        let client = createObj [ "provider", createObj [ "list", box listFn ] ]

        let! limit = tryEffectiveLimit client "" messages
        equal "id fallback works" (Some 95000) limit
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

let spec_tryEffectiveLimit_modelNotInProvider_returnsNone () =
    promise {
        let modelObj =
            createObj [ "providerID", box "anthropic"; "modelID", box "unknown-model" ]

        let messages = [ mockUserMessage modelObj ]

        let models = createObj [ "claude-3-5", box (mkModel "claude-3-5" 200000) ]

        let listResponse = createObj [ "all", box [| mkProvider "anthropic" models |] ]

        let listFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return createObj [ "data", box listResponse ] })

        let client = createObj [ "provider", createObj [ "list", box listFn ] ]

        let! limit = tryEffectiveLimit client "" messages
        equal "model not found in provider -> None" None limit
    }

let spec_tryEffectiveLimit_unwrapsDataField () =
    // Regression: old code read response.all directly, but SDK wraps
    // in { data: { all, default, connected } }. This test confirms
    // we correctly traverse the data wrapper.
    promise {
        let modelObj = createObj [ "providerID", box "openai"; "modelID", box "gpt-4" ]

        let messages = [ mockUserMessage modelObj ]

        let models = createObj [ "gpt-4", box (mkModel "gpt-4" 128000) ]

        let listResponse =
            createObj
                [ "all", box [| mkProvider "openai" models |]
                  "default", box (createObj [])
                  "connected", box [||] ]

        let listFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return createObj [ "data", box listResponse ] })

        let client = createObj [ "provider", createObj [ "list", box listFn ] ]
        let! limit = tryEffectiveLimit client "" messages
        equal "data wrapper correctly traversed" (Some 123000) limit
    }

// --- tryCurrentUsage ---

let spec_tryCurrentUsage_usesSessionMessages () =
    promise {
        let messagesCallArgs = ResizeArray<obj>()

        let mkAssistantMsg tokensInput cacheRead =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "id", box "a-1"
                            "role", box "assistant"
                            "tokens",
                            box (
                                createObj
                                    [ "input", box tokensInput
                                      "output", box 100
                                      "cache", box (createObj [ "read", box cacheRead; "write", box 0 ]) ]
                            ) ]
                  )
                  "parts", box [||] ]

        let mkUserMsg =
            createObj
                [ "info", box (createObj [ "id", box "u-1"; "role", box "user" ])
                  "parts", box [||] ]

        let messagesResponse =
            createObj [ "data", box [| mkUserMsg; mkAssistantMsg 5000 2000; mkAssistantMsg 8000 3000 |] ]

        let messagesFn =
            System.Func<obj, JS.Promise<obj>>(fun args ->
                messagesCallArgs.Add args
                promise { return messagesResponse })

        let client = createObj [ "session", createObj [ "messages", box messagesFn ] ]

        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "input + cache.read of last assistant" (Some(8000 + 3000)) tokens

        let argsUsed = messagesCallArgs.[0]
        let path = Dyn.get argsUsed "path"
        let query = Dyn.get argsUsed "query"
        check "session.messages path.id" (Dyn.str path "id" = "sess-1")
        check "session.messages query.directory exists" (not (Dyn.isNullish query))
    }

let spec_tryCurrentUsage_noAssistantMessages_returnsNone () =
    promise {
        let mkUserMsg =
            createObj
                [ "info", box (createObj [ "id", box "u-1"; "role", box "user" ])
                  "parts", box [||] ]

        let messagesResponse = createObj [ "data", box [| mkUserMsg |] ]

        let messagesFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return messagesResponse })

        let client = createObj [ "session", createObj [ "messages", box messagesFn ] ]

        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "no assistant messages -> None" None tokens
    }

let spec_tryCurrentUsage_emptyData_returnsNone () =
    promise {
        let messagesResponse = createObj [ "data", box [||] ]

        let messagesFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return messagesResponse })

        let client = createObj [ "session", createObj [ "messages", box messagesFn ] ]

        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "empty data -> None" None tokens
    }

let spec_tryCurrentUsage_missingSessionMessages_returnsNone () =
    promise {
        let client = createObj []
        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "missing session.messages -> None" None tokens
    }

let spec_tryCurrentUsage_sumsInputAndCacheRead () =
    // Verify that the total = input + cache.read (not just input).
    promise {
        let mkAssistantMsg tokensInput cacheRead =
            createObj
                [ "info",
                  box (
                      createObj
                          [ "role", box "assistant"
                            "tokens",
                            box (
                                createObj
                                    [ "input", box tokensInput
                                      "cache", box (createObj [ "read", box cacheRead; "write", box 0 ]) ]
                            ) ]
                  )
                  "parts", box [||] ]

        let messagesResponse = createObj [ "data", box [| mkAssistantMsg 1000 500 |] ]

        let messagesFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return messagesResponse })

        let client = createObj [ "session", createObj [ "messages", box messagesFn ] ]

        let! tokens = tryCurrentUsage client "sess-1" [||]
        equal "input + cache.read = 1500" (Some 1500) tokens
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_tryEffectiveLimit_usesQueryDirectoryAndUnwrapsData ()
        do! spec_tryEffectiveLimit_idFallback ()
        do! spec_tryEffectiveLimit_missingModel_returnsNone ()
        do! spec_tryEffectiveLimit_missingProviderList_returnsNone ()
        do! spec_tryEffectiveLimit_modelNotInProvider_returnsNone ()
        do! spec_tryEffectiveLimit_unwrapsDataField ()
        do! spec_tryCurrentUsage_usesSessionMessages ()
        do! spec_tryCurrentUsage_noAssistantMessages_returnsNone ()
        do! spec_tryCurrentUsage_emptyData_returnsNone ()
        do! spec_tryCurrentUsage_missingSessionMessages_returnsNone ()
        do! spec_tryCurrentUsage_sumsInputAndCacheRead ()
    }
