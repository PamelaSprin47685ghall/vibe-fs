namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module OrchestratorHostTests =

    let private mkSid s = SessionId.create s

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
                  RegisterReviewerTree = fun _ _ -> ()
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
