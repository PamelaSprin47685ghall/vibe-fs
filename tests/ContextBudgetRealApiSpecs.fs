module Wanxiangshu.Tests.ContextBudgetRealApiSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.OpencodeContextBudgetObservation
open Wanxiangshu.Runtime.ContextBudgetResolve



// --- tryObserveLatestUsage ---

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

        let! observation = tryObserveLatestUsage client "sess-1" ""

        equal
            "input + cache.read of last assistant"
            (Some(8000L + 3000L))
            (observation |> Option.map (fun item -> item.InputTokens))

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

        let! tokens = tryObserveLatestUsage client "sess-1" ""
        equal "no assistant messages -> None" None tokens
    }

let spec_tryCurrentUsage_emptyData_returnsNone () =
    promise {
        let messagesResponse = createObj [ "data", box [||] ]

        let messagesFn =
            System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return messagesResponse })

        let client = createObj [ "session", createObj [ "messages", box messagesFn ] ]

        let! tokens = tryObserveLatestUsage client "sess-1" ""
        equal "empty data -> None" None tokens
    }

let spec_tryCurrentUsage_missingSessionMessages_returnsNone () =
    promise {
        let client = createObj []
        let! tokens = tryObserveLatestUsage client "sess-1" ""
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

        let! observation = tryObserveLatestUsage client "sess-1" ""
        equal "input + cache.read = 1500" (Some 1500L) (observation |> Option.map (fun item -> item.InputTokens))
    }

// --- helpers for provider-list regression spec ---

let private mkModelResponse () : obj =
    createObj
        [ "data",
          box (
              createObj
                  [ "model",
                    box (
                        createObj
                            [ "id", box "openai/gpt-4o"
                              "providerID", box "openai"
                              "limit", box (createObj [ "input", box 128000.0 ]) ]
                    ) ]
          ) ]

let private mkSessionGetFn (response: obj) : System.Func<obj, JS.Promise<obj>> =
    System.Func<obj, JS.Promise<obj>>(fun _ -> promise { return response })

let private mkProviderListFn (called: bool[]) : System.Func<obj, JS.Promise<obj>> =
    System.Func<obj, JS.Promise<obj>>(fun _ ->
        called.[0] <- true
        promise { return raise (System.Exception "provider.list must not be called") })

let private mkTarget
    (sessionGetFn: System.Func<obj, JS.Promise<obj>>)
    (providerListFn: System.Func<obj, JS.Promise<obj>>)
    : obj =
    createObj
        [ "session",
          box (
              createObj
                  [ "get", box sessionGetFn
                    "model",
                    box (
                        createObj
                            [ "id", box "openai/gpt-4o"
                              "providerID", box "openai"
                              "limit", box (createObj [ "input", box 128000.0 ]) ]
                    )
                    "provider", box (createObj [ "list", box providerListFn ]) ]
          ) ]

// --- resolveMaxInputTokens must not depend on provider.list ---

let spec_resolveMaxInputTokens_usesSessionModelLimit_withoutProviderList () =
    promise {
        let sessionGetResponse = mkModelResponse ()
        let sessionGetFn = mkSessionGetFn sessionGetResponse
        let providerListCalled = [| false |]
        let providerListFn = mkProviderListFn providerListCalled
        let target = mkTarget sessionGetFn providerListFn
        let! result = resolveMaxInputTokens [ target ] "sess-1" ""
        equal "returns session model limit.input (128000)" 128000 result
        check "provider.list was never called (legacy seam removed)" (not providerListCalled.[0])
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_tryCurrentUsage_usesSessionMessages ()
        do! spec_tryCurrentUsage_noAssistantMessages_returnsNone ()
        do! spec_tryCurrentUsage_emptyData_returnsNone ()
        do! spec_tryCurrentUsage_missingSessionMessages_returnsNone ()
        do! spec_tryCurrentUsage_sumsInputAndCacheRead ()
        do! spec_resolveMaxInputTokens_usesSessionModelLimit_withoutProviderList ()
    }
