module Wanxiangshu.Opencode.PluginWanxiangzhenDeps

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorBootstrap
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorDepsFactory
open Wanxiangshu.Shell.Wanxiangzhen.ConfigReader

open Wanxiangshu.Opencode.PluginWanxiangzhenHooks

let realCoordinatorDeps (workspaceRoot: string) =
    Wanxiangshu.Shell.Wanxiangzhen.CoordinatorDepsFactory.realCoordinatorDeps workspaceRoot

type PluginWithDepsResult =
    abstract hooks: obj with get
    abstract runtime: CoordinatorRuntime with get

let pluginWithDeps
    (ctx: obj)
    (deps: CoordinatorDeps)
    : JS.Promise<PluginWithDepsResult>
    =
    promise {
        let client = get ctx "client"
        let directory = str ctx "directory"
        let config = readConfig directory
        let mb, gitError = resolveMasterBranch directory config deps
        let! rt = createWithDeps client directory config mb gitError deps
        let hooks = assembleCoordinatorHooks rt
        return createObj [ "hooks", box hooks; "runtime", box rt ] |> unbox
    }