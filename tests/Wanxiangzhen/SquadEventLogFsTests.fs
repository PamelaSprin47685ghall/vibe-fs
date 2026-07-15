module Wanxiangshu.Tests.Wanxiangzhen.SquadEventLogFsTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Shell.EventLogFiles
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.Wanxiangzhen.SquadEventLogRuntime
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

[<Import("mkdtempSync", "node:fs")>]
let private mkdtempSync (template: string) : string = jsNative

[<Import("rmSync", "node:fs")>]
let private rmSync (path: string) (opts: obj) : unit = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (data: string) : unit = jsNative

let private withTempDir (f: string -> JS.Promise<unit>) : JS.Promise<unit> =
    promise {
        let dir = mkdtempSync "wanxiangshu-el-"

        try
            do! f dir
        finally
            rmSync dir (createObj [ "recursive", box true; "force", box true ])
    }

let entriesAsync () : (string * (unit -> JS.Promise<unit>)) list =
    [ ("SquadEventLogFs.append and read round-trip",
       fun () ->
           withTempDir (fun dir ->
               promise {
                   let ev = SquadCreated("s1", "req")
                   let! w = appendSquadEvent dir "t1" ev

                   match w with
                   | Error e -> failwith e
                   | Ok() -> ()

                   let! events = readAllSquadEvents dir
                   equal 1 events.Length

                   match events.[0] with
                   | SquadCreated(sid, req) ->
                       equal "s1" sid
                       equal "req" req
                   | _ -> check "" false
               }))

      ("SquadEventLogFs.truncate on corrupt tail line",
       fun () ->
           withTempDir (fun dir ->
               promise {
                   let! _ =
                       appendSquadEvent
                           dir
                           "t"
                           (TasksCreated(
                               "s1",
                               [ { taskId = "a"
                                   title = "t"
                                   description = "d"
                                   dependsOn = [] } ]
                           ))

                   let path = Wanxiangshu.Shell.EventLogCodec.eventPath dir
                   writeFileSync path (readFileSync path "utf-8" + "\n{not-json\n")
                   let! events = readAllSquadEvents dir
                   equal 1 events.Length
               })) ]
