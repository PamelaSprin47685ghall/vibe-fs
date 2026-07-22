module Wanxiangshu.Tests.ArchitectureGatesTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureGatesFs
open Wanxiangshu.Tests

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let run () : unit =
    let cwd = unbox<string> (nodeProcess?cwd ())
    let srcRoot = pathJoin cwd "src"
    let testsRoot = pathJoin cwd "tests"
    let integrationRoot = pathJoin cwd "integration"
    let e2eRoot = pathJoin cwd "e2e"

    let sizeViolations = ArchitectureGatesSize.run srcRoot
    let layerViolations = ArchitectureGatesLayer.run srcRoot

    let projectViolations =
        ArchitectureGatesProject.run cwd srcRoot testsRoot integrationRoot e2eRoot

    let promptViolations = PromptArchitectureGatesTests.run srcRoot 5

    let allViolations = ResizeArray<string>()
    allViolations.AddRange sizeViolations
    allViolations.AddRange layerViolations
    allViolations.AddRange projectViolations
    allViolations.AddRange promptViolations

    if allViolations.Count > 0 then
        let summary = String.concat "\n" (Seq.cast<string> allViolations)
        failwith (sprintf "architecture gate violations (%d):\n%s" allViolations.Count summary)
    else
        check "architecture gates passed" true
