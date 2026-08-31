namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

/// AGENT-028: Role × initial selected tier → the persona embedded in ParticipantIdentity.
/// IdentitySeed resolves it once. Bookkeeper is InternalLeaf — not a public Role;
/// use `bookkeeperPersona`.
[<RequireQualifiedAccess>]
module PersonaCatalog =

    let personaV1 (role: Role) (tier: AgentTier) : string =
        match role, tier with
        | Role.Orchestrator, AgentTier.Fast -> "Integrator"
        | Role.Orchestrator, AgentTier.Deep -> "Director"
        | Role.Manager, AgentTier.Fast -> "Coordinator"
        | Role.Manager, AgentTier.Deep -> "Lead"
        | Role.Coder, AgentTier.Fast -> "Coder"
        | Role.Coder, AgentTier.Deep -> "Engineer"
        | Role.Inspector, AgentTier.Fast -> "Scout"
        | Role.Inspector, AgentTier.Deep -> "Investigator"
        | Role.DevOps, AgentTier.Fast -> "Technician"
        | Role.DevOps, AgentTier.Deep -> "Operator"
        | Role.Browser, AgentTier.Fast -> "Navigator"
        | Role.Browser, AgentTier.Deep -> "Researcher"
        | Role.Inquiry, AgentTier.Fast -> "Analyst"
        | Role.Inquiry, AgentTier.Deep -> "Inquirer"
        | Role.Reviewer, AgentTier.Fast -> "Examiner"
        | Role.Reviewer, AgentTier.Deep -> "Auditor"
        | Role.Blogger, AgentTier.Fast -> "Scribe"
        | Role.Blogger, AgentTier.Deep -> "Chronicler"
        | Role.Distiller, AgentTier.Fast -> "Condenser"
        | Role.Distiller, AgentTier.Deep -> "Distiller"

    let bookkeeperPersonaV1 (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "Clerk"
        | AgentTier.Deep -> "Curator"

    let persona (role: Role) (tier: AgentTier) : string = personaV1 role tier

    let bookkeeperPersona (tier: AgentTier) : string = bookkeeperPersonaV1 tier

    /// HOST-026 analogue: child / attached / InternalLeaf ParticipantIdentity inherits the owner persona.
    let inheritFrom (ownerPersona: string) : string = ownerPersona
