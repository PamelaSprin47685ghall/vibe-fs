module Wanxiangshu.Opencode.ProgressObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Kernel.ToolOutputInfo
open Wanxiangshu.Kernel.FallbackKernel.Types

open Wanxiangshu.Shell
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.OpencodeHookInputCodec
open Wanxiangshu.Shell.FallbackRuntimeState
open Wanxiangshu.Shell.EventLogRuntime
open Wanxiangshu.Shell.WorkBacklogToolsCodec
open Wanxiangshu.Shell.ToolRuntimeContext
open Wanxiangshu.Shell.OpencodeSessionEventCodecCommon
open Wanxiangshu.Opencode.BacklogSession

[<Import("appendFileSync", "node:fs")>]
let private appendFileSync (path: string) (data: string) : unit = jsNative

module Dyn = Wanxiangshu.Shell.Dyn

type ProgressObserver
    (
        host: Host,
        ctx: obj,
        backlogSession: Wanxiangshu.Opencode.BacklogSession.BacklogSession,
        fallbackRuntime: FallbackRuntimeState
    ) =

    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    member _.OnChatMessage(sessionID: SessionId, agent: string, parts: obj) : JS.Promise<unit> =
        let text = getPartsText parts
        let sid = Id.sessionIdValue sessionID

        if not (isNudgePrompt text) && agent <> "" then
            fallbackRuntime.SetAgentName sid agent

        resolvedUnitPromise ()

    member _.HandleToolExecuteAfter (input: obj) (output: obj) : JS.Promise<unit> =
        promise {
            let sessionIDStr = sessionIdFromHookInput input ""
            let tool = normalizeToolName host (toolNameFromHookInput input)

            if tool = todoWriteToolName host then
                let methodologies = selectMethodologiesFromHookArgs (argsFromHookInput input)

                let isError =
                    hookOutputError output <> ""
                    || ToolExecute.isNetworkErrorText (hookOutputText output)

                match hookOutputString output with
                | Some oldText ->
                    if not isError then
                        let newBase = todoWriteOutput methodologies

                        let finalOutput =
                            let markerIdx = oldText.IndexOf(ToolHookRuntime.reprimandMarker)

                            if markerIdx >= 0 then
                                newBase + oldText.Substring(markerIdx)
                            else
                                newBase

                        setHookOutputString output finalOutput

                    let directory =
                        (fromOpencode input (pluginDirectoryFromCtx ctx)).Execution.Directory

                    let sid = sessionIdFromHookInput input ""

                    let debugMsg =
                        sprintf "DEBUG PROGRESS_OBSERVER: tool=%s, sid=%s, directory=%s\n" tool sid directory

                    try
                        appendFileSync "/tmp/debug-mimocode.txt" debugMsg
                    with _ ->
                        ()

                    let args = argsFromHookInput input

                    if not (Dyn.isNullish args) && not isError then
                        match decodeTodoWriteArgs (host = Mimocode) args with
                        | Ok(decodedArgs, _) when sid <> "" ->
                            do! appendWorkBacklogCommittedOrFail directory sid decodedArgs
                        | _ -> ()
                | None -> ()
            elif tool = "task_complete" then
                let sid = sessionIdFromHookInput input ""

                if sid <> "" then
                    let st = fallbackRuntime.GetOrCreateState sid

                    fallbackRuntime.UpdateState
                        sid
                        { st with
                            Lifecycle = FallbackLifecycle.TaskComplete }
            elif tool = "submit_review" then
                match hookOutputString output with
                | Some text when isSubmitReviewWipProgressOutput text -> ()
                | _ -> ()
        }
