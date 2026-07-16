module Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenDeps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorBootstrap
open Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorDepsFactory
open Wanxiangshu.Runtime.Wanxiangzhen.ConfigReader

open Wanxiangshu.Hosts.Opencode.PluginWanxiangzhenHooks

let realCoordinatorDeps (workspaceRoot: string) =
    Wanxiangshu.Runtime.Wanxiangzhen.CoordinatorDepsFactory.realCoordinatorDeps workspaceRoot

type PluginWithDepsResult =
    abstract hooks: obj with get
    abstract runtime: CoordinatorRuntime with get

let pluginWithDeps (ctx: obj) (deps: CoordinatorDeps) : JS.Promise<PluginWithDepsResult> =
    promise {
        let client = get ctx "client"
        let directory = str ctx "directory"
        let config = readConfig directory
        let mb, gitError = resolveMasterBranch directory config deps
        let! rt = createWithDeps client directory config mb gitError deps
        let hooks = assembleCoordinatorHooks rt
        return createObj [ "hooks", box hooks; "runtime", box rt ] |> unbox
    }
