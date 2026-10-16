namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop

/// Strict UTF-8 read, bridged to `wanxiangli/fs` (CUT-24). ENOENT →
/// FILE_NOT_FOUND, fatal decode or NUL bytes → INVALID_UTF8; the
/// algorithm lives in the library, failures map back through
/// JsFailure.ofWire.
module JsUtf8Fs =

    [<Import("Filesystem", "wanxiangli/fs")>]
    let private Filesystem: obj = jsNative

    [<Emit("$0({ root: '' })")>]
    let private createFilesystem (factory: obj) : obj = jsNative

    let private api = createFilesystem Filesystem

    let private ofResult (result: obj) : Result<string, JsFailure> =
        if unbox<bool> (result?ok) then
            Ok(string (result?value))
        else
            Error(JsFailure.ofWire (string (result?code)) (string (result?reason)))

    let readUtf8 (path: string) : Result<string, JsFailure> = ofResult (api?readUtf8 (path))

    let readUtf8Classified (path: string) : Result<string, JsFailure> =
        ofResult (api?readUtf8Classified (path))
