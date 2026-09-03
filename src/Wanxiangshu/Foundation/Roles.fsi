namespace Wanxiangshu.Foundation

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
    val isInternal: Role -> bool
