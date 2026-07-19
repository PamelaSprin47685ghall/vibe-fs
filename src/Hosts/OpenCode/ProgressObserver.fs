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
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure
open Wanxiangshu.Runtime.SubsessionEventRouter
open Wanxiangshu.Runtime.WorkBacklogToolsCodec
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Messaging.OpencodeHostEvent
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.BacklogEventWriter

type ProgressObserver
    (
        host: Host,
        ctx: obj,
        backlogSession: Wanxiangshu.Runtime.BacklogSession.BacklogSession,
        fallbackRuntime: FallbackRuntimeStore
    ) =

    let resolvedUnitPromise () : JS.Promise<unit> = Promise.lift ()

    let handleTodoWriteAfter (input: obj) (output: obj) : JS.Promise<unit> =
        promise {
            let methodologies = selectMethodologiesFromHookArgs (argsFromHookInput input)

            let isError =
                hookOutputError output <> ""
                || ToolExecute.isNetworkErrorText (hookOutputText output)

            match hookOutputString output with
            | Some _ ->
                if not isError then
                    let newBase = todoWriteOutput methodologies
                    setHookOutputString output newBase

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
                            |> Array.forall (fun t -> Wanxiangshu.Kernel.ToolArgs.TodoItemStatus.isTerminal t.Status)

                        let ev =
                            { CurrentTurnEvidence.empty with
                                Todos = if allCompleted then TodosCompleted else TodosNotCompleted }

                        do! SubsessionEventRouter.routeEvidence directory sid ev |> Promise.map ignore
                    | _ -> ()
            | None -> ()
        }

    let handleTaskCompleteAfter (input: obj) : JS.Promise<unit> =
        promise {
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

                fallbackRuntime.Update(
                    sid,
                    setCore
                        { st with
                            Lifecycle = FallbackLifecycle.TaskComplete }
                )
        }

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
            let tool = normalizeToolName host (toolNameFromHookInput input)

            if tool = todoWriteToolName host then
                do! handleTodoWriteAfter input output
            elif tool = "task_complete" then
                do! handleTaskCompleteAfter input
        }
