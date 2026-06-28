module Wanxiangshu.Shell.FallbackMessageCodec

open Wanxiangshu.Shell.Dyn

/// Scan messages backwards for the most recent task/todowrite tool part and
/// report whether every todo item is in a terminal status.
let allTodosCompleted (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then false
    else
        msgs
        |> Array.rev
        |> Array.tryPick (fun msg ->
            let parts = Dyn.get msg "parts"
            if not (Dyn.isArray parts) then None
            else
                (parts :?> obj array)
                |> Array.rev
                |> Array.tryPick (fun part ->
                    let partType = Dyn.str part "type"
                    let tool = Dyn.str part "tool"
                    if (partType = "tool" || partType = "dynamic-tool")
                       && (tool = "task" || tool = "todowrite") then
                        let input = Dyn.get (Dyn.get part "state") "input"
                        let todos = Dyn.get input "todos"
                        if Dyn.isArray todos then
                            (todos :?> obj array)
                            |> Array.forall (fun todo ->
                                let s = Dyn.str todo "status"
                                s = "completed" || s = "cancelled")
                            |> Some
                        else None
                    else None))
        |> Option.defaultValue false
