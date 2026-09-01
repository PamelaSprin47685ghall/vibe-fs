namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode

/// Bookkeeper provider verb: one JavaScript program atomically reshapes the
/// staged question/answer Case. The sandbox has no filesystem capability.
module JsBookkeeperTool =
    val admission: ToolAdmission
    val spec: factory: HostToolFactory -> ToolSpec
    val execute: args: HostToolArguments -> context: HostToolContext -> Task<string>
