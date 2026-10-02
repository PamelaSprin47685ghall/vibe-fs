namespace Wanxiangshu.Enforcer

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Foundation
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Resources

module EnforcerSurface =

    val rules: unit -> obj array

    /// Same packaged English rulebook load as `rules`, projected as
    /// `{ ok: true, value }` / `{ ok: false, error }`. Callers and semantic tests
    /// need to branch on a machine-readable cause rather than match exception
    /// prose, while the rulebook load itself stays a single decision in
    /// `rules`: `tryRules` only changes how a failure leaves the boundary.
    val tryRules: unit -> obj
    val ruleCount: unit -> int
    val fieldNames: unit -> string array
    val chronicleExecutionContract: bool -> obj
    val tryFindByField: string -> obj
    val validate: int -> obj array -> obj
    val decodeCall: obj -> obj
    val missingTipError: string
    val hasValidText: obj -> bool
    val canonicalCycle: obj -> obj
    val maxBlogTextBytes: int
    val maxEvidenceBytes: int
    val composeBloggerSystemPrompt: string -> string -> string
    val loadFor: string -> obj array
    val validateBounds: string -> string option -> obj
    val resolveField: field: string -> rules: obj array -> obj
