namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Threading.Tasks
open Fable.Core.JsInterop
open Xunit
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module OrchestratorHostTests =

    let private mkSid s = SessionId.create s

    /// Worktree-scoped manager idle arrives via /global/event as
    /// { directory, payload: { type, properties } }. HostEventPort must see it.
    [<Fact>]
    let ``normalizeHostEvent unwraps global SSE payload for idle`` () =
        let eventPort = Events.HostEventPort()
        let mutable observed = []

        use _sub =
            (eventPort :> IEventObservationPort)
                .SubscribeTerminalListener(fun sessionId outcome ->
                    observed <- (SessionId.value sessionId, outcome) :: observed)

        HostEventSubscribe.observe
            eventPort
            (createObj
                [ "directory", box "/tmp/wanxiangshu-mgr"
                  "payload",
                  box (
                      createObj
                          [ "type", box "session.idle"
                            "properties", createObj [ "sessionID", box "mgr-child" ] ]
                  ) ])

        match observed with
        | [ (sessionId, Completed _) ] when sessionId = "mgr-child" -> ()
        | other -> failwithf "expected completed mgr-child, got %A" other

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

    [<Fact>]
    let ``SpikePlugin_initSpikePlugin_exposes_hooks_and_ports`` () =
        task {
            let input = createObj []
            let! hooksObj = SpikePlugin.initSpikePlugin input
            Assert.False(isNull hooksObj)
            Assert.False(isNull hooksObj?projection)
            Assert.False(isNull hooksObj?events)
            Assert.False(isNull hooksObj?sessions)
            Assert.False(isNull hooksObj?``chat.transform``)
        }
