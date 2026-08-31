namespace Wanxiangshu.Participant.Persona

open Wanxiangshu.Foundation

/// Sole identity directory for managed agents (AGENT-001…004).
///
/// Name, tier, role, peer, role groupings, and legacy rejection all derive here.
[<RequireQualifiedAccess>]
module ManagedAgentCatalog =

    let peerTier (tier: AgentTier) : AgentTier =
        match tier with
        | AgentTier.Fast -> AgentTier.Deep
        | AgentTier.Deep -> AgentTier.Fast

    let nameOf (tier: AgentTier) (role: Role) : string =
        sprintf "%s-%s" (Roles.wireTierLabel tier) (Roles.roleLabel role)

    let peerNameOf (tier: AgentTier) (role: Role) : string = nameOf (peerTier tier) role

    /// Lowercase Host schema label for the `calling` selector.
    let personaCallingName (persona: Persona) : string =
        match persona with
        | Persona.Integrator -> "integrator"
        | Persona.Director -> "director"
        | Persona.Coordinator -> "coordinator"
        | Persona.Lead -> "lead"
        | Persona.Coder -> "coder"
        | Persona.Engineer -> "engineer"
        | Persona.Scout -> "scout"
        | Persona.Investigator -> "investigator"
        | Persona.Technician -> "technician"
        | Persona.Operator -> "operator"
        | Persona.Navigator -> "navigator"
        | Persona.Researcher -> "researcher"
        | Persona.Analyst -> "analyst"
        | Persona.Inquirer -> "inquirer"
        | Persona.Examiner -> "examiner"
        | Persona.Auditor -> "auditor"
        | Persona.Scribe -> "scribe"
        | Persona.Chronicler -> "chronicler"
        | Persona.Condenser -> "condenser"
        | Persona.Distiller -> "distiller"
        | Persona.Clerk -> "clerk"
        | Persona.Curator -> "curator"

    /// Parse one exact lowercase Host `calling` label into typed identity.
    let tryParsePersonaCallingName (value: string) : Persona option =
        match value with
        | "integrator" -> Some Persona.Integrator
        | "director" -> Some Persona.Director
        | "coordinator" -> Some Persona.Coordinator
        | "lead" -> Some Persona.Lead
        | "coder" -> Some Persona.Coder
        | "engineer" -> Some Persona.Engineer
        | "scout" -> Some Persona.Scout
        | "investigator" -> Some Persona.Investigator
        | "technician" -> Some Persona.Technician
        | "operator" -> Some Persona.Operator
        | "navigator" -> Some Persona.Navigator
        | "researcher" -> Some Persona.Researcher
        | "analyst" -> Some Persona.Analyst
        | "inquirer" -> Some Persona.Inquirer
        | "examiner" -> Some Persona.Examiner
        | "auditor" -> Some Persona.Auditor
        | "scribe" -> Some Persona.Scribe
        | "chronicler" -> Some Persona.Chronicler
        | "condenser" -> Some Persona.Condenser
        | "distiller" -> Some Persona.Distiller
        | "clerk" -> Some Persona.Clerk
        | "curator" -> Some Persona.Curator
        | _ -> None

    let allRoles: Role list = Roles.all

    let allPublicRoles: Role list = allRoles |> List.filter (Roles.isInternal >> not)

    let allInternalRoles: Role list = allRoles |> List.filter Roles.isInternal

    /// Manager fork-agent enum (AGENT-009 / GLORY-031): the Reviewer is
    /// Host-owned and does not exist on the Manager's surface. No
    /// orchestrator/manager/blogger/distiller either.
    let managerForkableRoles: Role list =
        [ Role.Coder; Role.Inspector; Role.DevOps; Role.Browser; Role.Inquiry ]

    let private namesFor (roles: Role list) : string list =
        roles
        |> List.collect (fun role -> [ nameOf AgentTier.Fast role; nameOf AgentTier.Deep role ])

    /// AGENT-002: InternalLeaf Bookkeeper pair — not a public Role.
    let bookkeeperNames: string list = [ "fast-bookkeeper"; "deep-bookkeeper" ]

    let isBookkeeperName (name: string) : bool =
        let lower = name.ToLowerInvariant()
        lower = "fast-bookkeeper" || lower = "deep-bookkeeper"

    let tryParseBookkeeperTier (name: string) : AgentTier option =
        match name.ToLowerInvariant() with
        | "fast-bookkeeper" -> Some AgentTier.Fast
        | "deep-bookkeeper" -> Some AgentTier.Deep
        | _ -> None

    let bookkeeperNameOf (tier: AgentTier) : string =
        sprintf "%s-bookkeeper" (Roles.wireTierLabel tier)

    let bookkeeperPeerName (name: string) : string option =
        match tryParseBookkeeperTier name with
        | Some tier -> Some(bookkeeperNameOf (peerTier tier))
        | None -> None

    /// AGENT-002: the required 22 managed agent names (20 Role × tier + Bookkeeper pair).
    let requiredNames: string list = namesFor allRoles @ bookkeeperNames

    let managerForkableNames: string list = namesFor managerForkableRoles

    let orchestratorForkableNames: string list =
        [ nameOf AgentTier.Fast Role.Manager; nameOf AgentTier.Deep Role.Manager ]

    let inspectorToolNames: string list =
        [ nameOf AgentTier.Fast Role.Inspector; nameOf AgentTier.Deep Role.Inspector ]

    let coderToolNames: string list =
        [ nameOf AgentTier.Fast Role.Coder; nameOf AgentTier.Deep Role.Coder ]

    /// AGENT-004 exact bare names. Shape variants (underscore, reversed suffix) are
    /// covered by `isLegacyAgentName` patterns rather than an open-ended string list.
    let legacyAgentNames: Set<string> =
        set
            [ "orchestrator"
              "manager"
              "build"
              "plan"
              "coder"
              "inspector"
              "devops"
              "browser"
              "meditator"
              "inquiry"
              "reviewer"
              "student"
              "teacher"
              "blogger"
              "executor"
              "distiller"
              "bookkeeper"
              "fast"
              "deep" ]

    /// AGENT-004: exact legacy names plus forbidden shapes (no alias, no autocomplete).
    let isLegacyAgentName (lower: string) : bool =
        legacyAgentNames.Contains lower
        || lower.Contains("_")
        || lower.EndsWith("-fast")
        || lower.EndsWith("-deep")
        || lower.StartsWith("fast_")
        || lower.StartsWith("deep_")

    let formatLegacyNameNotSupported (name: string) : string =
        sprintf "Legacy agent name '%s' is not supported. Managed agents require explicit fast-/deep- names." name

    let formatLegacyNameInConfig (name: string) : string =
        sprintf
            "Legacy agent name '%s' is present in opencode.json. Managed agents require explicit fast-/deep- names."
            name
