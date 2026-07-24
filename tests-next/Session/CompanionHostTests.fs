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

module CompanionHostTests =

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let mutable output = [ "history" ]
        let childId = SessionId.create "blogger-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, _) =
                    output <- output @ [ "blog paragraph" ]

                    terminal
                    |> Option.iter (fun listener -> listener childId (Completed(MessageId.create "blog")))

                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                member _.AbortSession(_) = Task.FromResult(Ok())

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = output }

        host, (fun () -> childCount)

    [<Fact>]
    let ``CompanionHost_updates_B_and_reuses_blogger`` () =
        task {
            let host, childCount = makeFake ()
            let companion = CompanionHost(SessionId.create "primary", host)

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "blog paragraph", companion.Memory.CurrentB)

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":2}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(1, childCount ())
            Assert.True(companion.EnablePrefixReplacement())

            let messages =
                [ { Role = "user"
                    Text = "old"
                    ToolCalls = None
                    Metadata = None }
                  { Role = "user"
                    Text = "tail"
                    ToolCalls = None
                    Metadata = None } ]

            let replaced = companion.ReplacePrefix(messages, 0)
            Assert.Equal("blog paragraph", replaced.Head.Text)

            let host2, _ = makeFake ()
            let companion2 = CompanionHost(SessionId.create "raw-primary", host2)
            let first = [ createObj [ "role", box "user"; "text", box "old" ] ]
            let second = first @ [ createObj [ "role", box "user"; "text", box "tail" ] ]
            companion2.TransformRaw first |> ignore
            do! companion2.WaitInFlightAsync()
            Assert.True(companion2.EnablePrefixReplacement())
            let projected = companion2.TransformRaw second
            Assert.Equal(2, projected.Length)
            Assert.Equal("blog paragraph", (projected.Head: obj)?text)
            Assert.Equal("tail", (projected.[1]: obj)?text)
        }

    [<Fact>]
    let ``CompanionHost_persists_and_restores_B_baseline_and_replacement`` () =
        withTempDir (fun directory ->
            task {
                let primaryId = SessionId.create "durable-primary"
                let runtimeId = Wanxiangshu.Next.Kernel.Identity.RuntimeId.create "durable-runtime"
                let journal = AgentJournal.create directory runtimeId 1001 DateTimeOffset.UtcNow
                let durable = AgentJournalCompanionPort(journal) :> ICompanionDurablePort
                let host, _ = makeFake ()
                let companion = CompanionHost(primaryId, host, durable)

                Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
                do! companion.WaitInFlightAsync()
                Assert.True(companion.EnablePrefixReplacement())
                Assert.Equal(Some "blog paragraph", companion.Memory.CurrentB)
                Assert.Equal(Some "{\"step\":1}", companion.Memory.LastSuccessfulProjection)
                Assert.True(companion.Memory.ReplacementActive)

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

                let restoredHost, _ = makeFake ()
                let restored = CompanionHost(primaryId, restoredHost, restoredDurable)

                Assert.Equal(Some "blog paragraph", restored.Memory.CurrentB)
                Assert.Equal(Some "{\"step\":1}", restored.Memory.LastSuccessfulProjection)
                Assert.True(restored.Memory.ReplacementActive)
                (restoredJournal :> IDisposable).Dispose()
            })
