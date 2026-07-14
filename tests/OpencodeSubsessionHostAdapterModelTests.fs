module Wanxiangshu.Tests.OpencodeSubsessionHostAdapterModelTests

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Opencode.SubsessionHostAdapter
open Wanxiangshu.Tests.Assert

let private model0: FallbackModel =
    { ProviderID = "openai"
      ModelID = "gpt-5"
      Variant = None
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let private modelWithVariant: FallbackModel = { model0 with Variant = Some "high" }

/// DelegateToHost semantics: None model must format to None — no model field
/// is ever passed to OpenCode's session.prompt, letting agent.<name>.model
/// (opencode.jsonc static config) or currentModel resolution take effect.
let noneModelFormatsToNone () =
    equal "DelegateToHost formats to None" None (buildDispatchModelString None)

let someModelWithoutVariantFormatsProviderSlashModel () =
    equal "provider/model, no variant" (Some "openai/gpt-5") (buildDispatchModelString (Some model0))

let someModelWithVariantFormatsProviderSlashModelColonVariant () =
    equal "provider/model:variant" (Some "openai/gpt-5:high") (buildDispatchModelString (Some modelWithVariant))

let run () =
    noneModelFormatsToNone ()
    someModelWithoutVariantFormatsProviderSlashModel ()
    someModelWithVariantFormatsProviderSlashModelColonVariant ()
