module Wanxiangshu.Shell.KnowledgeGraphTestHooks

open Fable.Core
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.JobTesting
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts

/// Host-agnostic test hook operations. Each host constructs a KgTestOps value
/// by closing over its runtime instance; the shared implementations below
/// operate only on this record.
type KgTestOps =
    { createTestPorts: unit -> KnowledgeGraphRuntimeTestPorts
      registerJob: string * KnowledgeGraphJobContext -> unit
      hasJob: string -> bool
      mapLaunch: BookkeeperLaunch -> obj }

let private takeLaunchesFromPorts (ports: KnowledgeGraphRuntimeTestPorts) : BookkeeperLaunch list =
    unbox (ports.SwapState(fun s ->
        let launches, next = drainLaunches s
        next, box launches))

let private waitJobsOnPorts (ports: KnowledgeGraphRuntimeTestPorts) : JS.Promise<unit> =
    promise {
        do! ports.RunOnCommandQueue(fun () -> Promise.lift ())
        do! ports.AwaitBackgroundSinkJobs()
    }

let registerTestJob (ops: KgTestOps) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let readField fieldName = Dyn.str payload fieldName
    let ctx = buildTestingJobContext workspaceRoot kindTag readField
    ops.registerJob (sessionID, ctx)

let takeTestLaunches (ops: KgTestOps) : obj array =
    let ports = ops.createTestPorts ()
    takeLaunchesFromPorts ports
    |> List.map ops.mapLaunch
    |> List.toArray

let waitTestJobs (ops: KgTestOps) : JS.Promise<unit> =
    let ports = ops.createTestPorts ()
    waitJobsOnPorts ports

let hasTestJob (ops: KgTestOps) (sessionID: string) : bool =
    ops.hasJob sessionID
