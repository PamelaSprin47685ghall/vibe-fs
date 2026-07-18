namespace Wanxiangshu.Hosts.Opencode.ProgressObserver

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.Nudge.TodoStatus
open Wanxiangshu.Kernel.Nudge.SubmitReviewHooks
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Kernel.Subsession.Fold

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.OpencodeHookInputCodec
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.LeaseTransitions
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.EventLogRuntime
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.OpencodeHostEvent
open Wanxiangshu.Hosts.Opencode.BacklogSession

type ProgressObserver
    (
        host: Host,
        ctx: obj,
        backlogSession: Wanxiangshu.Hosts.Opencode.BacklogSession.BacklogSession,
        fallbackRuntime: FallbackRuntimeStore
    ) =

    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    member _.OnChatMessage
        (sessionID: Wanxiangshu.Kernel.Primitives.Identity.SessionId, agent: string, parts: obj)
        : JS.Promise<unit> =
        let text = getPartsText parts
        let sid = Id.sessionIdValue sessionID

        if not (isNudgePrompt text) && agent <> "" then
            fallbackRuntime.UpdateSession(sid, recordAgentName agent)

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

                    let args = argsFromHookInput input

                    if not (Dyn.isNullish args) && not isError then
                        match decodeTodoWriteArgs (host = Mimocode) args with
                        | Ok(decodedArgs, []) when sid <> "" ->
                            do! appendWorkBacklogCommittedOrFail directory sid decodedArgs

                            let allCompleted =
                                decodedArgs.Todos
                                |> Array.forall (fun t ->
                                    Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.isTerminal t.Status)

                            let ev =
                                { CurrentTurnEvidence.empty with
                                    Todos = if allCompleted then TodosCompleted else TodosNotCompleted }

                            do! SubsessionEventRouter.routeEvidence directory sid ev |> Promise.map ignore
                        | _ -> ()
                | None -> ()
            elif tool = "task_complete" then
                let sid = sessionIdFromHookInput input ""

                if sid <> "" then
                    let args = argsFromHookInput input
                    let output = if Dyn.isNullish args then "" else Dyn.str args "output"

                    let evidence =
                        { CurrentTurnEvidence.empty with
                            Outcome = CompletionRequested output }

                    let directory =
                        (fromOpencode input (pluginDirectoryFromCtx ctx)).Execution.Directory

                    let! _ = SubsessionEventRouter.routeEvidence directory sid evidence

                    let st = fallbackRuntime.GetOrCreateState sid

                    fallbackRuntime.UpdateState
                        sid
                        { st with
                            Lifecycle = FallbackLifecycle.TaskComplete }
        }
