namespace Wanxiangshu.Repository.Programming.Js

open Fable.Core
open Fable.Core.JsInterop

/// JS-005: the strict UTF-8 read adapter behind the js-* runtime bindings —
/// ENOENT → FILE_NOT_FOUND, fatal decode → INVALID_UTF8 (never silent
/// replacement chars). Durable facts never live here; EventStore owns them
/// (JS-012).
module JsUtf8Fs =

    [<Import("readFileSync", "node:fs")>]
    let private readFileSync (path: string) (options: obj) : obj = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileBuffer (path: string) : obj = jsNative

    [<Emit("new TextDecoder('utf-8', { fatal: true }).decode($0)")>]
    let private decodeUtf8 (buffer: obj) : string = jsNative

    let private classifyReadFailure path (ex: obj) =
        let code = string (ex?code)

        if code = "ENOENT" then
            JsFailure.FileNotFound path
        else
            JsFailure.FileReadFailed path

    let private decodeClassified path buffer : Result<string, JsFailure> =
        try
            Ok(decodeUtf8 buffer)
        with _ ->
            Error(JsFailure.InvalidUtf8 path)

    /// JS-005: strict UTF-8 read. ENOENT → FILE_NOT_FOUND; fatal decode error
    /// → INVALID_UTF8 (never silent replacement chars).
    let readUtf8 (path: string) : Result<string, JsFailure> =
        try
            let buffer = readFileBuffer path
            Ok(decodeUtf8 buffer)
        with ex ->
            Error(classifyReadFailure path (box ex))

    /// JS-005: read with invalid-UTF-8 classification; used by file() so the
    /// code is distinct from generic read failures.
    let readUtf8Classified (path: string) : Result<string, JsFailure> =
        try
            let buffer = readFileBuffer path
            decodeClassified path buffer
        with ex ->
            Error(classifyReadFailure path (box ex))
