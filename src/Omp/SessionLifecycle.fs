module Wanxiangshu.Omp.SessionLifecycle

open Fable.Core.JsInterop
open Wanxiangshu.Omp.KnowledgeGraph.Runtime
open Wanxiangshu.Omp.SessionLifecycleHooks
open Wanxiangshu.Omp.SessionCompacting
open Wanxiangshu.Shell.ReviewRuntime

let registerSessionLifecycle (pi: obj) (reviewStore: ReviewStore) (kgRuntime: OmpKnowledgeGraphRuntime) : unit =
    pi?on("before_agent_start", box (fun (event: obj) (ctx: obj) -> beforeAgentStartHandler pi event ctx))
    pi?on("tool_call", box (fun (event: obj) (ctx: obj) -> toolCallHandler pi reviewStore kgRuntime event ctx))
    pi?on("tool_result", box (fun (event: obj) (ctx: obj) -> toolResultHandler pi reviewStore kgRuntime event ctx))
    pi?on("agent_end", box (fun (_event: obj) (ctx: obj) -> agentEndHandler pi reviewStore ctx))
    pi?on("session_start", box (fun (_event: obj) (ctx: obj) -> sessionStartHandler pi kgRuntime ctx))
    pi?on("turn_start", box (fun (_event: obj) (ctx: obj) -> turnStartHandler pi ctx))
    pi?on("session.compacting", box (fun (event: obj) (ctx: obj) -> sessionCompactingHandler pi event ctx))
    pi?on("session_shutdown", box (fun (_event: obj) (ctx: obj) -> sessionShutdownHandler reviewStore kgRuntime ctx))
