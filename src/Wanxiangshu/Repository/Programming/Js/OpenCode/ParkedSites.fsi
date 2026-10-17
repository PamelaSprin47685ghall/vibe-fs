namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Repository.Programming.Js

/// Module-level parked-site table (PLAN S2 step 3, algorithms C/D/E/F/G).
/// The table is a process-live object: it resets on restart, never touches
/// durable storage, and consumes each handle exactly once with no tombstone.
/// Compiled right after Process/JsSandbox (canonical order) so ToolRegistry,
/// PairHint and PluginSessionScope can call `parkedHandles` / `reapSession`
/// through the OpenCode namespace without defining the table themselves
/// (repository-programming-031).
module JsParkedSites =
    /// A parked site: the in-flight sandbox run handle (whose closure chain
    /// holds the vm context and whose DeadlineRef is resettable), the read
    /// snapshots for write-before checks, the session owner, and the Parked
    /// state.
    type JsParkedSite =
        { Handle: string
          RunHandle: Wanxiangshu.Process.JsSandbox.JsRunHandle
          ReadSnapshots: ResizeArray<JsReadSnapshot>
          SessionId: string }

    /// Concurrency ceiling for parked sites (PLAN S2 step 3: default 8).
    val concurrencyLimit: int

    /// Allocate a monotonically increasing process-unique handle (algorithm C2).
    val allocateHandle: unit -> string

    val register:
        site: JsParkedSite -> Result<string, JsFailure>

    val tryFind: handle: string -> JsParkedSite option
    val remove: handle: string -> unit
    val parkedHandles: unit -> string list
    val handlesForSession: sessionId: string -> string list
    val reapSession: sessionId: string -> Task
    val clearAll: unit -> unit
