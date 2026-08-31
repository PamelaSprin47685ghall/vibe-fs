namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type Persona =
    | Integrator
    | Director
    | Coordinator
    | Lead
    | Coder
    | Engineer
    | Scout
    | Investigator
    | Technician
    | Operator
    | Navigator
    | Researcher
    | Analyst
    | Inquirer
    | Examiner
    | Auditor
    | Scribe
    | Chronicler
    | Condenser
    | Distiller
    | Clerk
    | Curator

[<RequireQualifiedAccess>]
module Persona =

    let render (persona: Persona) : string =
        match persona with
        | Persona.Integrator -> "Integrator"
        | Persona.Director -> "Director"
        | Persona.Coordinator -> "Coordinator"
        | Persona.Lead -> "Lead"
        | Persona.Coder -> "Coder"
        | Persona.Engineer -> "Engineer"
        | Persona.Scout -> "Scout"
        | Persona.Investigator -> "Investigator"
        | Persona.Technician -> "Technician"
        | Persona.Operator -> "Operator"
        | Persona.Navigator -> "Navigator"
        | Persona.Researcher -> "Researcher"
        | Persona.Analyst -> "Analyst"
        | Persona.Inquirer -> "Inquirer"
        | Persona.Examiner -> "Examiner"
        | Persona.Auditor -> "Auditor"
        | Persona.Scribe -> "Scribe"
        | Persona.Chronicler -> "Chronicler"
        | Persona.Condenser -> "Condenser"
        | Persona.Distiller -> "Distiller"
        | Persona.Clerk -> "Clerk"
        | Persona.Curator -> "Curator"

    let tryParse (label: string) : Persona option =
        match label with
        | "Integrator" -> Some Persona.Integrator
        | "Director" -> Some Persona.Director
        | "Coordinator" -> Some Persona.Coordinator
        | "Lead" -> Some Persona.Lead
        | "Coder" -> Some Persona.Coder
        | "Engineer" -> Some Persona.Engineer
        | "Scout" -> Some Persona.Scout
        | "Investigator" -> Some Persona.Investigator
        | "Technician" -> Some Persona.Technician
        | "Operator" -> Some Persona.Operator
        | "Navigator" -> Some Persona.Navigator
        | "Researcher" -> Some Persona.Researcher
        | "Analyst" -> Some Persona.Analyst
        | "Inquirer" -> Some Persona.Inquirer
        | "Examiner" -> Some Persona.Examiner
        | "Auditor" -> Some Persona.Auditor
        | "Scribe" -> Some Persona.Scribe
        | "Chronicler" -> Some Persona.Chronicler
        | "Condenser" -> Some Persona.Condenser
        | "Distiller" -> Some Persona.Distiller
        | "Clerk" -> Some Persona.Clerk
        | "Curator" -> Some Persona.Curator
        | _ -> None

/// AGENT-028: Role × initial selected tier → the persona embedded in ParticipantIdentity.
/// IdentitySeed resolves it once. Bookkeeper is InternalLeaf — not a public Role;
/// use `bookkeeperPersona`.
[<RequireQualifiedAccess>]
module PersonaCatalog =

    let persona (role: Role) (tier: AgentTier) : Persona =
        match role, tier with
        | Role.Orchestrator, AgentTier.Fast -> Persona.Integrator
        | Role.Orchestrator, AgentTier.Deep -> Persona.Director
        | Role.Manager, AgentTier.Fast -> Persona.Coordinator
        | Role.Manager, AgentTier.Deep -> Persona.Lead
        | Role.Coder, AgentTier.Fast -> Persona.Coder
        | Role.Coder, AgentTier.Deep -> Persona.Engineer
        | Role.Inspector, AgentTier.Fast -> Persona.Scout
        | Role.Inspector, AgentTier.Deep -> Persona.Investigator
        | Role.DevOps, AgentTier.Fast -> Persona.Technician
        | Role.DevOps, AgentTier.Deep -> Persona.Operator
        | Role.Browser, AgentTier.Fast -> Persona.Navigator
        | Role.Browser, AgentTier.Deep -> Persona.Researcher
        | Role.Inquiry, AgentTier.Fast -> Persona.Analyst
        | Role.Inquiry, AgentTier.Deep -> Persona.Inquirer
        | Role.Reviewer, AgentTier.Fast -> Persona.Examiner
        | Role.Reviewer, AgentTier.Deep -> Persona.Auditor
        | Role.Blogger, AgentTier.Fast -> Persona.Scribe
        | Role.Blogger, AgentTier.Deep -> Persona.Chronicler
        | Role.Distiller, AgentTier.Fast -> Persona.Condenser
        | Role.Distiller, AgentTier.Deep -> Persona.Distiller

    let bookkeeperPersona (tier: AgentTier) : Persona =
        match tier with
        | AgentTier.Fast -> Persona.Clerk
        | AgentTier.Deep -> Persona.Curator

    let personaV1 (role: Role) (tier: AgentTier) : string = persona role tier |> Persona.render

    let bookkeeperPersonaV1 (tier: AgentTier) : string = bookkeeperPersona tier |> Persona.render

    /// HOST-026 analogue: child / attached / InternalLeaf ParticipantIdentity inherits the owner persona.
    let inheritFrom (ownerPersona: string) : string = ownerPersona
