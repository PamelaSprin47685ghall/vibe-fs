module Wanxiangshu.Shell.KnowledgeGraphTestHooks

open Fable.Core
open Wanxiangshu.Kernel.KnowledgeGraph.JobTesting
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.KnowledgeGraphRuntimeTestPorts

/// Host-specific callbacks that adapter wrappers close over the runtime instance.
type KgTestOps =
    { createTestPorts: unit -> KnowledgeGraphRuntimeTestPorts
      registerJob: string * KnowledgeGraphJobContext -> unit
      takeLaunches: KnowledgeGraphRuntimeTestPorts -> BookkeeperLaunch list
      waitJobs: KnowledgeGraphRuntimeTestPorts -> JS.Promise<unit>
      hasJob: string -> bool
      mapLaunch: BookkeeperLaunch -> obj }

let registerTestJob (ops: KgTestOps) (sessionID: string) (workspaceRoot: string) (kindTag: string) (payload: obj) : unit =
    let readField fieldName = str payload fieldName
    let ctx = buildTestingJobContext workspaceRoot kindTag readField
    ops.registerJob (sessionID, ctx)

let takeTestLaunches (ops: KgTestOps) : obj array =
    let ports = ops.createTestPorts ()
    let launches = ops.takeLaunches ports
    launches |> List.map ops.mapLaunch |> List.toArray

let waitTestJobs (ops: KgTestOps) : JS.Promise<unit> =
    let ports = ops.createTestPorts ()
    ops.waitJobs ports

let hasTestJob (ops: KgTestOps) (sessionID: string) : bool =
    ops.hasJob sessionID
