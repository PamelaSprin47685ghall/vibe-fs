module Wanxiangshu.Runtime.FileSwapIO

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.FileSwap
open System.IO

[<Import("promises", "node:fs")>]
let private fsPromises: obj = jsNative

[<Emit("$0[$1]($2)")>]
let private callMethod1 (o: obj) (name: string) (arg: 'A) : 'T = jsNative

[<Emit("$0[$1]($2, $3)")>]
let private callMethod2 (o: obj) (name: string) (arg1: 'A) (arg2: 'B) : 'T = jsNative

/// File system operations needed for swap, with testability in mind.
type IFileSwapIO =
    abstract ReadText: path: string -> JS.Promise<string>
    abstract WriteTemp: path: string * content: string -> JS.Promise<string>
    abstract Replace: temp: string * target: string -> JS.Promise<unit>
    abstract Restore: target: string * original: string -> JS.Promise<unit>
    abstract DeleteIfExists: path: string -> JS.Promise<unit>

/// Production file-io implementation using Node.js fs promises.
type NodeFileSwapIO() =
    interface IFileSwapIO with
        member _.ReadText(path: string) : JS.Promise<string> =
            callMethod2 fsPromises "readFile" path "utf-8"

        member _.WriteTemp(path: string, content: string) : JS.Promise<string> =
            promise {
                let dir = System.IO.Path.GetDirectoryName(path)
                let temp = System.IO.Path.GetTempFileName()

                let tempPath =
                    if not (isNull dir) && dir <> "" then
                        System.IO.Path.Combine(dir, System.IO.Path.GetFileName(temp))
                    else
                        temp

                do! callMethod2 fsPromises "writeFile" tempPath content |> Promise.map ignore
                return tempPath
            }

        member _.Replace(temp: string, target: string) : JS.Promise<unit> =
            promise {
                do! callMethod2 fsPromises "rename" temp target |> Promise.map ignore
                return ()
            }

        member _.Restore(target: string, original: string) : JS.Promise<unit> =
            promise {
                do! callMethod2 fsPromises "writeFile" target original |> Promise.map ignore
                return ()
            }

        member _.DeleteIfExists(path: string) : JS.Promise<unit> =
            promise {
                try
                    do! callMethod1 fsPromises "unlink" path |> Promise.map ignore
                with _ ->
                    ()
            }
