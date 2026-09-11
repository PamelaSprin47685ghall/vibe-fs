namespace Wanxiangshu.Repository.Knowledge.Casebook

open Wanxiangshu.Persistence.EventStore

/// Casebook-owned canonical integration oracle. The persistence spine receives
/// it through explicit `CanonicalIntegrator.createWithRules` injection and
/// never references this module.
[<RequireQualifiedAccess>]
module CasebookIntegrationRules =

    val casebookRule: IntegrationRule
    val rules: IntegrationRule list
