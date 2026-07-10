/// RED tests: verify ContextBudgetUsageCodec reads real OpenCode v1 SDK shapes.
/// Session = { tokens?: {input,output,reasoning,cache:{read,write}}, model?: {id,providerID,variant?} }
/// Model = { limit: {context, input?, output} }
/// session.get({sessionID}) → {data: Session}
/// provider.list() → {data: {all: Provider[]}}
/// Provider = { id, models: {[modelId]: Model} }
module Wanxiangshu.Tests.ContextBudgetRealApiSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.ContextBudgetUsageCodec

/// RED: tryGetMaxInputTokensAsync must call session.get({sessionID})
/// then provider.list() and read model.limit.input or .context
let spec_tryGetMaxInputTokensAsync_realSchema () =
    promise {
        let mutable capturedSessionArg = null
        let mutable providerListCalled = false

        let mockSessionGet (arg: obj) =
            promise {
                capturedSessionArg <- arg

                return
                    createObj
                        [ "data",
                          createObj
                              [ "model", createObj [ "id", box "claude-sonnet-4"; "providerID", box "anthropic" ] ] ]
            }

        let mockProviderList (_arg: obj) =
            promise {
                providerListCalled <- true

                return
                    createObj
                        [ "data",
                          createObj
                              [ "all",
                                box
                                    [| createObj
                                           [ "id", box "anthropic"
                                             "models",
                                             createObj
                                                 [ "claude-sonnet-4",
                                                   createObj
                                                       [ "limit",
                                                         createObj
                                                             [ "context", box 200000
                                                               "input", box 200000
                                                               "output", box 16000 ] ] ] ] |] ] ]
            }

        let client =
            createObj
                [ "session", createObj [ "get", box (System.Func<obj, JS.Promise<obj>>(mockSessionGet)) ]
                  "provider", createObj [ "list", box (System.Func<obj, JS.Promise<obj>>(mockProviderList)) ] ]

        let! limit = tryGetMaxInputTokensAsync client "s-async"
        equal "limit.input from provider.list" (Some 200000) limit

        check
            "session.get flat sessionID arg"
            (not (isNullish capturedSessionArg)
             && string (get capturedSessionArg "sessionID") = "s-async")

        check "provider.list called" providerListCalled
    }

/// RED: tryGetMaxInputTokensAsync with only limit.context (no input)
let spec_tryGetMaxInputTokensAsync_contextFallback () =
    promise {
        let mockGet (_arg: obj) =
            promise {
                return
                    createObj
                        [ "data", createObj [ "model", createObj [ "id", box "gpt-4"; "providerID", box "openai" ] ] ]
            }

        let mockList (_arg: obj) =
            promise {
                return
                    createObj
                        [ "data",
                          createObj
                              [ "all",
                                box
                                    [| createObj
                                           [ "id", box "openai"
                                             "models",
                                             createObj
                                                 [ "gpt-4",
                                                   createObj
                                                       [ "limit",
                                                         createObj [ "context", box 128000; "output", box 4096 ] ] ] ] |] ] ]
            }

        let client =
            createObj
                [ "session", createObj [ "get", box (System.Func<obj, JS.Promise<obj>>(mockGet)) ]
                  "provider", createObj [ "list", box (System.Func<obj, JS.Promise<obj>>(mockList)) ] ]

        let! limit = tryGetMaxInputTokensAsync client "s-ctx"
        equal "fallback to limit.context" (Some 128000) limit
    }

/// RED: null client → None
let spec_tryGetMaxInputTokensAsync_nullClient () =
    promise {
        let! limit = tryGetMaxInputTokensAsync (box null) "s-null"
        equal "null client → None" None limit
    }

