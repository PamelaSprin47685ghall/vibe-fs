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

let private e2eMetaDirectory (root: string) : string =
    match envVar "WANXIANGZHEN_E2E_META_DIR" with
    | "" -> root
    | dir -> dir

let private e2eMetaPath (root: string) : string =
    pathJoin (e2eMetaDirectory root) ".wanxiangzhen-e2e-meta.json"

let writeE2eMetaIfEnabled (rt: CoordinatorRuntime) : unit =
    let isE2e =
        rt.IsE2e
        || envVar "WANXIANGZHEN_E2E" = "1"
        || envVar "WANXIANGZHEN_E2E_INPROCESS" = "1"

    if isE2e then
        let dir = e2eMetaDirectory rt.ProjectRoot
        let fullPath = pathJoin dir ".wanxiangzhen-e2e-meta.json"

        let meta =
            {| coordinatorUrl = rt.CoordinatorUrl
               token = rt.Token
               masterSessionId = rt.MasterSessionId
               sessionId = rt.Dag.SessionId |}

        writeFileSync fullPath (string (JSON?stringify(meta)))

let createE2eMetaTempDirectory () : string =
    mkdtempSync (pathJoin (tmpdir ()) "wanxiangzhen-e2e-")
