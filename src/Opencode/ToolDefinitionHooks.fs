module VibeFs.Opencode.ToolDefinitionHooks

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.MagicCore
open VibeFs.Kernel.MagicTodo
open VibeFs.Opencode.AgentConfig
open VibeFs.Opencode.HookSchema
open VibeFs.Opencode.MagicTodo

let private setKey (o: obj) (k: string) (v: obj) : unit = o?(k) <- v

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let toolID = Dyn.str input "toolID"
        if toolID = magicTodoToolNameFor host then
            match host with
            | Opencode ->
                setKey output "description" (box toolDescription)
                setKey output "jsonSchema" (buildMagicTodoSchema ())
            | Mimocode ->
                setKey output "description" (box fusedTaskToolDescription)
                rewriteToolJsonSchema setKey mergeMagicReportIntoTaskSchema output
    }

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> =
    toolDefinitionFor opencode input output
