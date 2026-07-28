namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal

module private NodeFsTestSupport =
    [<Import("existsSync", "node:fs")>]
    let existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let mkdirSync (path: string, opts: obj) : unit = jsNative

    [<Import("rmSync", "node:fs")>]
    let rmSync (path: string, opts: obj) : unit = jsNative

    [<Import("tmpdir", "node:os")>]
    let tmpdir () : string = jsNative

    [<Import("join", "node:path")>]
    let pathJoin (a: string, b: string) : string = jsNative

module JournalTestSupport =

    let registerAuthorityRoot (journal: AgentJournal) sessionId agent =
        let selected, peer, role, tier =
            match Wanxiangshu.Next.OpenCode.ManagedAgent.tryParse agent with
            | Some managed ->
                managed.Name,
                (Wanxiangshu.Next.OpenCode.ManagedAgent.peer managed).Name,
                Wanxiangshu.Next.OpenCode.PromptAuthority.roleLabel managed.Role,
                Wanxiangshu.Next.OpenCode.PromptAuthority.tierLabel managed.Tier
            | None ->
                // Bare canonical role fallback for older fixtures that still pass
                // unprefixed names into journal helpers during migration.
                // Map to the fast managed agent by default; derive the peer.
                match Wanxiangshu.Next.OpenCode.PromptAuthority.tryParseRole agent with
                | Some canonicalRole ->
                    let fast =
                        Wanxiangshu.Next.OpenCode.ManagedAgent.nameOf
                            Wanxiangshu.Next.Kernel.AgentTier.Fast
                            canonicalRole

                    let deep =
                        Wanxiangshu.Next.OpenCode.ManagedAgent.nameOf
                            Wanxiangshu.Next.Kernel.AgentTier.Deep
                            canonicalRole

                    fast, deep, Wanxiangshu.Next.OpenCode.PromptAuthority.roleLabel canonicalRole, "Fast"
                | None ->
                    let peer =
                        if agent.StartsWith("fast-") then
                            "deep-" + agent.Substring(5)
                        elif agent.StartsWith("deep-") then
                            "fast-" + agent.Substring(5)
                        else
                            agent

                    let tier = if agent.StartsWith("deep-") then "Deep" else "Fast"

                    agent, peer, agent, tier

        AgentJournal.appendAgent
            (StreamId.Session(SessionId.create sessionId))
            (Some(TurnId.ofMessageId (MessageId.create "u1")))
            (AgentFact.AuthorityRootAccepted
                {| SessionId = SessionId.create sessionId
                   LogicalRunId = "run-" + sessionId
                   HostMessageId = "u1"
                   AuthorityKind = "HumanRoot"
                   SelectedAgent = selected
                   PeerAgent = peer
                   CanonicalRole = role
                   SelectedTier = tier |})
            journal
        |> ignore

    let withTempDir (action: string -> Task<unit>) : Task<unit> =
        task {
            let dir =
                NodeFsTestSupport.pathJoin (
                    NodeFsTestSupport.tmpdir (),
                    "wanxiangshu_test_" + Guid.NewGuid().ToString("N")
                )

            try
                NodeFsTestSupport.mkdirSync (dir, {| recursive = true |}) |> ignore
                do! action dir
            finally
                try
                    if NodeFsTestSupport.existsSync dir then
                        NodeFsTestSupport.rmSync (dir, {| recursive = true; force = true |}) |> ignore
                with _ ->
                    ()
        }
