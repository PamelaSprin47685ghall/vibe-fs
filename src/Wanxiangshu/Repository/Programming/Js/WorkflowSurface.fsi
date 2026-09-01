namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// Opaque workflow outcome. The semantic observation functions expose the
/// commit report and failure algebra without leaking JsToolOutcome's union.
[<RequireQualifiedAccess>]
module JsWorkflowSurface =

    val run:
        workspaceRoot: string ->
        role: string ->
        language: string ->
        program: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        store: obj ->
            Task<obj>

    val runObserved:
        workspaceRoot: string ->
        role: string ->
        language: string ->
        program: string ->
        deadlineMs: int ->
        deadlineEpochMs: int64 ->
        outputBoundBytes: int ->
        store: obj ->
        fileAccessObservation: obj ->
            Task<obj>

    val caseName: value: obj -> string
    val rewritten: value: obj -> string array
    val created: value: obj -> string array
    val failureCode: value: obj -> obj
    val failureReason: value: obj -> obj
    val render: value: obj -> string
