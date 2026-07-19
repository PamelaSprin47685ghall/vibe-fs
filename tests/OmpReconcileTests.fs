module Wanxiangshu.Tests.OmpReconcileTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Tests.TestWorkspace

module Dyn = Wanxiangshu.Runtime.Dyn

/// Verify that when reconcileUnfinishedRuns runs on startup, it constructs a real hostFactory
/// and triggers physical close (sessionDelete) on zombie sessions.
let ompReconciliationClosesZombieSessions () =
    promise {
        let! root = mkdtempAsync "omp-reconcile-test-"
        let sid = SessionId.create "omp-zombie-sid-1"
        let parent = SessionId.create "parent-sid"
        let rid = RunId.create "run-zombie-1"

        let eventStore = Wanxiangshu.Runtime.SubsessionEventStore.create root

        let runStartedData =
            { SessionId = sid
              ParentSessionId = parent
              RunId = rid }

        do! eventStore.Append(sid, [ RunStarted runStartedData ])

        let mutable sessionDeleteCalled = false
        let mutable calledSessionId = ""

        let sessionApi =
            createObj
                [ "sessionDelete",
                  box (fun (arg: obj) ->
                      sessionDeleteCalled <- true
                      calledSessionId <- Dyn.str arg "sessionId"
                      Promise.lift (box null)) ]

        let pi = createObj [ "directory", box root; "session", box sessionApi ]

        let hostFactory (_sid: string) =
            Wanxiangshu.Hosts.Omp.SubsessionHostAdapter.createHost null "" pi root

        let! _ = Wanxiangshu.Runtime.SubsessionReconcile.reconcileUnfinishedRuns root (Some hostFactory)

        check "sessionDelete was called" sessionDeleteCalled
        check "sessionDelete called with correct sessionId" (calledSessionId = "omp-zombie-sid-1")

        let actor =
            Wanxiangshu.Runtime.SubsessionActorRegistry.SubsessionActorRegistry.GetOrCreate
                root
                "omp-zombie-sid-1"
                (hostFactory "omp-zombie-sid-1")
                eventStore

        check "actor state is poisoned after reconcile" actor.IsPoisoned
    }
