namespace Wanxiangshu.Next.Tests.Integration

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Orchestrator
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module OrchestratorRecoveryFixtures =
    module GitTestRepo =
        [<Import("execFileSync", "node:child_process")>]
        let execFileSync (file: string, args: string[], opts: obj) : string = jsNative

        [<Import("mkdirSync", "node:fs")>]
        let mkdirSync (path: string, opts: obj) : unit = jsNative

        [<Import("join", "node:path")>]
        let pathJoin (a: string, b: string) : string = jsNative

        /// Creates a real git repo with two empty commits on branch `main`.
        /// Returns (firstCommitSha, secondCommitSha); the first is a proper ancestor
        /// of the second.
        let createTwoCommitRepo (parentDir: string) : string * string * string =
            let dir = pathJoin (parentDir, "repo")
            mkdirSync (dir, {| recursive = true |})

            let git (args: string list) =
                execFileSync ("git", List.toArray args, {| cwd = dir; encoding = "utf8" |})
                |> fun s -> s.Trim()

            git [ "init"; "-b"; "main" ] |> ignore
            git [ "config"; "user.email"; "test@example.com" ] |> ignore
            git [ "config"; "user.name"; "test" ] |> ignore
            git [ "commit"; "--allow-empty"; "-m"; "candidate" ] |> ignore
            let candidate = git [ "rev-parse"; "HEAD" ]
            git [ "commit"; "--allow-empty"; "-m"; "advance target" ] |> ignore
            let targetHead = git [ "rev-parse"; "HEAD" ]
            (dir, candidate, targetHead)

    let makePort (facts: AgentFact list) =
        let mutable projection = AgentProjection.empty

        for fact in facts do
            projection <- Fold.foldAgentFact projection fact

        let recorded = ResizeArray<AgentFact>()

        let port =
            { AppendFact =
                fun _ fact ->
                    recorded.Add fact
                    projection <- Fold.foldAgentFact projection fact

                    Ok
                        { AgentProjections = projection
                          RuntimeId = None }
              Snapshot =
                fun () ->
                    { AgentProjections = projection
                      RuntimeId = None } }

        port, recorded

    let managerJob managerId worktree prompt =
        AgentFact.OrchestratorManagerJobCreated
            {| ManagerId = managerId
               WorktreePath = worktree
               Branch = sprintf "manager/%s" managerId
               Prompt = prompt |}

    let authority worktreeHead targetHead : GitAuthorityPort =
        { GetHead = fun _ -> Task.FromResult(Ok worktreeHead)
          GetTargetHead = fun _ _ -> Task.FromResult(Ok targetHead) }

    let gitPort (rebaseCalls: int ref) (reverifyHead: string) : GitPort =
        { IsDirty = fun _ -> Task.FromResult false
          CreateWorktree = fun _ _ _ -> Task.FromResult(Ok())
          Rebase =
            fun _ _ ->
                rebaseCalls.Value <- rebaseCalls.Value + 1
                Task.FromResult(Ok())
          FfMerge = fun _ _ _ -> Task.FromResult(Ok reverifyHead)
          ConflictedFiles = fun _ -> Task.FromResult(Ok [])
          RemoveWorktree = fun _ -> Task.FromResult(Ok())
          HasRebaseHead = fun _ -> Task.FromResult false
          ListWorktrees = fun () -> Task.FromResult(Ok [])
          ListManagerBranches = fun () -> Task.FromResult(Ok [])
          DeleteBranch = fun _ -> Task.FromResult(Ok())
          ReadHead = fun _ -> Task.FromResult(Ok reverifyHead)
          GetTargetHead = fun _ -> Task.FromResult(Ok reverifyHead) }

    let runRecovery facts git authorityPort managerId worktree prompt =
        task {
            let journal, recorded = makePort facts

            let manager: ManagerPort =
                { RunManager = fun _ _ _ -> Task.FromResult(Ok())
                  Reverify = fun _ _ _ -> Task.FromResult(Ok()) }

            let orch =
                Orchestrator(git, manager, "/repo", "main", ?journal = Some journal, ?authority = Some authorityPort)

            orch.RecoverManagerJob(managerId, worktree, prompt, true)
            let! verdict = orch.JoinPublished()
            return verdict, recorded
        }
