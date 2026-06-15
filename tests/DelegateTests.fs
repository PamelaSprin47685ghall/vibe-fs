module VibeFs.Tests.DelegateTests

open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Dyn
open VibeFs.MuxPlugin.Delegate

let private muxEnv (pairs: (string * string) list) : obj =
    createObj [ for k, v in pairs -> k, box v ]

let private configWithMuxEnv (pairs: (string * string) list) : obj =
    createObj [ "muxEnv", box (muxEnv pairs) ]

let coerceThinking () =
    equal "med → medium" (Some "medium") (coerceThinkingLevel "med")
    equal "high unchanged" (Some "high") (coerceThinkingLevel "high")
    check "invalid → none" (coerceThinkingLevel "bogus" = None)
    check "empty → none" (coerceThinkingLevel "" = None)

let parentRuntime () =
    let noneCfg = configWithMuxEnv []
    check "no mux keys → null" (isNullish (buildParentRuntimeAiSettings noneCfg))

    let modelOnly = configWithMuxEnv [ "MUX_MODEL_STRING", "openai:gpt-4o-mini" ]
    let mOnly = buildParentRuntimeAiSettings modelOnly
    equal "model only" "openai:gpt-4o-mini" (str mOnly "modelString")
    check "thinking omitted" (not (has mOnly "thinkingLevel"))

    let both =
        configWithMuxEnv
            [ "MUX_MODEL_STRING", "anthropic:claude-sonnet-4-6"
              "MUX_THINKING_LEVEL", "med" ]

    let bothObj = buildParentRuntimeAiSettings both
    equal "both model" "anthropic:claude-sonnet-4-6" (str bothObj "modelString")
    equal "both thinking" "medium" (str bothObj "thinkingLevel")

let run () =
    coerceThinking ()
    parentRuntime ()
