namespace Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type AgentTier =
    | Fast
    | Deep

[<RequireQualifiedAccess>]
type Role =
    | Manager
    | Orchestrator
    | Coder
    | Inspector
    | Browser
    | Inquiry
    | Reviewer
    | DevOps
    | Distiller
    | Blogger

module Roles =
    val all: Role list
    val roleLabel: Role -> string
    val tryParseRole: string -> Role option
    val wireTierLabel: AgentTier -> string
    val tryParseTier: string -> AgentTier option
    val isInternal: Role -> bool
