module Wanxiangshu.Opencode.KnowledgeGraphTestHooks

open Fable.Core
open Wanxiangshu.Shell.KnowledgeGraphTestHooks
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState

type KnowledgeGraphRuntime with
    member this.TestHooks : KgTestOps =
        { createTestPorts = fun () -> this.CreateTestPorts()
          registerJob = fun (sessionID, ctx) -> this.RegisterJob(sessionID, ctx)
          takeLaunches =
              fun (ports: KnowledgeGraphRuntimeTestPorts) ->
                  unbox<BookkeeperLaunch list>
                      (ports.SwapState(fun s ->
                          let launches, next = drainLaunches s
                          next, box launches))
          waitJobs =
              fun (ports: KnowledgeGraphRuntimeTestPorts) ->
                  promise {
                      do! ports.RunOnCommandQueue(fun () -> Promise.lift ())
                      do! ports.AwaitBackgroundSinkJobs()
                  }
          hasJob = fun _ -> false
          mapLaunch = box }
