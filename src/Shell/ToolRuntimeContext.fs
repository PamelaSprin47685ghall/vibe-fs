module Wanxiangshu.Shell.ToolRuntimeContext

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolContext
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeContextCodec
open Wanxiangshu.Shell.ToolContextCodec

type IToolRuntimeContext = {
    Execution: ToolExecutionContext
    AbortSignal: obj option
}

let private abortSignalOption (signal: obj) : obj option =
    if Dyn.isNullish signal then None else Some signal

let fromMuxConfig (config: obj) : Result<IToolRuntimeContext, DomainError> =
    decodeMuxConfig (unbox<IMuxToolContext> config)
    |> Result.map (fun execution ->
        { Execution = execution; AbortSignal = abortSignalOption (Dyn.get config "abortSignal") })

let fromOpencode (context: obj) (fallbackDir: string) : IToolRuntimeContext =
    let execution = decodeOpencodeToolContext (unbox<IOpenCodeToolContext> context) fallbackDir
    { Execution = execution; AbortSignal = abortSignalOption (getAbortSignalFromContext context) }

let pluginDirectoryFromCtx (ctx: obj) : string =
    (fromOpencode ctx "").Execution.Directory

let sessionId (ctx: IToolRuntimeContext) : SessionId = ctx.Execution.SessionId

let workspaceId (ctx: IToolRuntimeContext) : WorkspaceId option = ctx.Execution.WorkspaceId

let sessionIdString (ctx: IToolRuntimeContext) : string = Id.sessionIdValue ctx.Execution.SessionId

let workspaceIdString (ctx: IToolRuntimeContext) : string =
    ctx.Execution.WorkspaceId |> Option.map Id.workspaceIdValue |> Option.defaultValue ""
