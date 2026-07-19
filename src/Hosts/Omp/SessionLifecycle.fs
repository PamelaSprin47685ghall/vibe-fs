module Wanxiangshu.Hosts.Omp.SessionLifecycle

open Fable.Core.JsInterop
open Wanxiangshu.Hosts.Omp.NudgeToolFilter
open Wanxiangshu.Hosts.Omp.NudgeHooks
open Wanxiangshu.Hosts.Omp.NudgeDispatchLogic
open Wanxiangshu.Hosts.Omp.TodoHooks
open Wanxiangshu.Hosts.Omp.TodoStateManagement
open Wanxiangshu.Hosts.Omp.Codec
open Wanxiangshu.Runtime.ReviewRuntime
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

let registerSessionLifecycle (pi: obj) (reviewStore: ReviewStore) (fallbackRuntime: FallbackRuntimeStore) : unit =
    pi?on (
        "before_agent_start",
        box (fun (event: obj) (ctx: obj) -> beforeAgentStartHandler pi event ctx fallbackRuntime)
    )

    pi?on ("tool_call", box (fun (event: obj) (ctx: obj) -> toolCallHandler pi reviewStore event ctx))
    pi?on ("tool_result", box (fun (event: obj) (ctx: obj) -> toolResultHandler pi reviewStore event ctx))

    pi?on (
        "agent_end",
        box (fun (_event: obj) (ctx: obj) ->
            promise {
                let sidOpt = getSessionIdFromContext ctx

                match sidOpt with
                | Some sid when sid <> "" ->
                    fallbackRuntime.Update(sid, setEventHandlingActive true)

                    try
                        do! agentEndHandler pi reviewStore fallbackRuntime ctx
                    finally
                        fallbackRuntime.Update(sid, setEventHandlingActive false)
                | _ -> do! agentEndHandler pi reviewStore fallbackRuntime ctx
            })
    )

    pi?on ("session_start", box (fun (_event: obj) (ctx: obj) -> sessionStartHandler pi reviewStore ctx))
    pi?on ("session_prompt", box (fun (_event: obj) (ctx: obj) -> sessionPromptHandler pi reviewStore ctx))
    pi?on ("turn_start", box (fun (event: obj) (ctx: obj) -> turnStartHandler pi event ctx fallbackRuntime))

    pi?on (
        "session_shutdown",
        box (fun (_event: obj) (ctx: obj) -> sessionShutdownHandler reviewStore fallbackRuntime ctx)
    )
