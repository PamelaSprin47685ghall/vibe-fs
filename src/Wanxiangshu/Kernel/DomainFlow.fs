namespace Wanxiangshu.Kernel

open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Domain error / context values used by direct CE programs (not a Flow AST).

[<RequireQualifiedAccess>]
type AgentError =
    | HostFailure of string
    | SessionDead of string
    | InvalidFork of string
    | ParentCancelled

[<RequireQualifiedAccess>]
type CompanionError =
    | ProjectionFailed of string
    | BloggerFailed of string

type AgentContext =
    { SessionId: string; AgentName: string }

type CompanionContext = { SessionId: string }
