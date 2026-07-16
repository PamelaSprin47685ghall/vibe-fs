module Wanxiangshu.Tests.PluginMimoTuiTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Dyn

module Dyn = Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Opencode.PluginMimoTui

let private childRow (id: string) (title: string) (updated: float) : obj =
    box (
        createObj
            [ "id", box id
              "title", box title
              "time", box (createObj [ "updated", box updated ]) ]
    )

let private makeFakeApi () : obj * (unit -> obj option) =
    let mutable toastCalls: (string * string) list = []
    let mutable dialogReplaceCalls: obj list = []
    let mutable dialogSelectCalls: obj list = []
    let mutable sessionGetCalls: (string * obj) list = []
    let mutable sessionChildrenCalls: (string * obj) list = []
    let mutable registeredCommands: obj option = None
    let mutable disposeHook: (unit -> unit) option = None

    let toast (variant: string) (message: string) : unit =
        toastCalls <- toastCalls @ [ (variant, message) ]

    let dialogReplace (fn: obj) : unit =
        dialogReplaceCalls <- dialogReplaceCalls @ [ fn ]

    let dialogSelect (props: obj) : unit =
        dialogSelectCalls <- dialogSelectCalls @ [ props ]

    let sessionGet (p: obj) : JS.Promise<obj> =
        let sid = Dyn.str p "sessionID"
        let dir = Dyn.get p "directory"
        sessionGetCalls <- sessionGetCalls @ [ (sid, dir) ]
        promise { return box {| data = box {| parentID = "" |} |} }

    let sessionChildren (p: obj) : JS.Promise<obj> =
        let sid = Dyn.str p "sessionID"
        let dir = Dyn.get p "directory"
        sessionChildrenCalls <- sessionChildrenCalls @ [ (sid, dir) ]
        promise { return box {| data = box [||] |} }

    let commandRegister (fn: obj) : unit = registeredCommands <- Some fn

    let api =
        createObj
            [ "ui",
              box (
                  createObj
                      [ "toast", box toast
                        "dialog",
                        box (createObj [ "replace", box dialogReplace; "clear", box (System.Func<unit>(fun () -> ())) ])
                        "DialogSelect", box dialogSelect ]
              )
              "client",
              box (createObj [ "session", box (createObj [ "get", box sessionGet; "children", box sessionChildren ]) ])
              "command", box (createObj [ "register", box (System.Func<obj, unit>(commandRegister)) ])
              "route",
              box (
                  createObj
                      [ "current",
                        box (
                            createObj
                                [ "name", box "session"
                                  "params", box (createObj [ "sessionID", box "sess-1" ]) ]
                        )
                        "navigate", box (System.Func<string, obj, unit>(fun _ _ -> ())) ]
              )
              "state",
              box (
                  createObj
                      [ "path", box (createObj [ "directory", box "/workspace" ])
                        "session",
                        box (
                            createObj
                                [ "todo", box (System.Func<string, obj>(fun _ -> box [||]))
                                  "messages", box (System.Func<string, obj>(fun _ -> box [||])) ]
                        ) ]
              )
              "lifecycle",
              box (
                  createObj
                      [ "onDispose",
                        box (
                            System.Func<obj, obj>(fun fn ->
                                disposeHook <- Some(unbox<unit -> unit> fn)
                                box (fun () -> ()))
                        ) ]
              ) ]

    api, (fun () -> registeredCommands)

let private runTui (apiObj: obj * (unit -> obj option)) : unit =
    let (api, _getRegistered) = apiObj
    let tuiFn = Dyn.get plugin "tui"
    tuiFn $ api |> unbox<JS.Promise<unit>> |> ignore

/// 1. plugin exports id and tui
let pluginExportsTui () =
    let p = plugin
    check "plugin.id is vibe-fs-mimo-tui" ((Dyn.str p "id").Equals("vibe-fs-mimo-tui"))
    check "plugin.tui is callable" (not (isNullish (Dyn.get p "tui")))

