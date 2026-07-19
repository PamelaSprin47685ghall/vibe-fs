module Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenE2eMeta

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

[<Global>]
let private JSON: obj = jsNative

[<Import("writeFileSync", "node:fs")>]
let private writeFileSync (path: string) (data: string) : unit = jsNative

[<Import("mkdtempSync", "node:fs")>]
let private mkdtempSync (prefix: string) : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (path: string) (seg: string) : string = jsNative

[<Import("tmpdir", "node:os")>]
let private tmpdir () : string = jsNative

let private envVar (key: string) : string =
    let e = nodeProcess?("env")
    if isNullish e then "" else str e key

let private e2eMetaDirectory () : string =
    match envVar "WANXIANGZHEN_E2E_META_DIR" with
    | "" -> mkdtempSync (pathJoin (tmpdir ()) "wanxiangzhen-e2e-")
    | dir -> dir

let private writeE2eMeta (rt: CoordinatorRuntime) (dir: string) : unit =
    let fullPath = pathJoin dir ".wanxiangzhen-e2e-meta.json"

    let meta =
        {| coordinatorUrl = rt.CoordinatorUrl
           token = rt.Token
           masterSessionId = rt.MasterSessionId
           sessionId = rt.Dag.SessionId |}

    writeFileSync fullPath (string (JSON?stringify(meta)))

let writeE2eMetaIfEnabled (rt: CoordinatorRuntime) : unit =
    let isE2e =
        rt.IsE2e
        || envVar "WANXIANGZHEN_E2E" = "1"
        || envVar "WANXIANGZHEN_E2E_INPROCESS" = "1"

    if isE2e then
        writeE2eMeta rt (e2eMetaDirectory ())

let createE2eMetaTempDirectory () : string =
    mkdtempSync (pathJoin (tmpdir ()) "wanxiangzhen-e2e-")
