module Wanxiangshu.Tests.KnowledgeGraphMaintenanceRunTests

open System
open Fable.Core
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Kernel.KnowledgeGraph
open Wanxiangshu.Kernel.KnowledgeGraph.Types
open Wanxiangshu.Kernel.KnowledgeGraph.Maintenance
open Wanxiangshu.Kernel.KnowledgeGraph.RuntimeState
open Wanxiangshu.Shell.KnowledgeGraphFiles
open Wanxiangshu.Shell.KnowledgeGraphMaintenanceRun
open Wanxiangshu.Shell.PromiseQueue

let runMaintenanceIfDueInvokesTryLaunchOnce () = promise {
    let! workspaceDir = mkdtempAsync "kg-maint-run-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [
        knowledgeGraphEntry "0a3f" ["e"] "f" ]
    let mutable state = initialKnowledgeGraphState
    let mutable tryLaunchCount = 0
    let queue = SerialQueue()
    let host =
        { WorkspaceRoot = workspaceDir
          GetState = fun () -> state
          SetState = fun s -> state <- s
          Now = fun () -> DateTime(2026, 6, 20)
          TryLaunch = fun _ _ _ -> tryLaunchCount <- tryLaunchCount + 1 }
    do! runMaintenanceIfDue queue host
    check "runMaintenanceIfDue calls TryLaunch once" (tryLaunchCount = 1)
    let key, _ = bookkeeperMaintenanceLaunch workspaceDir "2026-06-18"
    check "state records maintenance dedup key" (Set.contains key state.scheduledMaintenance)
    do! rmAsync workspaceDir
}

let runMaintenanceIfDueSkipsSecondLaunchWhenAlreadyScheduled () = promise {
    let! workspaceDir = mkdtempAsync "kg-maint-dedup-"
    do! ensureKnowledgeGraphDir workspaceDir
    do! writeKnowledgeGraphFileAsync (dayPath workspaceDir "2026-06-18") (DayHeader("2026-06-18", false)) [
        knowledgeGraphEntry "0a3f" ["e"] "f" ]
    let key, _ = bookkeeperMaintenanceLaunch workspaceDir "2026-06-18"
    let mutable state =
        { initialKnowledgeGraphState with scheduledMaintenance = Set.singleton key }
    let mutable tryLaunchCount = 0
    let queue = SerialQueue()
    let host =
        { WorkspaceRoot = workspaceDir
          GetState = fun () -> state
          SetState = fun s -> state <- s
          Now = fun () -> DateTime(2026, 6, 20)
          TryLaunch = fun _ _ _ -> tryLaunchCount <- tryLaunchCount + 1 }
    do! runMaintenanceIfDue queue host
    check "already scheduled key skips TryLaunch" (tryLaunchCount = 0)
    do! rmAsync workspaceDir
}

let runMaintenanceIfDueSkipsWhenNoKgDir () = promise {
    let! workspaceDir = mkdtempAsync "kg-maint-skip-"
    let mutable tryLaunchCount = 0
    let queue = SerialQueue()
    let host =
        { WorkspaceRoot = workspaceDir
          GetState = fun () -> initialKnowledgeGraphState
          SetState = fun _ -> ()
          Now = fun () -> DateTime.UtcNow
          TryLaunch = fun _ _ _ -> tryLaunchCount <- tryLaunchCount + 1 }
    do! runMaintenanceIfDue queue host
    check "no kg dir skips TryLaunch" (tryLaunchCount = 0)
    do! rmAsync workspaceDir
}

let run () = promise {
    do! runMaintenanceIfDueInvokesTryLaunchOnce ()
    do! runMaintenanceIfDueSkipsSecondLaunchWhenAlreadyScheduled ()
    do! runMaintenanceIfDueSkipsWhenNoKgDir ()
}