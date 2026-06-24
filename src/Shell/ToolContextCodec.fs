module VibeFs.Shell.ToolContextCodec

open VibeFs.Kernel.Domain
open VibeFs.Kernel.ToolContext
open VibeFs.Shell.Dyn

let private firstString (o: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun k ->
        let v = Dyn.get o k
        if Dyn.isNullish v then None
        else
            let s = string v
            if s = "" then None else Some s)

let decodeOpencodeToolContext (context: obj) (fallbackDir: string) : ToolExecutionContext =
    let directory =
        match firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] with
        | Some s -> s
        | None -> fallbackDir
    let sessionId =
        match firstString context [ "sessionID"; "sessionId"; "session_id" ] with
        | Some s -> s
        | None -> ""
    { Directory = directory; SessionId = sessionId; WorkspaceId = None }

let muxConfigDirectoryFallback (config: obj) : string =
    match firstString config [ "directory"; "cwd"; "workspacePath" ] with
    | Some s -> s
    | None -> ""

let decodeMuxConfig (config: obj) : Result<ToolExecutionContext, DomainError> =
    let wid = Dyn.get config "workspaceId"
    if isNull wid || string wid = "" then
        Error (InvalidIntent ("mux", "workspaceId", "required"))
    else
        let workspaceId = string wid
        let directory =
            [ "directory"; "cwd"; "workspacePath" ]
            |> List.tryPick (fun k -> firstString config [ k ])
            |> function
                | Some s -> s
                | None -> ""
        let sessionId =
            match firstString config [ "sessionID"; "sessionId"; "session_id" ] with
            | Some s -> s
            | None -> ""
        Ok { Directory = directory; SessionId = sessionId; WorkspaceId = Some workspaceId }