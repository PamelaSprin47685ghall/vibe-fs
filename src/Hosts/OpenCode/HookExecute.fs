module Wanxiangshu.Hosts.Opencode.HookExecute

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.TreeSitterFormat
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Hosts.Opencode.HookSchemaDecoration
open Wanxiangshu.Runtime.ChildAgentRegistry
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.PatchToolsCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.LivelockGuard
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Runtime.ToolSequenceThrottle
open Wanxiangshu.Kernel.ToolResult

/// Shared per-process tool sequence throttle for PTY read delay.
let private toolSequenceThrottle = ToolSequenceThrottle()

/// Apply PTY read throttle before executing the tool.
let private applyPtyReadThrottle (tool: string) (nextArgs: obj) (sessionID: string) : JS.Promise<unit> =
    promise {
        let terminalId =
            if tool = "pty_read" then
                let id = Dyn.str nextArgs "id"
                if id = "" then None else Some id
            else
                None

        do! toolSequenceThrottle.BeforeExecution(sessionID, tool, terminalId)
    }

let private rewriteMimocodeApplyPatchArgsForExecute (output: obj) (input: obj) (args: obj) : unit =
    if toolNameFromHookInput input <> "apply_patch" then
        ()
    else
        match decodeApplyPatchFields args with
        | Ok fields -> setHookArgs output (createObj [ "patchText", box fields.PatchText ])
        | Error e -> setHookError output (wireEncodeToolError "apply_patch" e)

let toolExecuteBeforeFor (host: Host) (input: obj) (output: obj) : JS.Promise<unit> =
    promise {
        let tool = toolNameFromHookInput input
        let inputArgs = argsFromHookInput input
        let outputArgs = argsFromHookOutput output

        if
            (isNull inputArgs || Dyn.isNullish inputArgs)
            && (isNull outputArgs || Dyn.isNullish outputArgs)
        then
            raise (System.Exception("Tool validation error: arguments are null"))

        let rawArgs = resolveHookExecuteArgs input output

        if host = Mimocode && tool = "apply_patch" then
            rewriteMimocodeApplyPatchArgsForExecute output input rawArgs

        let args = resolveHookExecuteArgs input output

        if isNull args || Dyn.isNullish args then
            raise (System.Exception("Tool validation error: resolved arguments are null"))

        match ToolHookRuntime.executeBeforeGateway tool args with
        | Result.Error e ->
            setHookError output e
            raise (System.Exception("Tool validation error: " + e))
        | Result.Ok(nextArgs, env) ->
            setHookArgs output nextArgs

            let sessionID = ToolHookRuntime.tryExtractSessionId input |> Option.defaultValue ""

            let toolCallID =
                ToolHookRuntime.tryExtractToolCallId input |> Option.defaultValue ""

            ToolHookRuntime.saveCompliance sessionID toolCallID env

            do! applyPtyReadThrottle tool nextArgs sessionID

            setUiLabel args tool
            setUiLabel nextArgs tool
    }

let toolExecuteBefore (input: obj) (output: obj) : JS.Promise<unit> =
    toolExecuteBeforeFor opencode input output

// Forwarding entry-points for backward compatibility.
// PluginHooks.fs calls toolExecuteAfterFor from this module.
open Wanxiangshu.Hosts.Opencode.HookExecuteAfter

let toolExecuteAfterFor
    (host: Host)
    (pluginDirectory: string)
    (lifecycleObserver: Wanxiangshu.Hosts.Opencode.SessionLifecycleObserver.SessionLifecycleObserver)
    (registry: ChildAgentRegistry)
    (scope: RuntimeScope)
    (input: obj)
    (output: obj)
    : JS.Promise<unit> =
    HookExecuteAfter.toolExecuteAfterFor host pluginDirectory lifecycleObserver registry scope input output
