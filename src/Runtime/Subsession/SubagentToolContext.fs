module Wanxiangshu.Runtime.SubagentToolContext

open Wanxiangshu.Runtime.Dyn

type SubagentAiSettings =
    { ModelString: string option
      ThinkingLevel: string option
      Variant: string option }

let emptySettings: SubagentAiSettings =
    { ModelString = None
      ThinkingLevel = None
      Variant = None }

type ToolContext =
    { Directory: string
      SessionID: string
      AbortSignal: obj }

let firstString (ctx: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun key ->
        let v = Dyn.get ctx key
        if Dyn.isNullish v then None else Some(string v))

let getAbortSignal (context: obj) : obj =
    if Dyn.isNullish context then
        null
    else
        let abort = Dyn.get context "abort"
        if Dyn.isNullish abort then null else abort

let extractToolContext (context: obj) (pluginDirectory: string) : ToolContext =
    let directory =
        match firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] with
        | Some s when s <> "" -> s
        | _ -> pluginDirectory

    let sessionID =
        match firstString context [ "sessionID"; "sessionId"; "session_id" ] with
        | Some s when s <> "" -> s
        | _ -> ""

    { Directory = directory
      SessionID = sessionID
      AbortSignal = getAbortSignal context }
