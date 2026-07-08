module Wanxiangshu.Tests.Wanxiangzhen.PluginTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Wanxiangzhen.SquadTask
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.PromiseQueue
open Wanxiangshu.Shell.Wanxiangzhen.CoordinatorRuntime
open Wanxiangshu.Opencode.PluginWanxiangzhenHooks
open Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

let entries () : (string * (unit -> unit)) list = [
    ("mutateOutputParts with null parts creates array", fun () ->
        let output = createObj []
        let part = box "hello"
        mutateOutputParts output part
        let parts = get output "parts"
        checkBare (not (isNullish parts))
        let arr = unbox<obj array> parts
        equal 1 arr.Length
        equal "hello" (unbox<string> arr.[0]))

    ("mutateOutputParts with existing parts list replaces content", fun () ->
        let list = System.Collections.Generic.List<obj>()
        list.Add(box "old")
        let output = createObj [ "parts", box list ]
        mutateOutputParts output (box "new")
        equal 1 list.Count
        equal "new" (unbox<string> list.[0]))
]
