namespace Wanxiangshu.Repository.Programming.Js

module JsGlobFs =
    type JsGlobListing = { Paths: string list }

    type JsVisibleEntry = { Name: string; IsDir: bool }

    val matchesPathPattern: pattern: string -> path: string -> Result<bool, JsFailure>
    val glob: root: string -> pattern: string -> System.Threading.Tasks.Task<Result<JsGlobListing, JsFailure>>

    /// JS-007 / [057]: one directory listed with the library's gitignore
    /// semantics (per-directory .gitignore, negation, root .git/info/exclude).
    val visibleEntries: root: string -> path: string -> Result<JsVisibleEntry list, JsFailure>
