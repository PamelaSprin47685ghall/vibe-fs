namespace Wanxiangshu.Strength

open Wanxiangshu.Persistence.EventStore

/// Strength-owned canonical integration oracle. The persistence spine receives
/// it through explicit `CanonicalIntegrator.createWithRules` injection and
/// never references this module.
[<RequireQualifiedAccess>]
module StrengthIntegrationRules =
    val strengthRule: IntegrationRule
    val rules: IntegrationRule list