/// RED: tryGetRealContextUsage must call session.get({sessionID})
/// and read tokens.input + tokens.cache.read (no total field)
let spec_tryGetRealContextUsage_realSchema () =
    promise {
        let mutable capturedArg = null

        let mockGet (arg: obj) =
            promise {
                capturedArg <- arg

                return
                    createObj
                        [ "data",
                          createObj
                              [ "tokens",
                                createObj
                                    [ "input", box 45000
                                      "output", box 5000
                                      "reasoning", box 1000
                                      "cache", createObj [ "read", box 5000; "write", box 0 ] ] ] ]
            }

        let client =
            createObj [ "session", createObj [ "get", box (System.Func<obj, JS.Promise<obj>>(mockGet)) ] ]

        let opt = tryGetRealContextUsage client "s-real"
        check "Some func returned" opt.IsSome
        let! tokens = opt.Value [||]
        // input + cache.read = 45000 + 5000 = 50000
        equal "tokens = input + cache.read" (Some 50000) tokens
        check "flat sessionID arg" (not (isNullish capturedArg) && string (get capturedArg "sessionID") = "s-real")
    }

/// RED: session with no tokens → None
let spec_tryGetRealContextUsage_noTokens () =
    promise {
        let mockGet (_arg: obj) =
            promise { return createObj [ "data", createObj [ "title", box "no tokens" ] ] }

        let client =
            createObj [ "session", createObj [ "get", box (System.Func<obj, JS.Promise<obj>>(mockGet)) ] ]

        let opt = tryGetRealContextUsage client "s-no-tokens"
        check "Some func returned" opt.IsSome
        let! tokens = opt.Value [||]
        equal "no tokens → None" None tokens
    }

/// RED: resolveMaxInputTokens fallback must be 0, not 200000
let spec_resolveMaxInputTokens_zeroFallback () =
    promise {
        let! res = resolveMaxInputTokens [ createObj [] ] "sess-id"
        equal "fallback should be 0" 0 res

        let t =
            createObj
                [ "session",
                  createObj [ "model", createObj [ "limit", createObj [ "context", box 60000; "output", box 4000 ] ] ] ]

        let! res2 = resolveMaxInputTokens [ t ] "sess-id"
        equal "sync priority" 60000 res2
    }

/// RED: resolveMaxInputTokens must prefer async limit.input over sync limit.context when input < context
let spec_resolveMaxInputTokens_preferInputOverContextAcrossSyncAsync () =
    promise {
        let mockGet (_arg: obj) =
            promise {
                return
                    createObj
                        [ "data",
                          createObj
                              [ "model", createObj [ "id", box "claude-sonnet-4"; "providerID", box "anthropic" ] ] ]
            }

        let mockList (_arg: obj) =
            promise {
                return
                    createObj
                        [ "data",
                          createObj
                              [ "all",
                                box
                                    [| createObj
                                           [ "id", box "anthropic"
                                             "models",
                                             createObj
                                                 [ "claude-sonnet-4",
                                                   createObj
                                                       [ "limit",
                                                         createObj
                                                             [ "context", box 200000
                                                               "input", box 100000
                                                               "output", box 16000 ] ] ] ] |] ] ]
            }

        let t =
            createObj
                [ "session",
                  createObj
                      [ "model", createObj [ "limit", createObj [ "context", box 200000; "output", box 16000 ] ]
                        "get", box (System.Func<obj, JS.Promise<obj>>(mockGet)) ]
                  "provider", createObj [ "list", box (System.Func<obj, JS.Promise<obj>>(mockList)) ] ]

        let! limit = resolveMaxInputTokens [ t ] "sess-tdd-limit"
        equal "prefer async input limit (100000) over sync context limit (200000)" 100000 limit
    }

let run () : JS.Promise<unit> =
    promise {
        do! spec_tryGetMaxInputTokensAsync_realSchema ()
        do! spec_tryGetMaxInputTokensAsync_contextFallback ()
        do! spec_tryGetMaxInputTokensAsync_nullClient ()
        do! spec_tryGetRealContextUsage_realSchema ()
        do! spec_tryGetRealContextUsage_noTokens ()
        do! spec_resolveMaxInputTokens_zeroFallback ()
        do! spec_resolveMaxInputTokens_preferInputOverContextAcrossSyncAsync ()
    }
