module Wanxiangshu.Omp.SessionLifecycle

open Fable.Core.JsInterop
open Wanxiangshu.Omp.SessionLifecycleHooks
open Wanxiangshu.Omp.NudgeHooks
open Wanxiangshu.Shell.ReviewRuntime

let registerSessionLifecycle (pi: obj) (reviewStore: ReviewStore) : unit =
    pi?on ("before_agent_start", box (fun (event: obj) (ctx: obj) -> beforeAgentStartHandler pi event ctx))
    pi?on ("tool_call", box (fun (event: obj) (ctx: obj) -> toolCallHandler pi reviewStore event ctx))
    pi?on ("tool_result", box (fun (event: obj) (ctx: obj) -> toolResultHandler pi reviewStore event ctx))
    pi?on ("agent_end", box (fun (_event: obj) (ctx: obj) -> agentEndHandler pi reviewStore ctx))
    pi?on ("session_start", box (fun (_event: obj) (ctx: obj) -> sessionStartHandler pi reviewStore ctx))
    pi?on ("session_prompt", box (fun (_event: obj) (ctx: obj) -> sessionPromptHandler pi reviewStore ctx))
    pi?on ("turn_start", box (fun (_event: obj) (ctx: obj) -> turnStartHandler pi ctx))
    pi?on ("session_shutdown", box (fun (_event: obj) (ctx: obj) -> sessionShutdownHandler reviewStore ctx))
