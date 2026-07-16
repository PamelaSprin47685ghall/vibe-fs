module Wanxiangshu.Kernel.HostAdapter

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

/// Subagent role discriminant. One case per logical agent type.
type SubagentRole =
    | Coder
    | Investigator
    | Meditator
    | Browser

/// Canonical request shape for spawning a subagent. Host adapters translate
/// this into their native child-session protocol.
type SubagentRequest =
    { Role: SubagentRole
      Title: string
      Prompt: string
      AllowedTools: string array }

/// Outcome of a subagent run. Aborted covers both ClientCancellation and
/// MessageAborted so the caller never needs to match those DU cases.
type SubagentResponse =
    | Success of string
    | Spawned of childID: string * report: string
    | Failure of DomainError
    | Aborted
