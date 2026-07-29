namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// SessionDead gate: 0.5.0 fallback never kills on retry count. Kept to prove
/// Fork/Reuse still work after many fallback advances, and BaseModel inheritance.
module HostForkRuntimeSessionDeadTests =

    let private noopDisposable () =
        { new IDisposable with
            member _.Dispose() = () }

    let private makeCountingFake () =
        let mutable childPrompt = 0
        let mutable prompt = 0
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, _) = noopDisposable ()

                member _.SendPrompt(_, _, _) =
                    prompt <- prompt + 1
                    Task.FromResult(Ok(MessageId.create "x"))

                member _.SendChildPromptFireAndForget(_, _, _, _) =
                    childPrompt <- childPrompt + 1
                    Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task
                member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                member _.GetSessionOutput(_) = [] }

        host, (fun () -> childPrompt), (fun () -> prompt)

    let private capturingFake () =
        let captured = ResizeArray<OpenCodePromptOptions>()
        let childId = SessionId.create "child-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, _) = noopDisposable ()

                member _.SendPrompt(_, _, o) =
                    captured.Add o
                    Task.FromResult(Ok(MessageId.create "x"))

                member _.SendChildPromptFireAndForget(_, _, _, o) =
                    captured.Add o
                    Task.FromResult(Ok())

                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task
                member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                member _.GetSessionOutput(_) = [] }

        host, captured

    let private link (journal: AgentJournal) (parentId: SessionId) (childId: SessionId) (agentId: string) =
        AgentJournal.appendAgent
            (StreamId.Session parentId)
            None
            (AgentFact.AgentForked
                {| ParentId = parentId
                   ChildId = ChildId.create (SessionId.value childId)
                   TargetAgent = agentId
                   Role = Some "fast-coder" |})
            journal
        |> ignore

    let private fail (journal: AgentJournal) (childId: SessionId) (n: int) =
        for i in 1..n do
            AgentJournal.appendAgent
                (StreamId.Session childId)
                None
                (AgentFact.FallbackCursorAdvanced
                    {| SessionId = childId
                       LogicalRunId = "run-test"
                       AuthorityRootUserMessageId = "root-test"
                       Reason = sprintf "f%d" i
                       AssistantMessageId = sprintf "m%d" i
                       ProviderAttempt = sprintf "pa%d" i |})
                journal
            |> ignore

    [<Fact>]
    let ``Fork_and_Reuse_still_ok_after_four_fallback_advances`` () =
        withTempDir (fun d ->
            task {
                let p = SessionId.create "p-dead"
                let c = SessionId.create "child-1"
                let a = "a-dead"

                use j = AgentJournal.create d (RuntimeId.create "r-dead") 1 DateTimeOffset.UtcNow
                link j p c a
                fail j c 4

                let host, cc, pc = makeCountingFake ()
                let b = HostForkRuntime(p, host, journal = j)
                let! f = b.Fork(a, AgentRole.Coder, "w")
                let! r = b.Reuse(a, "w")

                Assert.True(Result.isOk f, sprintf "expected Fork Ok after 4 advances, got %A" f)
                Assert.True(Result.isOk r, sprintf "expected Reuse Ok after 4 advances, got %A" r)
                Assert.True(cc () + pc () >= 1, "expected at least one prompt send")
            })

    [<Fact>]
    let ``Fork_new_AgentOwnerRoot_sends_Agent_with_Model_None`` () =
        withTempDir (fun d ->
            task {
                let p = SessionId.create "p-3"
                let c = SessionId.create "child-1"
                let a = "a-3"

                use j = AgentJournal.create d (RuntimeId.create "r-3") 1 DateTimeOffset.UtcNow
                link j p c a
                // Prior fallback advances must not inject Model into a new AgentOwnerRoot.
                fail j c 3

                let host, captured = capturingFake ()
                let b = HostForkRuntime(p, host, journal = j)
                let! r = b.Fork(a, AgentRole.Coder, "w")

                Assert.True(Result.isOk r, sprintf "expected Ok, got %A" r)
                Assert.True(captured.[0].Model.IsNone)
                Assert.True(captured.[0].Agent.IsSome)
            })

    [<Fact>]
    let ``Four_advances_survive_journal_rebuild_without_dead_refusal`` () =
        withTempDir (fun d ->
            task {
                let p = SessionId.create "p-reb"
                let a = "a-reb"
                let c = SessionId.create "child-1"

                use j1 = AgentJournal.create d (RuntimeId.create "r-reb1") 1 DateTimeOffset.UtcNow
                let host1, _, _ = makeCountingFake ()
                let! first = (HostForkRuntime(p, host1, journal = j1)).Fork(a, AgentRole.Coder, "w")
                Assert.True(Result.isOk first, sprintf "expected first Fork Ok, got %A" first)

                fail j1 c 4
                (j1 :> IDisposable).Dispose()

                use j2 =
                    AgentJournal.createFromBoot d (RuntimeId.create "r-reb2") 2 DateTimeOffset.UtcNow (Boot.boot d)

                let host2, _, _ = makeCountingFake ()
                let! r = (HostForkRuntime(p, host2, journal = j2)).Fork(a, AgentRole.Coder, "w")

                Assert.True(Result.isOk r, sprintf "expected Fork Ok after rebuild, got %A" r)
            })
