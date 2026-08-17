namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// JS runtime owner boundary for bindings and sandbox execution. The mutable
/// staging buffer and sandbox failure union remain opaque; observations cross
/// as plain result objects.
[<RequireQualifiedAccess>]
module JsRuntimeSurface =

    type private JsBindingsHandle(api: obj, staging: ResizeArray<JsStagedMutation>) =
        member _.Api = api
        member _.Staging = staging

    let createApi (root: string) : obj =
        let staging = ResizeArray<JsStagedMutation>()
        let api = JsToolsBindings.createApi root staging
        box (JsBindingsHandle(api, staging))

    let api (handle: obj) : obj =
        (unbox<JsBindingsHandle> handle).Api

    let stagedCount (handle: obj) : int =
        (unbox<JsBindingsHandle> handle).Staging.Count

    let stagedKinds (handle: obj) : string array =
        (unbox<JsBindingsHandle> handle).Staging
        |> Seq.map (function
            | JsStagedMutation.Rewrite _ -> "Rewrite"
            | JsStagedMutation.Create _ -> "Create")
        |> Seq.toArray

    let private failureResult failure =
        box {| ok = false; code = JsFailure.code failure; reason = JsFailure.reason failure |}

    /// Run one model program with a supplied JS-native API object.
    let run
        (baseClassSource: string)
        (modelSource: string)
        (apiValue: obj)
        (deadlineMs: int)
        (deadlineEpochMs: int64)
        (outputBoundBytes: int)
        : Task<obj> =
        task {
            let! result =
                Wanxiangshu.Process.JsSandbox.runSurface
                    baseClassSource
                    modelSource
                    apiValue
                    deadlineMs
                    deadlineEpochMs
                    outputBoundBytes

            return
                match result with
                | Ok value -> box {| ok = true; value = value |}
                | Error failure -> failureResult failure
        }