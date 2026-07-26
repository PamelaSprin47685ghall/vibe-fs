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

    let private bloggerModel =
        { providerID = "cheap-provider"
          modelID = "cheap-blogger"
          variant = Some "fast" }

    let private makeFake () =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable childCount = 0
        let mutable sentModel: OpencodeModel option = None
        let mutable output = [ "history" ]
        let childId = SessionId.create "blogger-1"

        let host =
            { new ISessionHostPort with
                member _.SubscribeTerminal(_, listener) =
                    terminal <- Some listener

                    { new IDisposable with
                        member _.Dispose() = terminal <- None }

                member _.SendPrompt(_, _, options) =
                    sentModel <- options.Model
                    output <- output @ [ "blog paragraph" ]

                    terminal
                    |> Option.iter (fun listener -> listener childId (Completed(MessageId.create "blog")))

                    Task.FromResult(Ok(MessageId.create "accepted"))

                member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                member _.AbortSession(_) = Task.FromResult(Ok())
                member _.AbortChildren(_) = Task.FromResult(()) :> Task

                member _.CreateChildSession(_, _) =
                    childCount <- childCount + 1
                    Task.FromResult(Ok childId)

                member _.GetSessionOutput(_) = output }

        host, (fun () -> childCount), (fun () -> sentModel)

    [<Fact>]
    let ``CompanionHost_updates_B_and_reuses_blogger`` () =
        task {
            let host, childCount, sentModel = makeFake ()

            let companion =
                CompanionHost(SessionId.create "primary", host, ?bloggerModel = Some(Ok bloggerModel))

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "blog paragraph", companion.Memory.CurrentB)

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":2}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(1, childCount ())
            Assert.Equal(Some bloggerModel, sentModel ())
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
            Assert.True(replaced.Head.Text.Contains("blog paragraph"), "B should be accumulated after two submissions")

            let host2, _, _ = makeFake ()

            let companion2 =
                CompanionHost(SessionId.create "raw-primary", host2, ?bloggerModel = Some(Ok bloggerModel))

            let first = [ createObj [ "role", box "user"; "text", box "old" ] ]
            let second = first @ [ createObj [ "role", box "user"; "text", box "tail" ] ]
            companion2.TransformRaw first |> ignore
            do! companion2.WaitInFlightAsync()
            Assert.True(companion2.EnablePrefixReplacement())
            let projected = companion2.TransformRaw second
            Assert.Equal(2, projected.Length)
            let headParts = unbox<obj array> ((projected.Head: obj)?parts)
            Assert.NotNull((headParts.[0]: obj)?text)
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
                let host, _, _ = makeFake ()

                let companion =
                    CompanionHost(primaryId, host, durable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
                do! companion.WaitInFlightAsync()
                Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":2}"))
                do! companion.WaitInFlightAsync()
                Assert.True(companion.EnablePrefixReplacement())
                Assert.Equal(Some "blog paragraph\n\nblog paragraph", companion.Memory.CurrentB)
                Assert.Equal(Some "{\"step\":2}", companion.Memory.LastSuccessfulProjection)
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

                let restoredHost, _, _ = makeFake ()

                let restored =
                    CompanionHost(primaryId, restoredHost, restoredDurable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Some "blog paragraph\n\nblog paragraph", restored.Memory.CurrentB)
                Assert.Equal(Some "{\"step\":2}", restored.Memory.LastSuccessfulProjection)
                Assert.True(restored.Memory.ReplacementActive)
                (restoredJournal :> IDisposable).Dispose()
            })

    [<Fact>]
    let ``CompanionHost_same_message_id_with_changed_content_is_not_prefix`` () =
        task {
            let host, _, _ = makeFake ()

            let companion =
                CompanionHost(SessionId.create "canonical-primary", host, ?bloggerModel = Some(Ok bloggerModel))

            let first =
                [ createObj
                      [ "info", box (createObj [ "id", box "message-1"; "role", box "user" ])
                        "parts", box [| createObj [ "type", box "text"; "text", box "old" ] |] ] ]

            let changed =
                [ createObj
                      [ "info", box (createObj [ "id", box "message-1"; "role", box "user" ])
                        "parts", box [| createObj [ "type", box "text"; "text", box "new" ] |] ] ]

            companion.TransformRaw first |> ignore
            do! companion.WaitInFlightAsync()
            Assert.True(companion.EnablePrefixReplacement())

            let transformed = companion.TransformRaw changed
            Assert.Equal(1, transformed.Length)
            Assert.False(isNull transformed.Head?info)
            Assert.Equal("message-1", unbox<string> transformed.Head?info?id)
        }

    [<Fact>]
    let ``Companion_jsonDelta_emits_nested_removals_and_array_replacements`` () =
        let previous =
            Some """{"gone":{"nested":true},"kept":{"items":[1,2,3]},"replace":[{"a":1}]}"""

        let current = """{"kept":{"items":[1]},"replace":[{"a":2}]}"""

        let delta = Companion.jsonDelta previous current
        Assert.True(delta.IsSome)
        Assert.Contains("\"op\":\"remove\"", delta.Value)
        Assert.Contains("/gone", delta.Value)
        Assert.Contains("/kept/items/2", delta.Value)
        Assert.Contains("/replace/0/a", delta.Value)
