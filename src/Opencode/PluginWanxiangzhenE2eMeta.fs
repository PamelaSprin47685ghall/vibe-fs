module Wanxiangshu.Opencode.PluginWanxiangzhenE2eMeta

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Global>]
let private JSON: obj = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (data: string) : unit = jsNative

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

let private envVar (key: string) : string =
    let e = nodeProcess?("env")
    if isNullish e then "" else str e key

let writeE2eMetaIfEnabled (rt: CoordinatorRuntime) : unit =
    let isE2e =
        rt.IsE2e
        || envVar "WANXIANGZHEN_E2E" = "1"
        || envVar "WANXIANGZHEN_E2E_INPROCESS" = "1"

    let fullPath = pathJoin rt.ProjectRoot ".wanxiangzhen-e2e-meta.json"

    if isE2e then
        let meta =
            {| coordinatorUrl = rt.CoordinatorUrl
               token = rt.Token
               masterSessionId = rt.MasterSessionId
               sessionId = rt.Dag.SessionId |}

        writeFileSync fullPath (string (JSON?stringify(meta)))
