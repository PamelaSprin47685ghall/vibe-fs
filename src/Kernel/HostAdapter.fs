module Wanxiangshu.Kernel.HostAdapter

open Fable.Core
open Wanxiangshu.Kernel.Domain

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
    | Failure of DomainError
    | Aborted

/// Host adapter abstracts per-host child-session spawning and session lookup.
/// Implementations live in Shell/Opencode, Shell/Mux, Shell/Omp.
type IHostAdapter =
    abstract WorkspaceRoot: string
    abstract SessionId: string
    abstract SpawnSubagent: request: SubagentRequest -> JS.Promise<SubagentResponse>
    abstract ContinueSubagent: childID: string * agent: string * prompt: string -> JS.Promise<SubagentResponse>
    abstract RegisterTempFiles: prompt: string * files: string list -> unit
    abstract TryGetTempFiles: prompt: string -> string list option
