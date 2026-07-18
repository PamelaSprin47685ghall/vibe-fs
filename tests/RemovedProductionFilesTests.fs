module Wanxiangshu.Tests.RemovedProductionFilesTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())
    let srcRoot = pathJoin cwd "src"

    let removedFiles =
        [ "Hosts/OpenCode/SubagentIo.fs"
          "Hosts/OpenCode/PtyIo.fs"
          "Hosts/Mux/MessageTransformCompaction.fs"
          "Hosts/Mux/BacklogSession.fs"
          "Hosts/OpenCode/BacklogSession.fs"
          "Hosts/Omp/NudgeDispatchLogic/ClaimHelper.fs"
          "Hosts/Omp/NudgeDispatchLogic/LeaseHelper.fs"
          "Hosts/Omp/NudgeDispatchLogic/SnapshotHelper.fs" ]

    for relativePath in removedFiles do
        let mutable path = srcRoot

        for segment in relativePath.Split('/') do
            path <- pathJoin path segment

        check (sprintf "legacy production file is removed: %s" relativePath) (not (existsSync path))
