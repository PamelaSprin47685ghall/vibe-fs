module VibeFs.Opencode.PluginMimoTui

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel

let private dateNow () : float =
    float (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())

let private relTime (updated: float) : string =
    let diff = dateNow () - updated
    if diff < 1000.0 then "active now"
    else
        let sec = diff / 1000.0
        if sec < 60.0 then $"{int sec}s ago"
        elif sec < 3600.0 then $"{int (sec / 60.0)}m ago"
        else $"{int (sec / 3600.0)}h ago"

let private numField (o: obj) (parent: string) (child: string) : float =
    let p = Dyn.get o parent
    if Dyn.isNullish p then 0.0
    else
        let v = Dyn.get p child
        if Dyn.isNullish v then 0.0 else unbox<float> v

let private checkpointWriterTitlePrefix = "checkpoint-writer:"

let private isVisibleSubagent (child: obj) : bool =
    let title = Dyn.str child "title"
    not (title.StartsWith checkpointWriterTitlePrefix)

let private childLabel (child: obj) : string =
    let title = Dyn.str child "title"
    if title = "" then Dyn.str child "id" else title

let private isCheckpointWriterSession (child: obj) : bool =
    (childLabel child).StartsWith("checkpoint-writer:")

let private toOption (child: obj) : obj =
    let id = Dyn.str child "id"
    let label = childLabel child
    box {| title = label; value = id; description = relTime (numField child "time" "updated") |}

let private awaitObj (p: obj) : JS.Promise<obj> = unbox<JS.Promise<obj>> p

let private toast (api: obj) (variant: string) (message: string) : unit =
    api?ui?toast(box {| message = message; variant = variant |}) |> ignore

/// Fetch every subagent that shares this session's top-level parent and present
/// them in a switchable dialog. Child sessions always carry parentID == the
/// top-level manager session, so resolving the parent first lets the dialog work
/// identically whether invoked from the manager or from inside a subagent.
let private openSubagents (api: obj) : unit =
    let route = api?route?current
    if Dyn.str route "name" <> "session" then
        toast api "info" "Open a session to view its subagents"
    else
        let sessionID = Dyn.str (Dyn.get route "params") "sessionID"
        let directory = api?state?path?directory
        promise {
            try
                let! sessRes = awaitObj (api?client?session?get(box {| sessionID = sessionID; directory = directory |}))
                let sess = Dyn.get sessRes "data"
                let parentID = if Dyn.isNullish sess then "" else Dyn.str sess "parentID"
                let rootID = if parentID = "" then sessionID else parentID
                let! childRes = awaitObj (api?client?session?children(box {| sessionID = rootID; directory = directory |}))
                let data = Dyn.get childRes "data"
                let children =
                    if Dyn.isNullish data then [||]
                    else unbox<obj[]> data |> Array.filter isVisibleSubagent
                let visible =
                    children |> Array.filter (fun c -> not (isCheckpointWriterSession c))
                if visible.Length = 0 then
                    toast api "info" "No subagents running yet"
                else
                    let options =
                        visible
                        |> Array.sortBy (fun c -> numField c "time" "created")
                        |> Array.map toOption
                    let onSelect =
                        System.Func<obj, unit>(fun opt ->
                            api?route?navigate("session", box {| sessionID = Dyn.str opt "value" |}) |> ignore
                            api?ui?dialog?clear() |> ignore)
                    let props =
                        box {| title = "Subagents"
                               placeholder = "Switch to subagent"
                               current = sessionID
                               options = options
                               onSelect = onSelect |}
                    api?ui?dialog?replace(System.Func<obj>(fun () -> api?ui?DialogSelect(props))) |> ignore
            with _ ->
                toast api "error" "Failed to load subagents"
        }
        |> Promise.start

let private registerCommands (api: obj) : unit =
    api?command?register(System.Func<obj>(fun () ->
        let inSession = Dyn.str (api?route?current) "name" = "session"
        box [| box {| title = "Subagents"
                      value = "vibe.subagents"
                      description = "List and switch to running subagents"
                      category = "vibe-fs"
                      slash = box {| name = "subagents" |}
                      hidden = not inSession
                      onSelect = System.Func<unit>(fun () -> openSubagents api) |} |]
    )) |> ignore

let private tuiImpl (api: obj) : JS.Promise<unit> =
    promise { registerCommands api }

[<ExportDefault>]
let plugin = box {| id = "vibe-fs-subagents"; tui = tuiImpl |}
