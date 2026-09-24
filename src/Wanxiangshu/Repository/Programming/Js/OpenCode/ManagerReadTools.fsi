namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Wanxiangshu.OpenCode

/// Manager review read-only tools: read-manager, glob-manager, grep-manager.
/// These tools run fixed, read-only JavaScript programs in the JsProgram sandbox
/// without mutation capabilities, executing with capabilities = { Read, Glob, Grep }.
module ManagerReadTools =
    val readManagerSpec:
        factory: HostToolFactory ->
        scope: ToolRuntimeScope ->
        groundingObservation: (HostToolContext -> string list -> string list -> Task<unit>) ->
            ToolSpec

    val globManagerSpec:
        factory: HostToolFactory ->
        scope: ToolRuntimeScope ->
        groundingObservation: (HostToolContext -> string list -> string list -> Task<unit>) ->
            ToolSpec

    val grepManagerSpec:
        factory: HostToolFactory ->
        scope: ToolRuntimeScope ->
        groundingObservation: (HostToolContext -> string list -> string list -> Task<unit>) ->
            ToolSpec
