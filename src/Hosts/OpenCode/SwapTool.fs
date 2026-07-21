module Wanxiangshu.Hosts.Opencode.SwapTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FileSwap
open Wanxiangshu.Runtime.FileSwap
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.FileSys
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Runtime

let swapTool () : obj =
    define
        "Exchange two non-overlapping line ranges between text files, or within the same text file. Use this as a structure-preserving refactoring primitive: move complete semantic blocks—functions, types, tests, or documentation sections—for module extraction, reordering, or cross-file migration without rewriting their contents. Line numbers are 1-based; begin is inclusive and endExclusive is exclusive."
        (createObj
            [ "path0",
              box
                  {| ``type`` = "string"
                     description = "First file path" |}
              "begin0",
              box
                  {| ``type`` = "integer"
                     minimum = 1
                     description = "Start line in first file, 1-based, inclusive" |}
              "endExclusive0",
              box
                  {| ``type`` = "integer"
                     description = "End line in first file, 1-based, exclusive" |}
              "path1",
              box
                  {| ``type`` = "string"
                     description = "Second file path" |}
              "begin1",
              box
                  {| ``type`` = "integer"
                     minimum = 1
                     description = "Start line in second file, 1-based, inclusive" |}
              "endExclusive1",
              box
                  {| ``type`` = "integer"
                     description = "End line in second file, 1-based, exclusive" |}
              "follow-tdd-and-kolmogorov-principles", box warnTddParam ])
        (fun args context ->
            promise {
                let runtime = fromOpencode context ""
                let cwd = runtime.Execution.Directory
                let path0 = resolve cwd (Wanxiangshu.Runtime.Dyn.str args "path0")
                let begin0 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "begin0")
                let endExclusive0 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "endExclusive0")
                let path1 = resolve cwd (Wanxiangshu.Runtime.Dyn.str args "path1")
                let begin1 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "begin1")
                let endExclusive1 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "endExclusive1")

                let request: SwapRequest =
                    { Path0 = path0
                      Range0 =
                        { Begin = begin0
                          EndExclusive = endExclusive0 }
                      Path1 = path1
                      Range1 =
                        { Begin = begin1
                          EndExclusive = endExclusive1 } }

                let io = NodeFileSwapIO()
                let! result = swap io request

                match result with
                | Ok msg -> return msg
                | Error e -> return $"swap failed: {e}"
            })
