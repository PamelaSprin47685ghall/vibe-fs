namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tools
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.JournalTests
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module CompanionResetTests =
    let private bloggerModel =
        { providerID = "cheap-provider"
          modelID = "cheap-blogger"
          variant = Some "fast" }

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let mutable sentModel: OpencodeModel option = None
        let mutable output = [ "history" ]
        let mutable prompts: string list = []
        let mutable sendError = false
        let mutable terminalOutcome: TerminalOutcome = Completed(MessageId.create "blog")
        let mutable growOutput = true
        let childId = SessionId.create "blogger-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, prompt, options) =
                    prompts <- prompts @ [ prompt ]
                    sentModel <- options.Model

                    if sendError then
                        Task.FromResult(Error "send failed")
                    else
                        if growOutput then
                            output <- output @ [ "blog paragraph" ]

                        terminal |> Option.iter (fun listener -> listener childId terminalOutcome)
                        Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = output }

        host,
        (fun () -> childCount),
        (fun () -> sentModel),
        (fun () -> prompts),
        (fun v -> sendError <- v),
        (fun v -> terminalOutcome <- v),
        (fun v -> growOutput <- v)

    [<Fact>]
    let ``reset send failure keeps flag and reanchors after success`` () =
        withTempDir (fun directory ->
            task {
                let primaryId = SessionId.create "reset-fail-primary"

                let journal =
                    AgentJournal.create directory (RuntimeId.create "reset-runtime") 1001 DateTimeOffset.UtcNow

                let durable = AgentJournalCompanionPort(journal) :> ICompanionDurablePort
                let seedHost, _, _, _, _, _, _ = makeFake ()

                let seed =
                    new CompanionHost(primaryId, seedHost, durable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, seed.SubmitProjection("{\"seed\":1}"))
                do! seed.WaitInFlightAsync()
                (journal :> IDisposable).Dispose()
                let boot = Boot.boot directory

                let restoredJournal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "restored-runtime")
                        1002
                        DateTimeOffset.UtcNow
                        boot

                let restoredDurable =
                    AgentJournalCompanionPort(restoredJournal) :> ICompanionDurablePort

                let host, _, _, prompts, setSendError, _, _ = makeFake ()
                setSendError true

                let restored =
                    new CompanionHost(primaryId, host, restoredDurable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, restored.SubmitProjection("{\"reset\":2}"))
                do! restored.WaitInFlightAsync()
                Assert.Equal(Some "{\"seed\":1}", restored.Memory.LastSuccessfulProjection)
                Assert.Contains("FULL PROJECTION", prompts () |> List.head)
                setSendError false
                Assert.Equal(Submitted, restored.SubmitProjection("{\"reset\":3}"))
                do! restored.WaitInFlightAsync()
                Assert.Contains("FULL PROJECTION", prompts () |> List.item 1)
                Assert.Contains("{\"reset\":3}", prompts () |> List.item 1)
                Assert.Equal(Some "{\"reset\":3}", restored.Memory.LastSuccessfulProjection)
                (restoredJournal :> IDisposable).Dispose()
            })

    [<Fact>]
    let ``reset abort and empty output keep pending reset`` () =
        withTempDir (fun directory ->
            task {
                let primaryId = SessionId.create "reset-boundary-primary"

                let journal =
                    AgentJournal.create directory (RuntimeId.create "reset-boundary") 1001 DateTimeOffset.UtcNow

                let durable = AgentJournalCompanionPort(journal) :> ICompanionDurablePort
                let host1, _, _, _, _, _, _ = makeFake ()

                let seed =
                    new CompanionHost(primaryId, host1, durable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, seed.SubmitProjection("{\"seed\":1}"))
                do! seed.WaitInFlightAsync()
                (journal :> IDisposable).Dispose()
                let boot = Boot.boot directory

                let reopened =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "reset-boundary-reopen")
                        1002
                        DateTimeOffset.UtcNow
                        boot

                let durable2 = AgentJournalCompanionPort(reopened) :> ICompanionDurablePort
                let host2, _, _, prompts, _, setTerminal, setGrowOutput = makeFake ()
                setTerminal (Aborted "cancelled")

                let restored =
                    new CompanionHost(primaryId, host2, durable2, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, restored.SubmitProjection("{\"abort\":2}"))
                do! restored.WaitInFlightAsync()
                Assert.Contains("FULL PROJECTION", prompts () |> List.head)

                setTerminal (Completed(MessageId.create "empty"))
                setGrowOutput false
                Assert.Equal(Submitted, restored.SubmitProjection("{\"empty\":3}"))
                do! restored.WaitInFlightAsync()
                Assert.Equal(Some "{\"seed\":1}", restored.Memory.LastSuccessfulProjection)

                setGrowOutput true
                Assert.Equal(Submitted, restored.SubmitProjection("{\"success\":4}"))
                do! restored.WaitInFlightAsync()
                Assert.Equal(Some "{\"success\":4}", restored.Memory.LastSuccessfulProjection)
                (reopened :> IDisposable).Dispose()
            })

    [<Fact>]
    let ``self rebase uses independent blogger budget`` () =
        Assert.True(CompanionTransform.bloggerSelfRebaseDue 32000 (String.replicate 102400 "x"))
        Assert.False(CompanionTransform.bloggerSelfRebaseDue 32000 (String.replicate 100000 "x"))
        Assert.False(CompanionTransform.bloggerSelfRebaseDue 0 (String.replicate 200000 "x"))
