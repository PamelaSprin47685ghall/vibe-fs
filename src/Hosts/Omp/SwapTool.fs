module Wanxiangshu.Hosts.Omp.SwapTool

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FileSwap
open Wanxiangshu.Runtime.FileSwap
open Wanxiangshu.Runtime
open Wanxiangshu.Hosts.Omp.Schema
open Wanxiangshu.Hosts.Omp.Codec

let private executeSwap (_id: string) (params': obj) (_s: obj) (_u: obj) (ctx: obj) : JS.Promise<ToolResult> =
    promise {
        let cwd = Dyn.str ctx "cwd"

        let resolvePath (p: string) =
            if p.StartsWith "/" then p
            elif cwd = "" then p
            elif cwd.EndsWith "/" then cwd + p
            else cwd + "/" + p

        let path0 = resolvePath (Dyn.str params' "path0")
        let begin0 = unbox<int> (Dyn.get params' "begin0")
        let endExclusive0 = unbox<int> (Dyn.get params' "endExclusive0")
        let path1 = resolvePath (Dyn.str params' "path1")
        let begin1 = unbox<int> (Dyn.get params' "begin1")
        let endExclusive1 = unbox<int> (Dyn.get params' "endExclusive1")

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
        | Ok msg -> return textResult msg
        | Error e -> return errorResult e
    }

let registerSwapTool (pi: obj) : unit =
    let tb = Dyn.get pi "typebox"

    pi?registerTool (
        createObj
            [ "name", box "swap"
              "label", box "Swap"
              "description",
              box
                  "Exchange two non-overlapping line ranges between text files, or within the same text file. Line numbers are 1-based; begin is inclusive and endExclusive is exclusive."
              "parameters",
              objectOf
                  [| ("path0", str "First file path" tb)
                     ("begin0",
                      tb?Type?Number (
                          createObj
                              [ "description", box "Start line in first file, 1-based, inclusive"
                                "minimum", box 1 ]
                      ))
                     ("endExclusive0", num "End line in first file, 1-based, exclusive" tb)
                     ("path1", str "Second file path" tb)
                     ("begin1",
                      tb?Type?Number (
                          createObj
                              [ "description", box "Start line in second file, 1-based, inclusive"
                                "minimum", box 1 ]
                      ))
                     ("endExclusive1", num "End line in second file, 1-based, exclusive" tb) |]
                  tb
              "execute", box (fun id p s u ctx -> executeSwap id p s u ctx) ]
    )
