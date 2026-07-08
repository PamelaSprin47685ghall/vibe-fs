module Wanxiangshu.Tests.AgentConfigApplyTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Shell.Dyn

let applyAgentConfigForNullCoderEntryUsesDefaults () =
    let cfg = createObj [ "agent", box (createObj [ "coder", null ]) ]
    let next = applyAgentConfigFor opencode cfg (createObj [])
    let coder = get (get next "agent") "coder"
    check "coder not nullish" (not (isNullish coder))
    equal "coder default mode" "subagent" (str coder "mode")
    let tools = get coder "tools"
    check "coder tools object" (not (isNullish tools))
    check "coder tools.glob present" (has tools "glob")

let applyFallbackModelOverridesSplitsModelAndVariant () =
    let cfg =
        createObj
            [ "agent",
              box (
                  createObj
                      [ "build", box (createObj [])
                        "coder", box (createObj [ "variant", box "stale" ]) ]
              ) ]

    let fbCfg: FallbackConfig =
        { DefaultChain =
            [ { ProviderID = "openai"
                ModelID = "gpt-5"
                Variant = None
                Temperature = None
                TopP = None
                MaxTokens = None
                ReasoningEffort = None
                Thinking = false } ]
          AgentChains =
            Map.ofList
                [ "build",
                  [ { ProviderID = "google"
                      ModelID = "gemini-3.5-flash"
                      Variant = Some "high"
                      Temperature = None
                      TopP = None
                      MaxTokens = None
                      ReasoningEffort = None
                      Thinking = false } ] ]
          MaxRetries = 2
          LoopMaxContinues = 3 }

    Wanxiangshu.Opencode.PluginCore.applyFallbackModelOverrides cfg (Some fbCfg)

    let agentObj = get cfg "agent"
    let buildObj = get agentObj "build"
    let coderObj = get agentObj "coder"

    equal "build model" "google/gemini-3.5-flash" (str buildObj "model")
    equal "build variant" "high" (str buildObj "variant")
    equal "coder model" "openai/gpt-5" (str coderObj "model")
    check "coder variant should be removed" (isNullish (get coderObj "variant"))

let run () =
    applyAgentConfigForNullCoderEntryUsesDefaults ()
    applyFallbackModelOverridesSplitsModelAndVariant ()
