module Wanxiangshu.Runtime.ToolContextCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolContext
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.ChildAgentRegistry

[<Global("globalThis.process")>]
let private nodeProcess: obj = jsNative

let private envVar (name: string) : string =
    let v = nodeProcess?env?(name)
    if Wanxiangshu.Runtime.Dyn.isNullish v then "" else string v

type IOpenCodeToolContext =
    abstract directory: string with get
    abstract cwd: string with get
    abstract workspaceDir: string with get
    abstract workspace_dir: string with get
    abstract workingDirectory: string with get
    abstract sessionID: string with get
    abstract sessionId: string with get
    abstract session_id: string with get

type IMuxToolContext =
    abstract workspaceId: string with get
    abstract directory: string with get
    abstract cwd: string with get
    abstract workspacePath: string with get
    abstract sessionID: string with get
    abstract sessionId: string with get
    abstract session_id: string with get

let private firstNonEmpty (options: string list) : string option =
    options
    |> List.tryPick (fun s ->
        if Wanxiangshu.Runtime.Dyn.isNullish (box s) then
            None
        else
            let t = s.Trim()
            if t = "" then None else Some t)

let decodeOpencodeToolContext (context: IOpenCodeToolContext) (fallbackDir: string) : ToolExecutionContext =
    let directory =
        match
            firstNonEmpty
                [ context.directory
                  context.cwd
                  context.workspaceDir
                  context.workspace_dir
                  context.workingDirectory ]
        with
        | Some s -> s
        | None -> fallbackDir

    let sessionId =
        match firstNonEmpty [ context.sessionID; context.sessionId; context.session_id ] with
        | Some s -> s
        | None -> ""

    let finalDir = directory

    { Directory = finalDir
      SessionId = Id.sessionIdQuick sessionId
      WorkspaceId = None
      ChildRegistry = ChildAgentRegistry.Create() }

let muxConfigDirectoryFallback (config: IMuxToolContext) : string =
    match firstNonEmpty [ config.directory; config.cwd; config.workspacePath ] with
    | Some s -> s
    | None -> ""

let decodeMuxConfig (config: IMuxToolContext) : Result<ToolExecutionContext, DomainError> =
    let wid = config.workspaceId

    if not (Wanxiangshu.Runtime.Dyn.isNullish (box wid)) && wid <> "" then
        let directory =
            match firstNonEmpty [ config.directory; config.cwd; config.workspacePath ] with
            | Some s -> s
            | None -> ""

        let sessionId =
            match firstNonEmpty [ config.sessionID; config.sessionId; config.session_id ] with
            | Some s -> s
            | None -> ""

        Ok
            { Directory = directory
              SessionId = Id.sessionIdQuick sessionId
              WorkspaceId = Some(Id.workspaceIdQuick wid)
              ChildRegistry = ChildAgentRegistry.Create() }
    else
        Error(InvalidIntent("mux", "workspaceId", "required"))

let decodeMuxConfigLenient (config: IMuxToolContext) : ToolExecutionContext =
    match decodeMuxConfig config with
    | Ok ctx -> ctx
    | Error _ ->
        let workspaceId = firstNonEmpty [ config.workspaceId ]

        let directory =
            firstNonEmpty [ config.directory; config.cwd; config.workspacePath ]
            |> Option.defaultValue ""

        let sessionId =
            firstNonEmpty [ config.sessionID; config.sessionId; config.session_id ]
            |> Option.defaultValue ""

        { Directory = directory
          SessionId = Id.sessionIdQuick sessionId
          WorkspaceId = workspaceId |> Option.map Id.workspaceIdQuick
          ChildRegistry = ChildAgentRegistry.Create() }
