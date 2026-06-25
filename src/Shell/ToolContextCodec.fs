module VibeFs.Shell.ToolContextCodec

open VibeFs.Kernel.Domain
open VibeFs.Kernel.ToolContext
open VibeFs.Shell.Dyn

let firstTrimmedString (o: obj) (keys: string list) : string option =
    keys
    |> List.tryPick (fun k ->
        let v = Dyn.get o k
        if Dyn.isNullish v then None
        else
            let s = (string v).Trim()
            if s = "" then None else Some s)

let private firstString (o: obj) (keys: string list) : string option = firstTrimmedString o keys

let decodeOpencodeToolContext (context: obj) (fallbackDir: string) : ToolExecutionContext =
    let directory =
        match firstString context [ "directory"; "cwd"; "workspaceDir"; "workspace_dir"; "workingDirectory" ] with
        | Some s -> s
        | None -> fallbackDir
    let sessionId =
        match firstString context [ "sessionID"; "sessionId"; "session_id" ] with
        | Some s -> s
        | None -> ""
    {
        Directory = directory
        SessionId = Id.sessionIdQuick sessionId
        WorkspaceId = None
    }

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
        Ok {
            Directory = directory
            SessionId = Id.sessionIdQuick sessionId
            WorkspaceId = Some (Id.workspaceIdQuick workspaceId)
        }

let decodeMuxConfigLenient (config: obj) : ToolExecutionContext =
    match decodeMuxConfig config with
    | Ok ctx -> ctx
    | Error _ ->
        let workspaceId = firstTrimmedString config [ "workspaceId" ]
        let directory =
            firstTrimmedString config [ "directory"; "cwd"; "workspacePath" ]
            |> Option.defaultValue ""
        let sessionId =
            firstTrimmedString config [ "sessionID"; "sessionId"; "session_id" ]
            |> Option.defaultValue ""
        {
            Directory = directory
            SessionId = Id.sessionIdQuick sessionId
            WorkspaceId = workspaceId |> Option.map Id.workspaceIdQuick
        }