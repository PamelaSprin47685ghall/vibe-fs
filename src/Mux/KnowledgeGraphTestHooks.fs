module Wanxiangshu.Mux.KnowledgeGraphTestHooks

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Mux.KnowledgeGraphRuntimeMux
open Wanxiangshu.Shell.KnowledgeGraphTestHooks

type MuxKnowledgeGraphRuntime with
    member this.KgTestOps : KgTestOps =
        { createTestPorts = this.CreateTestPorts
          registerJob = this.RegisterJob
          hasJob = this.HasJobForTest
          mapLaunch = fun l ->
              box (createObj [
                  "agent", box l.agent
                  "title", box l.title
                  "prompt", box l.prompt
                  "result", box l.result
              ]) }

type MuxKnowledgeGraphTestHooks(runtime: MuxKnowledgeGraphRuntime) =
    let ops = runtime.KgTestOps

    member _.RegisterJob(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        if String.IsNullOrWhiteSpace workspaceRoot then
            failwith "Knowledge graph job workspaceRoot must be a non-empty directory path."

        registerTestJob ops sessionID workspaceRoot kindTag payload

    member _.TakeLaunches() : obj array =
        takeTestLaunches ops

    member _.WaitJobs() : JS.Promise<unit> =
        waitTestJobs ops

    member _.HasJob(sessionID: string) : bool =
        hasTestJob ops sessionID

type MuxKnowledgeGraphRuntime with
    member this.TestHooks : MuxKnowledgeGraphTestHooks = MuxKnowledgeGraphTestHooks(this)