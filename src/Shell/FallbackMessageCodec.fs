module Wanxiangshu.Shell.FallbackMessageCodec

open Wanxiangshu.Shell.Dyn

/// Scan assistant message parts for raw XML tool-call patterns the model
/// emitted as text instead of executing (<function=…> or <function …>).
let hasToolCallAsText (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then false
    else
        msgs
        |> Array.exists (fun msg ->
            let info = Dyn.get msg "info"
            let role = Dyn.str info "role"
            if role <> "assistant" then false
            else
                let parts = Dyn.get msg "parts"
                if not (Dyn.isArray parts) then false
                else
                    (parts :?> obj array)
                    |> Array.exists (fun part ->
                        Dyn.str part "type" = "text"
                        && (let t = Dyn.str part "text" in t.Contains("<function=") || t.Contains("<function "))))

let isNetworkErrorText (text: string) : bool =
    if System.String.IsNullOrWhiteSpace text then false
    elif text.Contains("\n") then false
    else
        let lower = text.ToLowerInvariant()
        lower.Contains("error") && lower.Contains("network")

let hasNetworkErrorText (msgs: obj array) : bool =
    if isNull msgs || msgs.Length = 0 then false
    else
        msgs
        |> Array.exists (fun msg ->
            let role = Dyn.str (Dyn.get msg "info") "role"
            if role <> "assistant" then false
            else
                let parts = Dyn.get msg "parts"
                if not (Dyn.isArray parts) then false
                else
                    (parts :?> obj array)
                    |> Array.exists (fun part ->
                        Dyn.str part "type" = "text"
                        && isNetworkErrorText (Dyn.str part "text")))

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