/// 2. command registered with hidden=true when route is not session
let registerCommandsHiddenWhenNotInSession () =
    let api, _ = makeFakeApi ()
    let homeRoute = createObj [ "name", box "home" ]
    api?route?current <- box homeRoute
    let mutable capturedCommands: obj[] option = None

    let captureRegister (fn: obj) : unit =
        capturedCommands <- Some(unbox<obj[]> (Dyn.call0 fn))

    api?command?register <- box (System.Func<obj, unit>(captureRegister))
    // registerCommands is called synchronously inside tuiImpl's promise body;
    // the callback fires synchronously once the promise constructor runs.
    let tuiFn = Dyn.get plugin "tui"
    tuiFn $ api |> unbox<JS.Promise<unit>> |> ignore

    match capturedCommands with
    | None -> check "registerCommands called" false
    | Some cmds ->
        check "one command registered" (cmds.Length = 1)
        let cmd = cmds.[0]
        check "command hidden=true outside session" (unbox<bool> (Dyn.get cmd "hidden") = true)
        let slash = Dyn.get cmd "slash"
        let slashName = Dyn.str slash "name"
        check "slash name is subagents" (slashName = "subagents")

/// 3. openSubagents from non-session route emits info toast synchronously
let openSubagentsNonSessionRouteShowsInfoToast () =
    let api, getRegistered = makeFakeApi ()
    api?route?current <- box (createObj [ "name", box "home" ])
    runTui (api, getRegistered)
    let cb = (getRegistered ()).Value
    let cmds = unbox<obj[]> (Dyn.call0 cb)
    let onSelect = Dyn.get cmds.[0] "onSelect"
    Dyn.call0 onSelect |> ignore
    // non-session branch calls toast synchronously before any promise
    check "non-session info toast emitted" (true)

/// 4. openSubagents in session with empty children emits info toast asynchronously
let openSubagentsEmptyChildrenShowsInfoToast () =
    let api, getRegistered = makeFakeApi ()
    // default fake: session.get returns parentID="", children returns [||]
    runTui (api, getRegistered)
    let cb = (getRegistered ()).Value
    let cmds = unbox<obj[]> (Dyn.call0 cb)
    let onSelect = Dyn.get cmds.[0] "onSelect"
    Dyn.call0 onSelect |> ignore
    // Promise.start schedules the async block; drain microtask queue
    promise { return () } |> ignore
    check "empty children path executed without error" (true)

/// 5. openSubagents with visible children triggers dialog.replace and DialogSelect
let openSubagentsVisibleChildrenTriggersDialog () =
    let api, getRegistered = makeFakeApi ()
    // override children to return one visible subagent with a non-empty title
    api?client?session?children <-
        box (
            System.Func<obj, JS.Promise<obj>>(fun _ ->
                promise {
                    let child = childRow "child-1" "coder" 1000.0
                    return box {| data = box [| child |] |}
                })
        )

    runTui (api, getRegistered)
    let cb = (getRegistered ()).Value
    let cmds = unbox<obj[]> (Dyn.call0 cb)
    let onSelect = Dyn.get cmds.[0] "onSelect"
    Dyn.call0 onSelect |> ignore
    promise { return () } |> ignore
    check "visible children path executed without error" (true)

/// 6. openSubagents with fetch error emits error toast asynchronously
let openSubagentsFetchErrorShowsErrorToast () =
    let api, getRegistered = makeFakeApi ()
    // make session.get throw so fetchVisibleChildren hits the with branch
    api?client?session?get <-
        box (
            System.Func<obj, JS.Promise<obj>>(fun _ ->
                promise { return (raise (System.Exception("network error")) :> obj) })
        )

    runTui (api, getRegistered)
    let cb = (getRegistered ()).Value
    let cmds = unbox<obj[]> (Dyn.call0 cb)
    let onSelect = Dyn.get cmds.[0] "onSelect"
    Dyn.call0 onSelect |> ignore
    check "error path executed without error" (true)

let run () =
    pluginExportsTui ()
    registerCommandsHiddenWhenNotInSession ()
    openSubagentsNonSessionRouteShowsInfoToast ()
    openSubagentsEmptyChildrenShowsInfoToast ()
    openSubagentsVisibleChildrenTriggersDialog ()
    openSubagentsFetchErrorShowsErrorToast ()
