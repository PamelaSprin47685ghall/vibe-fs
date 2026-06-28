module Wanxiangshu.Opencode.ToolDefinitionHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Shell

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.WorkBacklog
open Wanxiangshu.Opencode.AgentConfig
open Wanxiangshu.Opencode.HookSchema
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeHookInputCodec

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let toolID = toolIdFromDefinitionHookInput input
        if toolID = todoWriteToolNameFor host then
            match host with
            | Opencode
            | Mux ->
                setKey output "description" (box toolDescription)
                setKey output "jsonSchema" (buildWorkBacklogSchema ())
            | Mimocode ->
                setKey output "description" (box fusedTaskToolDescription)
                let parameters = get output "parameters"
                if not (isNullish parameters) then
                    let safeExtend = get parameters "safeExtend"
                    let extend = get parameters "extend"
                    if (not (isNullish safeExtend) && Dyn.typeIs safeExtend "function") || (not (isNullish extend) && Dyn.typeIs extend "function") then
                        setKey output "parameters" (mergeWorkBacklogReportIntoTaskSchema parameters)
                    else
                        setKey output "parameters" (buildWorkBacklogSchema ())
                else
                    setKey output "jsonSchema" (buildWorkBacklogSchema ())
            | Omp -> ()
        elif WarnTdd.isModificationTool toolID then
            rewriteToolJsonSchema setKey (injectWarnTddIntoJsonSchema) output
        if WarnTdd.isWarnRequiredTool toolID then
            rewriteToolJsonSchema setKey (injectWarnIntoJsonSchema) output
    }

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    toolDefinitionFor opencode input output