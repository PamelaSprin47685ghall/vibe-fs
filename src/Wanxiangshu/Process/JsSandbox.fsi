namespace Wanxiangshu.Process

open System.Threading.Tasks
open Wanxiangshu.Repository.Programming.Js

module JsSandbox =
    val wrapProgram: baseClassSource: string -> modelSource: string -> deadlineEpochMs: int64 -> string
    val run:
        wrappedSource: string ->
        api: obj ->
        deadlineMs: int ->
        outputBoundBytes: int ->
        Task<Result<string, JsFailure>>
    val runSurface:
        baseClassSource: string ->
        modelSource: string ->
        api: obj ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        Task<Result<string, JsFailure>>
