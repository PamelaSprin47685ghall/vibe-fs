namespace Wanxiangshu.Repository.Programming.Js

module JsGlobFs =
    type JsGlobListing = { Paths: string list }

    val matchesPathPattern: pattern: string -> path: string -> Result<bool, JsFailure>
    val glob: root: string -> pattern: string -> System.Threading.Tasks.Task<Result<JsGlobListing, JsFailure>>
