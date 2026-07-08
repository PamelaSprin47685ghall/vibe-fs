module Wanxiangshu.Opencode.PluginWanxiangzhen

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.SlaveRuntime

[<Global("process")>]
let private nodeProcess: obj = jsNative

let private envVar (key: string) : string =
    let e = nodeProcess?("env")
    if isNullish e then "" else str e key

let private twoArgHook (f: obj -> obj -> JS.Promise<unit>) =
    box (System.Func<obj, obj, JS.Promise<unit>>(f))

let private coordinatorPlugin (ctx: obj) : JS.Promise<obj> =
    promise {
        let! result = PluginWanxiangzhenDeps.pluginWithDeps ctx (PluginWanxiangzhenDeps.realCoordinatorDeps ())
        return result.hooks
    }

let private slavePlugin (_: obj) : JS.Promise<obj> =
    promise {
        match readSlaveConfig () with
        | None -> return createObj []
        | Some cfg ->
            do! registerPid cfg
            let result = createObj []
            setKey result "id" (box "wanxiangzhen-slave")
            setKey result "name" (box "wanxiangzhen-slave")
            setKey result "tool" (slaveToolDefs cfg)

            setKey
                result
                "dispose"
                (box (fun () ->
                    doneBeacon cfg |> Promise.start |> ignore
                    Promise.lift ()))

            return result
    }

let plugin (ctx: obj) : JS.Promise<obj> =
    if envVar "SQUAD_COORDINATOR_URL" <> "" then
        slavePlugin ctx
    else
        coordinatorPlugin ctx

[<ExportDefault>]
let pluginModule: obj = createObj [ "id", box "wanxiangzhen"; "server", box plugin ]
