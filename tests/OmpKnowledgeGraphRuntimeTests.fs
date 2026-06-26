module Wanxiangshu.Tests.OmpKnowledgeGraphRuntimeTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Omp.KnowledgeGraph.Runtime

/// When the kg directory does not exist, Submit must reject without touching
/// the writeQueues map. This is the cheap guard that prevents queue allocations
/// for sessions that never opted into the knowledge graph.
let submitRejectsWhenKgDirMissing () =
    let runtime = OmpKnowledgeGraphRuntime (createObj [])
    promise {
        let! msg = runtime.Submit("session-1", "/no/such/dir", [])
        check "no kg dir message returned" (msg.Contains "Knowledge graph directory not found.")
    }

/// Two distinct sessions registered under two distinct workspace roots must
/// each be addressable independently. With a single shared queue the routing
/// key was sessionId only, so the second registration would have collided.
/// The snapshot must list both with their original roots.
let submitRoutesByWorkspaceRoot () =
    let runtime = OmpKnowledgeGraphRuntime (createObj [])
    runtime.RegisterJobForTesting("child-a", "/tmp/omp-kg-route-a", "append", createObj [])
    runtime.RegisterJobForTesting("child-b", "/tmp/omp-kg-route-b", "append", createObj [])
    let snap = runtime.SnapshotRegisteredJobsForTesting()
    check "two distinct jobs snapshot" (snap.Length = 2)
    let bySid = snap |> Array.toList |> Map.ofList
    check "child-a registered" (Map.containsKey "child-a" bySid)
    check "child-b registered" (Map.containsKey "child-b" bySid)
    check "child-a workspace root preserved" (bySid.["child-a"] = "/tmp/omp-kg-route-a")
    check "child-b workspace root preserved" (bySid.["child-b"] = "/tmp/omp-kg-route-b")

/// Two registrations against the same workspace root must NOT collapse: each
/// session keeps its own entry. This is what per-workspace queues are about —
/// serialise writes inside one workspace, never across them.
let submitKeepsTwoSessionsPerRootDistinct () =
    let runtime = OmpKnowledgeGraphRuntime (createObj [])
    runtime.RegisterJobForTesting("a", "/shared-root", "append", createObj [])
    runtime.RegisterJobForTesting("b", "/shared-root", "append", createObj [])
    let snap = runtime.SnapshotRegisteredJobsForTesting()
    let hasA = snap |> Array.exists (fun (sid, _) -> sid = "a")
    let hasB = snap |> Array.exists (fun (sid, _) -> sid = "b")
    check "two entries survive per-root" (snap.Length = 2 && hasA && hasB)

/// Bookkeeper launches must drain into the testing sink so callers can assert
/// that the dedup key kept the same job from being launched twice for the
/// same workspace.
let takeBookkeeperLaunchesForTestingStartsEmpty () =
    let runtime = OmpKnowledgeGraphRuntime (createObj [])
    let launches = runtime.TakeBookkeeperLaunchesForTesting()
    check "no launches on fresh runtime" (launches.Length = 0)

/// When the workspace root is whitespace or empty, StartMaintenanceIfDue must
/// noop instead of allocating a queue slot or scheduling a job.
let startMaintenanceIfDueNoopsForBlankRoot () =
    let runtime = OmpKnowledgeGraphRuntime (createObj [])
    promise {
        do! runtime.StartMaintenanceIfDue "   "
        let launches = runtime.TakeBookkeeperLaunchesForTesting()
        check "blank root does not enqueue" (launches.Length = 0)
    }