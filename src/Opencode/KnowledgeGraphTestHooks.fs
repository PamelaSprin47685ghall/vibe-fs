module Wanxiangshu.Opencode.KnowledgeGraphTestHooks

open Fable.Core
open Wanxiangshu.Opencode.KnowledgeGraphRuntime
open Wanxiangshu.Shell.KnowledgeGraphTestHooks

type KnowledgeGraphRuntime with
    member this.KgTestOps : KgTestOps =
        { createTestPorts = this.CreateTestPorts
          registerJob = this.RegisterJob
          hasJob = fun _ -> false
          mapLaunch = box }

type KnowledgeGraphTestHooks(runtime: KnowledgeGraphRuntime) =
    let ops = runtime.KgTestOps

    member _.RegisterJob(sessionID: string, workspaceRoot: string, kindTag: string, payload: obj) : unit =
        registerTestJob ops sessionID workspaceRoot kindTag payload

    member _.TakeLaunches() : obj array =
        takeTestLaunches ops

    member _.WaitJobs() : JS.Promise<unit> =
        waitTestJobs ops

type KnowledgeGraphRuntime with
    member this.TestHooks : KnowledgeGraphTestHooks = KnowledgeGraphTestHooks(this)