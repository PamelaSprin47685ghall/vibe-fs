module Wanxiangshu.Runtime.HostAdapter

open Fable.Core
open Wanxiangshu.Kernel.HostAdapter

/// Host port for child-session spawn and temp-file registry.
/// Implementations live under Hosts/* and Runtime host bridges.
type IHostAdapter =
    abstract WorkspaceRoot: string
    abstract SessionId: string
    abstract SpawnSubagent: request: SubagentRequest -> JS.Promise<SubagentResponse>
    abstract ContinueSubagent: childID: string * agent: string * prompt: string -> JS.Promise<SubagentResponse>
    abstract RegisterTempFiles: prompt: string * files: string list -> unit
    abstract TryGetTempFiles: prompt: string -> string list option
