namespace Wanxiangshu.Persistence.EventStore

/// Structural frontier oracle. It sees every durable event but owns no business
/// meaning; DomainConflict is simply `heads.Count > 1` in this Integrator slot.
[<RequireQualifiedAccess>]
module StructuralIntegration =
    val rule: IntegrationRule

[<RequireQualifiedAccess>]
module IntegratorEngine =
    val create: rules: IntegrationRule list -> isEventTypeKnown: (string -> bool) -> ICanonicalIntegrator
