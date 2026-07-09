module Wanxiangshu.Omp.SessionLifecycle

open Fable.Core.JsInterop
open Wanxiangshu.Omp.NudgeHooks
open Wanxiangshu.Omp.TodoHooks
open Wanxiangshu.Omp.Codec
open Wanxiangshu.Shell.ReviewRuntime
open Wanxiangshu.Shell.FallbackRuntimeState

let registerSessionLifecycle (pi: obj) (reviewStore: ReviewStore) (fallbackRuntime: FallbackRuntimeState) : unit =
    pi?on (
        "before_agent_start",
        box (fun (event: obj) (ctx: obj) -> beforeAgentStartHandler pi event ctx fallbackRuntime)
    )

    pi?on ("tool_call", box (fun (event: obj) (ctx: obj) -> toolCallHandler pi reviewStore event ctx))
    pi?on ("tool_result", box (fun (event: obj) (ctx: obj) -> TodoHooks.toolResultHandler pi reviewStore event ctx))

    pi?on (
        "agent_end",
        box (fun (_event: obj) (ctx: obj) ->
            promise {
                let sidOpt = getSessionIdFromContext ctx

                match sidOpt with
                | Some sid when sid <> "" ->
                    fallbackRuntime.SetEventHandlingActive sid true

                    try
                        do! agentEndHandler pi reviewStore fallbackRuntime ctx
                    finally
                        fallbackRuntime.SetEventHandlingActive sid false
                | _ -> do! agentEndHandler pi reviewStore fallbackRuntime ctx
            })
    )

    pi?on ("session_start", box (fun (_event: obj) (ctx: obj) -> TodoHooks.sessionStartHandler pi reviewStore ctx))
    pi?on ("session_prompt", box (fun (_event: obj) (ctx: obj) -> TodoHooks.sessionPromptHandler pi reviewStore ctx))
    pi?on ("turn_start", box (fun (event: obj) (ctx: obj) -> turnStartHandler pi event ctx fallbackRuntime))
    pi?on ("session_shutdown", box (fun (_event: obj) (ctx: obj) -> TodoHooks.sessionShutdownHandler reviewStore ctx))
