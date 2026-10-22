namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.OpenCode

/// Bookkeeper provider verb: one JavaScript program atomically reshapes the
/// staged question/answer Case. The sandbox has no filesystem capability.
module JsBookkeeperTool =
    val spec: factory: HostToolFactory -> ToolSpec

    /// One call. The session is the only fact the tool needs from its caller:
    /// the staged transaction is bound to it.
    val execute: args: HostToolArguments -> sessionId: SessionId -> Task<string>
