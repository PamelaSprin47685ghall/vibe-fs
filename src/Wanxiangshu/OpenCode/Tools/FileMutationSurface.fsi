namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Registered mv/rm owner boundary. The Host factory and ToolSpec records stay
/// opaque while names, argument vocabulary and bounded execution cross as JS
/// values.
[<RequireQualifiedAccess>]
module FileMutationSurface =

    val createMv: toolModule: obj -> obj
    val createRm: toolModule: obj -> obj
    val name: handle: obj -> string
    val argumentNames: handle: obj -> string array
    val description: handle: obj -> string
    /// Success carries structured data fields (`removed`, `moved`).
    ///
    /// A refusal carries the localized prose as its instruction plus a stable
    /// `code` field naming the reason (e.g. `tool/rm/directory-not-empty`).
    /// That code is the contract: consumers classify failures by it rather than
    /// by matching prose, which changes with the provider language.
    val execute: handle: obj -> args: obj -> context: obj -> Task<obj>
