module Wanxiangshu.Tests.Wanxiangzhen.ExtendedMockE2eFixtures

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenHooks
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadConfig
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorOps
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorLifecycle
open Wanxiangshu.Runtime.Wanxiangzhen.HttpCodec
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.Wanxiangzhen.HttpServer
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRoutes
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorSquadUpdate
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat
open Wanxiangshu.Tests.Wanxiangzhen.TestDoubles
open Wanxiangshu.Tests.Wanxiangzhen.SpinWait

let mkRunningTaskServer () =
    promise {
        let s = mkFake ()
        let deps = mkDeps s
        let rt = mkRuntime deps
        rt.MasterSessionId <- "squad-session-001"

        let evts = [| mkTaskEvent "squad-a1b2" "A" "desc A" [] |]
        let args = mkSquadUpdateArgs evts
        rt.Scheduling <- true
        let! _ = handleSquadUpdate rt args
        rt.Scheduling <- false
        do! schedulerTick rt
        let! _ = (routeHandler rt) "POST" "/task/squad-a1b2/register" (createObj [ "pid", box 111 ])
        let! server = startServer rt.Token (routeHandler rt)
        return s, rt, server
    }

let waitUntil (predicate: unit -> bool) (_timeoutMs: int) : JS.Promise<unit> = spinUntilFail predicate 500
