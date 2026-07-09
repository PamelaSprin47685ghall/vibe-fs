module Wanxiangshu.Tests.ArchitectureTestsTreeSitter

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport
open Wanxiangshu.Shell.TreeSitterShell
open Wanxiangshu.Kernel.TreeSitterKernel

let private isError (d: SyntaxDiagnostic) = d.severity = "error"

let private checkSourceFile (path: string) =
    promise {
        let content = requireFile path
        let! result = checkSyntax content path

        match result with
        | Failed _ -> check ($"arch: tree-sitter style check failed for " + path) false
        | Ok(_, diags) ->
            let errors = diags |> Array.filter isError

            for d in errors do
                check ($"arch: tree-sitter error in " + path + ": " + d.message) false
    }

let treeSitterStyleChecks () =
    promise {
        let dirs =
            [ "src/Kernel"
              "src/Shell"
              "src/Mux"
              "src/Opencode"
              "src/Omp"
              "src/Methodology" ]

        let files = dirs |> List.collect (fun d -> fsFilesRecursive d)

        for path in files do
            if path.EndsWith(".fs") then
                do! checkSourceFile path
    }
