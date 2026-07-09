module Wanxiangshu.Tests.Wanxiangzhen.MockE2eHttpTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorOps
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorLifecycle
open Wanxiangshu.Shell.Wanxiangzhen.HttpServer
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRoutes
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorSquadUpdate
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Tests.Wanxiangzhen.TestDoubles

[<Global>]
let private JSON: obj = jsNative

let testHttpTransportTokenAndRegister () : JS.Promise<unit> =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps
        rt.MasterSessionId <- "squad-session-001"

        let evts = [| mkTaskEvent "squad-a1b2" "Task A" "desc A" [] |]
        let args = mkSquadUpdateArgs evts
        rt.Scheduling <- true
        let! _ = handleSquadUpdate rt args
        rt.Scheduling <- false
        do! schedulerTick rt

        let! server = startServer rt.Token (routeHandler rt)

        try
            let! badResp =
                fetchJson
                    (server.Url + "/task/squad-a1b2/register")
                    (createObj
                        [ "method", box "POST"
                          "headers", box {| Authorization = box "Bearer wrong-token" |}
                          "body", box (JSON?stringify(createObj [ "pid", box 12345 ])) ])

            checkBare (badResp.status = 401)
            checkBare (str badResp.body "result" = "unauthorized")

            let! regResp =
                fetchJson
                    (server.Url + "/task/squad-a1b2/register")
                    (createObj
                        [ "method", box "POST"
                          "headers", box {| Authorization = box ("Bearer " + rt.Token) |}
                          "body", box (JSON?stringify(createObj [ "pid", box 12345 ])) ])

            checkBare (regResp.status = 200)
            checkBare (str regResp.body "result" = "registered")

            match findTask "squad-a1b2" rt.Dag with
            | None -> checkBare false
            | Some t -> checkBare (t.SlavePid = Some 12345)
        finally
            server.Close()
    }

let entriesAsync () : (string * (unit -> JS.Promise<unit>)) list =
    [ ("MockE2e.http_transport_token_register: bad-token 401 + correct-token register updates SlavePid",
       testHttpTransportTokenAndRegister) ]
