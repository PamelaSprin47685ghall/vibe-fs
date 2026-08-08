namespace Wanxiangshu.Domain

open Wanxiangshu.Kernel

/// Sole identity directory for managed agents (AGENT-001…004).
///
/// Name, tier, role, peer, role groupings, and legacy rejection all derive here.
[<RequireQualifiedAccess>]
module ManagedAgentCatalog =

    let roleLabel (role: Role) : string =
        match role with
        | Role.Manager -> "manager"
        | Role.Orchestrator -> "orchestrator"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Meditator -> "meditator"
        | Role.Reviewer -> "reviewer"
        | Role.Student -> "student"
        | Role.Teacher -> "teacher"
        | Role.Executor -> "executor"
        | Role.Blogger -> "blogger"

    let tryParseRole (value: string) : Role option =
        match value.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "meditator" -> Some Role.Meditator
        | "reviewer" -> Some Role.Reviewer
        | "student" -> Some Role.Student
        | "teacher" -> Some Role.Teacher
        | "executor" -> Some Role.Executor
        | "blogger" -> Some Role.Blogger
        | _ -> None

    /// Journal / durable capitalised form (Fast / Deep).
    let tierLabel (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "Fast"
        | AgentTier.Deep -> "Deep"

    /// Wire spelling used in Host agent names (fast / deep).
    let wireTierLabel (tier: AgentTier) : string =
        match tier with
        | AgentTier.Fast -> "fast"
        | AgentTier.Deep -> "deep"

    let tryParseTier (value: string) : AgentTier option =
        match value.ToLowerInvariant() with
        | "fast" -> Some AgentTier.Fast
        | "deep" -> Some AgentTier.Deep
        | _ -> None

    let peerTier (tier: AgentTier) : AgentTier =
        match tier with
        | AgentTier.Fast -> AgentTier.Deep
        | AgentTier.Deep -> AgentTier.Fast

    let nameOf (tier: AgentTier) (role: Role) : string =
        sprintf "%s-%s" (wireTierLabel tier) (roleLabel role)

    let peerNameOf (tier: AgentTier) (role: Role) : string = nameOf (peerTier tier) role

    let allPublicRoles: Role list =
        [ Role.Orchestrator
          Role.Manager
          Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Meditator
          Role.Reviewer
          Role.Student ]

    let allInternalRoles: Role list = [ Role.Blogger; Role.Executor; Role.Teacher ]

    let allRoles: Role list = allPublicRoles @ allInternalRoles

    /// Manager fork-agent enum (AGENT-009 / GLORY-031): the Reviewer is
    /// Host-owned and does not exist on the Manager's surface. No
    /// orchestrator/manager/blogger/executor either.
    let managerForkableRoles: Role list =
        [ Role.Coder; Role.Inspector; Role.DevOps; Role.Browser; Role.Meditator ]

    let private namesFor (roles: Role list) : string list =
        roles
        |> List.collect (fun role -> [ nameOf AgentTier.Fast role; nameOf AgentTier.Deep role ])

    /// AGENT-002: the required 24 managed agent names.
    let requiredNames: string list = namesFor allRoles

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
              "reviewer"
              "student"
              "teacher"
              "blogger"
              "executor"
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
