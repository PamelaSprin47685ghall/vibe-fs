namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

module private NodeFsTestSupport =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, opts: obj) : unit = jsNative

    [<Import("rmSync", "node:fs")>]
    let rmSync (path: string, opts: obj) : unit = jsNative

    [<Import("tmpdir", "node:os")>]
    let tmpdir () : string = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

module JournalTestSupport =

    let withTempDir (action: string -> Task<unit>) : Task<unit> =
        task {
            let dir =
                NodeFsTestSupport.pathJoin (
                    NodeFsTestSupport.tmpdir (),
                    "wanxiangshu_test_" + Guid.NewGuid().ToString("N")
                )

            try
                NodeFsTestSupport.mkdirSync (dir, {| recursive = true |}) |> ignore
                do! action dir
            finally
                try
                    if NodeFsTestSupport.existsSync dir then
                        NodeFsTestSupport.rmSync (dir, {| recursive = true; force = true |}) |> ignore
                with _ ->
                    ()
        }
