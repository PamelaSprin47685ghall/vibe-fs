module Wanxiangshu.Omp.Codec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Omp.Schema
module Dyn = Wanxiangshu.Shell.Dyn

[<Erase>]
type ISessionManager =
    abstract getEntries: (unit -> obj array) option
    abstract sessionId: string option
    abstract getSessionId: (unit -> obj) option

[<Erase>]
type IInnerPi =
    abstract createAgentSession: (obj -> JS.Promise<obj>) option

[<Erase>]
type IPi =
    abstract registerTool: obj -> unit
    abstract getActiveTools: (unit -> obj) option
    abstract setActiveTools: (string array -> JS.Promise<unit>) option
    abstract sendMessage: (obj * obj -> JS.Promise<unit>) option
    abstract pi: IInnerPi option

[<Erase>]
type IAgentSessionWrapper =
    abstract session: obj
    abstract dispose: (unit -> unit) option

[<Erase>]
type IChildSession =
    abstract abort: (unit -> unit) option

[<Erase>]
type INudgeHooksContext =
    abstract sessionManager: ISessionManager option
    abstract hasPendingMessages: (unit -> obj) option
    abstract workspaceRoot: string option
    abstract cwd: string option
    abstract ui: obj option

[<Erase>]
type IOmpContext =
    abstract sessionManager: ISessionManager option

type ToolTextResult = { ``type``: string; text: string }

type ToolResult =
    { content: ToolTextResult array
      isError: bool option
      display: bool option }

let textResult (text: string) : ToolResult =
    { content = [| { ``type`` = "text"; text = text } |]
      isError = None
      display = None }

let errorResult (text: string) : ToolResult =
    { content = [| { ``type`` = "text"; text = text } |]
      isError = Some true
      display = None }

let asErrorResult (error: obj) : ToolResult =
    errorResult (string error)

let private normalizeSessionId (sm: ISessionManager) : string option =
    let smObj = box sm
    if Dyn.typeIs (Dyn.get smObj "getSessionId") "function" then
        let id : obj = smObj?getSessionId()
        if Dyn.isNullish id then None else Some (string id)
    else sm.sessionId

let getSessionIdFromContext (ctxObj: obj) : string option =
    if Dyn.isNullish ctxObj then None
    else
        let ctx = unbox<IOmpContext> ctxObj
        match ctx.sessionManager with
        | None -> None
        | Some sm -> normalizeSessionId sm

let stringArraySchema (pi: obj) (description: string) : obj =
    let tb = Dyn.get pi "typebox"
    strArray description tb

let createAbortError () : obj =
    createObj [ "name", box "AbortError"; "message", box "Aborted" ]

let hasErrorName (error: obj) (name: string) : bool =
    not (Dyn.isNullish error) && string (Dyn.get error "name") = name

let optInt (o: obj) (key: string) : int option =
    let v = Dyn.get o key
    if Dyn.isNullish v then None else Some(unbox<int> v)
