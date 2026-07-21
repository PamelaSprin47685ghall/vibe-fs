module Wanxiangshu.Hosts.Mux.SwapTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FileSwap
open Wanxiangshu.Runtime.FileSwap
open Wanxiangshu.Runtime
open Wanxiangshu.Hosts.Mux.Wrappers

/// Mux swap tool definition.
let swapToolDef () : ToolDefinition =
    { name = "swap"
      description =
        "Exchange two non-overlapping line ranges between text files, or within the same text file. "
        + "Use this as a structure-preserving refactoring primitive: move complete semantic blocks—functions, types, tests, or documentation sections—"
        + "for module extraction, reordering, or cross-file migration without rewriting their contents. "
        + "Line numbers are 1-based; begin is inclusive and endExclusive is exclusive."
      parameters =
        mkSchema
            (createObj
                [ "path0", box (strProp "First file path")
                  "begin0", box (numProp "Start line in first file, 1-based, inclusive")
                  "endExclusive0", box (numProp "End line in first file, 1-based, exclusive")
                  "path1", box (strProp "Second file path")
                  "begin1", box (numProp "Start line in second file, 1-based, inclusive")
                  "endExclusive1", box (numProp "End line in second file, 1-based, exclusive") ])
            [| "path0"; "begin0"; "endExclusive0"; "path1"; "begin1"; "endExclusive1" |]
      execute =
        fun config args ->
            promise {
                let workspaceRoot =
                    match strField config "root" with
                    | Some r -> r
                    | None ->
                        match strField config "workspaceId" with
                        | Some wid -> wid
                        | None -> ""

                let resolvePath (p: string) =
                    if p.StartsWith "/" then p
                    elif workspaceRoot = "" then p
                    elif workspaceRoot.EndsWith "/" then workspaceRoot + p
                    else workspaceRoot + "/" + p

                let path0 = resolvePath (Wanxiangshu.Runtime.Dyn.str args "path0")
                let begin0 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "begin0")
                let endExclusive0 = unbox<int> (Wanxiangshu.Runtime.Dyn.get args "endExclusive0")
                let path1 = resolvePath (Wanxiangshu.Runtime.Dyn.str args "path1")
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
            }
      condition = None }
