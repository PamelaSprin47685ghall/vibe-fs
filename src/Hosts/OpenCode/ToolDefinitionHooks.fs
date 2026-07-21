module Wanxiangshu.Hosts.Opencode.ToolDefinitionHooks

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Hosts.Opencode.AgentConfig
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.ToolHookRuntime
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Hosts.Opencode.HookSchemaDecode

let private collectToolDefinitions (host: Host) (input: obj) (output: obj) : unit =
    let toolID = toolIdFromDefinitionHookInput input

    let schemaForRegistry =
        let jsonSchema = get output "jsonSchema"

        if not (isNullish jsonSchema) then
            jsonSchema
        else
            let parameters = get output "parameters"

            if not (isNullish parameters) then
                let fromEffect = tryBuildJsonSchemaFromEffectSchema parameters

                if not (isNullish fromEffect) then
                    fromEffect
                else
                    parameters
            else
                null

    if not (isNullish schemaForRegistry) then
        registerSchemaTypes toolID schemaForRegistry

let private registerToolHooks (toolID: string) (output: obj) : unit =
    let schemaForRegistry =
        let jsonSchema = get output "jsonSchema"

        if not (isNullish jsonSchema) then
            jsonSchema
        else
            let parameters = get output "parameters"

            if not (isNullish parameters) then
                let fromEffect = tryBuildJsonSchemaFromEffectSchema parameters

                if not (isNullish fromEffect) then
                    fromEffect
                else
                    parameters
            else
                null

    if not (isNullish schemaForRegistry) then
        registerSchemaTypes toolID schemaForRegistry

let toolDefinitionFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let toolID = toolIdFromDefinitionHookInput input
        decorateControlFields toolID output
        collectToolDefinitions host input output
        registerToolHooks toolID output
    }

let toolDefinition (input: obj) (output: obj) : JS.Promise<unit> = toolDefinitionFor opencode input output
