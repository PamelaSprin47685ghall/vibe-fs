namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

module EnforcerSurface =

    val rules: unit -> obj array
    val ruleCount: unit -> int
    val fieldNames: unit -> string array
    val chronicleExecutionContract: bool -> obj
    val tryFindByField: string -> obj
    val validate: int -> obj array -> obj
    val decodeCall: obj -> obj
    val missingTipError: string
    val hasValidText: obj -> bool
    val canonicalCycle: obj -> obj
    val isValidCycle: obj -> bool
    val maxBlogTextBytes: int
    val maxEvidenceBytes: int
    val composeBloggerSystemPrompt: string -> string -> string
    val loadFor: string -> obj array
    val validateBounds: string -> string option -> obj
    val validateProviderRun: string -> obj
    val classifyAssistantStep: obj -> obj
