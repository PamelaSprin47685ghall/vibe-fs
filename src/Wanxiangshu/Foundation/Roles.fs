namespace Wanxiangshu.Foundation

/// DSL-class: Vocabulary — the fixed set of managed agent roles (one
/// vocabulary, no control-flow reading).
[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Inquiry
    | DevOps
    | Distiller
    | Blogger

module Roles =

    let all: Role list =
        [ Role.Manager
          Role.Coder
          Role.Inspector
          Role.DevOps
          Role.Browser
          Role.Inquiry
          Role.Orchestrator
          Role.Distiller
          Role.Blogger ]

    /// Canonical wire label for a role (lowercase, AGENT-001 vocabulary).
    let roleLabel (role: Role) : string =
        match role with
        | Role.Manager -> "manager"
        | Role.Orchestrator -> "orchestrator"
        | Role.Coder -> "coder"
        | Role.Inspector -> "inspector"
        | Role.DevOps -> "devops"
        | Role.Browser -> "browser"
        | Role.Inquiry -> "inquiry"
        | Role.Distiller -> "distiller"
        | Role.Blogger -> "blogger"

    let tryParseRole (value: string) : Role option =
        match value.ToLowerInvariant() with
        | "manager" -> Some Role.Manager
        | "orchestrator" -> Some Role.Orchestrator
        | "coder" -> Some Role.Coder
        | "inspector" -> Some Role.Inspector
        | "devops" -> Some Role.DevOps
        | "browser" -> Some Role.Browser
        | "inquiry" -> Some Role.Inquiry
        | "distiller" -> Some Role.Distiller
        | "blogger" -> Some Role.Blogger
        | _ -> None

    /// AGENT-008 / ENF-006: Distiller and Blogger are private runtimes, not
    /// public fork / horizon vocabulary.
    let isInternal (role: Role) : bool =
        match role with
        | Role.Blogger
        | Role.Distiller -> true
        | _ -> false
