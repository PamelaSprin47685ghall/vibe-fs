namespace Wanxiangshu.Sphinx

open Wanxiangshu.Persistence.EventStore

/// Sphinx-owned canonical integration oracles. These are the only rules that
/// fold sphinx durable events; the persistence spine receives them through
/// explicit `CanonicalIntegrator.createWithRules` injection and never
/// references this module.
[<RequireQualifiedAccess>]
module SphinxIntegrationRules =
    val sphinxRule: IntegrationRule
    val sphinxGenericRule: IntegrationRule
    val rules: IntegrationRule list
