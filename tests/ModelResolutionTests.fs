module Wanxiangshu.Tests.ModelResolutionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Hosts.Opencode.OpenCodeModelResolution

let private createMockClient (catalogData: obj array) : obj =
    let providerApi =
        createObj [ "list", box (fun (arg: obj) -> promise { return createObj [ "data", box catalogData ] }) ]

    let sessionApi =
        createObj
            [ "get",
              box (fun (arg: obj) ->
                  promise {
                      return
                          createObj
                              [ "data",
                                box (
                                    createObj
                                        [ "model",
                                          box (
                                              createObj
                                                  [ "providerID", box "anthropic"; "modelID", box "claude-3-opus" ]
                                          ) ]
                                ) ]
                  }) ]

    createObj [ "provider", box providerApi; "session", box sessionApi ]

let testComputeUsableInputTokens () =
    promise {
        // Test the formula: max(0, context - min(nonzero output, 32000))
        // context=128000, output=16000 → usable = 128000 - 16000 = 112000
        let result1 = computeUsableInputTokens 128000 16000
        equal "128k context, 16k output → 112k usable" 112000 result1

        // context=200000, output=64000 → usable = 200000 - 32000 = 168000 (capped at 32000)
        let result2 = computeUsableInputTokens 200000 64000
        equal "200k context, 64k output → 168k usable (output capped)" 168000 result2

        // context=8192, output=0 → usable = 8192 - 32000 = 0 (clamped to 0)
        let result3 = computeUsableInputTokens 8192 0
        equal "8k context, 0 output → 0 usable (clamped)" 0 result3

        // context=40000, output=4000 → usable = 40000 - 4000 = 36000
        let result4 = computeUsableInputTokens 40000 4000
        equal "40k context, 4k output → 36k usable" 36000 result4
    }

let testExtractLimitFromCatalogEntry () =
    promise {
        // Test with context and output
        let entry1 =
            createObj [ "limit", box (createObj [ "context", box 128000; "output", box 16000 ]) ]

        let result1 = extractLimitFromCatalogEntry entry1
        equal "extract context+output" (Some(128000, 16000)) result1

        // Test with input (newer SDK)
        let entry2 =
            createObj [ "limit", box (createObj [ "input", box 200000; "output", box 8000 ]) ]

        let result2 = extractLimitFromCatalogEntry entry2
        equal "extract input+output" (Some(200000, 8000)) result2

        // Test with only context
        let entry3 = createObj [ "limit", box (createObj [ "context", box 32000 ]) ]

        let result3 = extractLimitFromCatalogEntry entry3
        equal "extract context only" (Some(32000, 0)) result3

        // Test with no limit
        let entry4 = createObj []
        let result4 = extractLimitFromCatalogEntry entry4
        equal "no limit → None" None result4
    }

let testFindModelInCatalog () =
    promise {
        let catalog =
            [| createObj
                   [ "providerID", box "anthropic"
                     "id", box "claude-3-opus"
                     "limit", box (createObj [ "context", box 200000 ]) ]
               createObj
                   [ "providerID", box "openai"
                     "id", box "gpt-4"
                     "limit", box (createObj [ "context", box 128000 ]) ] |]

        let found1 = findModelInCatalog (box catalog) "anthropic" "claude-3-opus"
        check "found anthropic model" found1.IsSome

        let found2 = findModelInCatalog (box catalog) "openai" "gpt-4"
        check "found openai model" found2.IsSome

        let found3 = findModelInCatalog (box catalog) "anthropic" "claude-3-sonnet"
        check "not found unknown model" found3.IsNone
    }

let testResolveModelResolution () =
    promise {
        let catalog =
            [| createObj
                   [ "providerID", box "anthropic"
                     "id", box "claude-3-opus"
                     "limit", box (createObj [ "context", box 128000; "output", box 16000 ]) ] |]

        let client = createMockClient catalog

        let! result = resolveModelResolution client "anthropic" "claude-3-opus" "/test/dir"

        equal "provider ID" "anthropic" result.ProviderID
        equal "model ID" "claude-3-opus" result.ModelID
        equal "usable input tokens" 112000 result.UsableInputTokens
        equal "source" "provider-catalog-context-reserved" result.Source
    }

let testResolveModelResolutionFallback () =
    promise {
        let emptyCatalog = [||]
        let client = createMockClient emptyCatalog

        let! result = resolveModelResolution client "anthropic" "unknown-model" "/test/dir"

        equal "provider ID" "anthropic" result.ProviderID
        equal "model ID" "unknown-model" result.ModelID
        equal "fallback tokens" 8192 result.UsableInputTokens
        equal "source" "fallback-8192" result.Source
    }

let testResolveModelResolutionNullClient () =
    promise {
        let! result = resolveModelResolution null "anthropic" "claude-3-opus" "/test/dir"

        equal "fallback tokens on null client" 8192 result.UsableInputTokens
        equal "source on null client" "fallback-8192" result.Source
    }

let run () =
    promise {
        do! testComputeUsableInputTokens ()
        do! testExtractLimitFromCatalogEntry ()
        do! testFindModelInCatalog ()
        do! testResolveModelResolution ()
        do! testResolveModelResolutionFallback ()
        do! testResolveModelResolutionNullClient ()
    }
