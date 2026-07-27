namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Orchestrator

module OrchestratorHostTests =

    let private mkSid s = SessionId.create s

    /// Worktree-scoped manager idle arrives via /global/event as
    /// { directory, payload: { type, properties } }. HostEventPort must see it.
    [<Fact>]
    let ``HostSignalSubscribe unwraps global SSE payload into idle signal`` () =
        let captured = ResizeArray<HostSignal>()
        let owned = System.Collections.Generic.HashSet<string>()
        let router = HostSignalRouter(owned, fun s -> captured.Add s)
        router.RegisterOwned(SessionId.create "mgr-child")

        let globalSse =
            createObj
                [ "directory", box "/tmp/wanxiangshu-mgr"
                  "payload",
                  box (
                      createObj
                          [ "type", box "session.status"
                            "properties",
                            box (
                                createObj
                                    [ "sessionID", box "mgr-child"
                                      "status", box (createObj [ "type", box "idle" ]) ]
                            ) ]
                  ) ]

        // HostSignalSubscribe.unwrap extracts the inner payload before the
        // adapter sees it. Drive the router with that unwrapped payload and
        // assert the resulting idle signal.
        router.Observe(globalSse?payload)
        Assert.True(captured.Count = 1)

        match captured.[0] with
        | SessionIdle sid -> Assert.Equal("mgr-child", SessionId.value sid)
        | other -> Assert.True(false, sprintf "unexpected %A" other)


    [<Fact>]
    let ``OrchestratorHost fork fails on non-repo without creating child`` () =
        task {
            let log = OrchestratorHostTestSupport.createLog ()
            let port = OrchestratorHostTestSupport.FakeSessionPort(log)
            let created = ResizeArray<string * string * string>()

            let deps =
                { Sessions = port :> ISessionHostPort
                  Journal = None
                  ModelConfig = None
                  OnChildCreated =
                    fun agentId role childId -> created.Add(agentId, role.ToString(), SessionId.value childId)
                  RegisterChildDirectory = fun _ _ -> ()
                  RegisterReviewerTree = fun _ _ -> ()
                  OnRunStarted = (fun _ _ _ -> ())
                  RepoPath = "/nonexistent-path-xyz"
                  TargetBranch = "" }

            let host = OrchestratorHost(deps, mkSid "orch-1")
            let! result = host.ForkManagerJob("m1", "task")

            match result with
            | Ok _ -> failwith "ForkManagerJob should fail on a non-existent repo path"
            | Error _ ->
                if log.CreateChild.Length <> 0 then
                    failwithf
                        "CreateChildSession must not be called when worktree fails, got %d calls"
                        log.CreateChild.Length
        }

    [<Fact>]
    let ``RecoverManagerJob reuses durable identity prompt and checkpoint`` () =
        task {
            let calls = ResizeArray<string * string * string>()

            let git: GitPort =
                { IsDirty = fun _ -> Task.FromResult false
                  CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
                  Rebase = fun _ _ -> Task.FromResult(Ok())
                  FfMerge = fun _ _ _ -> Task.FromResult(Ok "commit")
                  ConflictedFiles = fun _ -> Task.FromResult(Ok [])
                  RemoveWorktree = fun _ -> Task.FromResult(Ok())
                  HasRebaseHead = fun _ -> Task.FromResult false
                  ListWorktrees = fun () -> Task.FromResult(Ok [])
                  ListManagerBranches = fun () -> Task.FromResult(Ok [])
                  DeleteBranch = fun _ -> Task.FromResult(Ok()) }

            let manager: ManagerPort =
                { RunManager =
                    fun managerId worktree prompt ->
                        calls.Add(managerId, worktree, prompt)
                        Task.FromResult(Ok())
                  Reverify = fun _ _ _ -> Task.FromResult(Ok()) }

            let resumed = Orchestrator(git, manager, "/repo", "main")
            resumed.RecoverManagerJob("m1", "/worktree/m1", "saved prompt", false)
            let! resumedVerdict = resumed.JoinPublished()

            Assert.True(
                match resumedVerdict with
                | OrchestratorVerdict.Published _ -> true
                | _ -> false
            )

            Assert.Equal<(string * string * string) list>(
                [ ("m1", "/worktree/m1", "saved prompt") ],
                calls |> Seq.toList
            )

            let checkpointed = Orchestrator(git, manager, "/repo", "main")
            checkpointed.RecoverManagerJob("m2", "/worktree/m2", "saved candidate", true)
            let! checkpointedVerdict = checkpointed.JoinPublished()

            Assert.True(
                match checkpointedVerdict with
                | OrchestratorVerdict.Published _ -> true
                | _ -> false
            )

            Assert.Equal(1, calls.Count)
        }

    [<Fact>]
    let ``sweep removes stale manager worktree and branch but keeps active`` () =
        task {
            let activeId = "active"
            let activePath = "/wt/active"
            let stalePath = "/wt/stale"

            let activeJob: ManagerJob =
                { WorktreePath = activePath
                  Branch = "manager/active"
                  CandidateId = None
                  CandidateCommit = None
                  PublishedCommit = None
                  Prompt = ""
                  PreRebaseReviewCommit = None
                  RebasedCommit = None
                  ConflictFiles = None
                  PostRebaseReviewCommit = None
                  PublishClaimHead = None }

            let activeJobs = Map.ofList [ (ManagerId.create activeId, activeJob) ]
            let removedWorktrees = ResizeArray<string>()
            let deletedBranches = ResizeArray<string>()

            let git: GitPort =
                { IsDirty = fun _ -> Task.FromResult false
                  CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
                  Rebase = fun _ _ -> Task.FromResult(Ok())
                  FfMerge = fun _ _ _ -> Task.FromResult(Ok "x")
                  ConflictedFiles = fun _ -> Task.FromResult(Ok [])
                  RemoveWorktree =
                    fun p ->
                        removedWorktrees.Add p
                        Task.FromResult(Ok())
                  HasRebaseHead = fun _ -> Task.FromResult false
                  ListWorktrees =
                    fun () ->
                        Task.FromResult(
                            Ok
                                [ (activePath, Some "refs/heads/manager/active")
                                  (stalePath, Some "refs/heads/manager/stale") ]
                        )
                  ListManagerBranches = fun () -> Task.FromResult(Ok [ "manager/active"; "manager/stale" ])
                  DeleteBranch =
                    fun b ->
                        deletedBranches.Add b
                        Task.FromResult(Ok()) }

            do! OrchestratorSweep.sweepStaleArtifacts git activeJobs
            Assert.True(removedWorktrees.Contains stalePath, "stale worktree not removed")
            Assert.False(removedWorktrees.Contains activePath, "active worktree removed")
            Assert.True(deletedBranches.Contains "manager/stale", "stale branch not removed")
            Assert.False(deletedBranches.Contains "manager/active", "active branch removed")
        }

    [<Fact>]
    let ``SpikePlugin_initSpikePlugin_exposes_hooks_and_ports`` () =
        task {
            let input =
                createObj
                    [ "events", box (createObj [ "listen", box (fun () -> box (fun () -> ())) ]) ]

            let! hooksObj = SpikePlugin.initSpikePlugin input
            Assert.False(isNull hooksObj)
            Assert.False(isNull hooksObj?projection)
            Assert.False(isNull hooksObj?events)
            Assert.False(isNull hooksObj?sessions)
            Assert.False(isNull hooksObj?``chat.transform``)
        }
