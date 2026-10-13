namespace Wanxiangshu.Mission.Lifecycle

open Wanxiangshu.Persistence.EventStore

[<RequireQualifiedAccess>]
module LifecycleIntegrationRules =
    /// The one lifecycle vocabulary's integration rule.
    val rule: IntegrationRule
    val rules: IntegrationRule list
