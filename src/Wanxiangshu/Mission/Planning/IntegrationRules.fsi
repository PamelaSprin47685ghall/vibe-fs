namespace Wanxiangshu.Mission.Planning

open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module PlanIntegrationRules =
    val planRule: IntegrationRule
    /// The one lifecycle vocabulary's integration rule.
    val lifecycleRule: IntegrationRule
    val rules: IntegrationRule list
