namespace Wanxiangshu.Resources

[<RequireQualifiedAccess>]
module PromptSurface =
    val load: unit -> obj
    val loadForLanguage: language: string -> obj
    val allForLanguage: language: string -> string array
    val loadBookkeeperSystem: unit -> string
    val loadBookkeeperSystemFor: language: string -> string
    val runtimeLoad: unit -> obj
    val runtimeLoadForLanguage: language: string -> obj
    val runtimeInstallFromPackage: unit -> unit
    val runtimeCurrent: unit -> obj
