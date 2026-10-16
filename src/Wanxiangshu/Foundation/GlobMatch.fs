namespace Wanxiangshu.Foundation

open Fable.Core
open Fable.Core.JsInterop

/// Wildmatch / gitignore path matching kernel, bridged to the independent
/// `wanxiangli/fs` library (CUT-24). The matching algorithm lives in the
/// library; this module keeps the main-repo signature stable for existing
/// consumers (glob enumeration and requirement grounding).
module GlobMatch =

    type GlobPatternError = | InvalidPattern

    [<Import("Filesystem", "wanxiangli/fs")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: '' })")>]
    let private createFilesystem (factory: obj) : obj = jsNative

    let private api = createFilesystem Filesystem

    let private isOk (result: obj) = unbox<bool> (result?ok)

    let compilePattern (pattern: string) : Result<obj, GlobPatternError> =
        let result = api?compilePattern (pattern)

        if isOk result then
            Ok(result?value)
        else
            Error InvalidPattern

    let testCompiled (regex: obj) (text: string) : bool =
        unbox<bool> (api?testCompiled (regex, text))

    let matchesPathPattern (pattern: string) (path: string) : Result<bool, GlobPatternError> =
        let result = api?matchesPathPattern (pattern, path)

        if isOk result then
            Ok(unbox<bool> (result?value))
        else
            Error InvalidPattern
