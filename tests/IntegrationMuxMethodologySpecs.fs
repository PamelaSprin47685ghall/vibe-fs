module Wanxiangshu.Tests.IntegrationMuxMethodologySpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Mux.Plugin
open Wanxiangshu.Shell.Dyn

let muxTodoWriteMethodologySchemaSpec () = promise {
    let reg = sharedMuxRegistration ()
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write") |> Option.defaultValue null
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper for methodology" false
    else
        let fakeHostTodo =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _ ->
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let schema = get wrapped "parameters"
        let props = get schema "properties"
        let methodologySchema = get props "select_methodology"
        check "todo_write select_methodology is array type" (str methodologySchema "type" = "array")
        let itemsSchema = get methodologySchema "items"
        check "todo_write select_methodology items is string type" (str itemsSchema "type" = "string")
        let enumArr = unbox<obj[]> (get itemsSchema "enum")
        check "todo_write select_methodology enum has all values" (enumArr.Length = (List.toArray methodologyEnumValues).Length)
        check "todo_write select_methodology minItems is 1" (unbox<int> (get methodologySchema "minItems") = 1)
}
