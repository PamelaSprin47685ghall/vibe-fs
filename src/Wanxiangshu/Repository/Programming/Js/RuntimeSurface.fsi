namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// JS runtime owner boundary for bindings and sandbox execution. The mutable
/// staging buffer and sandbox failure union remain opaque; observations cross
/// as plain result objects.
[<RequireQualifiedAccess>]
module JsRuntimeSurface =

    val createApi: root: string -> obj
    val api: handle: obj -> obj
    val stagedCount: handle: obj -> int
    val stagedKinds: handle: obj -> string array
    val readPaths: handle: obj -> string array

    val run:
        baseClassSource: string ->
        modelSource: string ->
        apiValue: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
            Task<obj>
