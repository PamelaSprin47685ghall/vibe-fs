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
    let ``CompanionHost_updates_B_and_reuses_blogger`` () =
        task {
            let host, childCount, sentModel, _, _, _, _ = makeFake ()

            let companion =
                new CompanionHost(SessionId.create "primary", host, ?bloggerModel = Some(Ok bloggerModel))

            Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "blog paragraph", companion.Memory.LatestB)
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
            Assert.True(replaced.Head.Text.Contains("blog paragraph"))
        }

    [<Fact>]
    let ``CompanionHost_persists_and_restores_B_baseline_and_replacement`` () =
        withTempDir (fun directory ->
            task {
                let primaryId = SessionId.create "durable-primary"

                let journal =
                    AgentJournal.create directory (RuntimeId.create "durable-runtime") 1001 DateTimeOffset.UtcNow

                let durable = AgentJournalCompanionPort(journal) :> ICompanionDurablePort
                let host, _, _, _, _, _, _ = makeFake ()

                let companion =
                    new CompanionHost(primaryId, host, durable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":1}"))
                do! companion.WaitInFlightAsync()
                Assert.Equal(Submitted, companion.SubmitProjection("{\"step\":2}"))
                do! companion.WaitInFlightAsync()
                Assert.True(companion.EnablePrefixReplacement())
                Assert.Equal(Some "blog paragraph\n\nblog paragraph", companion.Memory.LatestB)
                Assert.Equal(Some "{\"step\":2}", companion.Memory.LastSuccessfulProjection)
                (journal :> IDisposable).Dispose()

                let boot = Boot.boot directory

                use restoredJournal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "durable-runtime-restored")
                        1002
                        DateTimeOffset.UtcNow
                        boot

                let restoredDurable =
                    AgentJournalCompanionPort(restoredJournal) :> ICompanionDurablePort

                let restoredHost, _, _, _, _, _, _ = makeFake ()

                let restored =
                    new CompanionHost(primaryId, restoredHost, restoredDurable, ?bloggerModel = Some(Ok bloggerModel))

                Assert.Equal(Some "blog paragraph\n\nblog paragraph", restored.Memory.LatestB)
                Assert.Equal(Some "{\"step\":2}", restored.Memory.LastSuccessfulProjection)
                Assert.True(restored.EnablePrefixReplacement())
            })

    [<Fact>]
    let ``CompanionHost_same_message_id_with_changed_content_is_not_prefix`` () =
        task {
            let host, _, _, _, _, _, _ = makeFake ()

            let companion =
                new CompanionHost(SessionId.create "canonical-primary", host, ?bloggerModel = Some(Ok bloggerModel))

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

    /// Bug A (P0 DATA LOSS): Y self-rebase must NOT advance the projection
    /// baseline. The Blogger child sees only the old B when condensing — it
    /// never processes the P0→P1 delta. If the baseline advanced to P1, the
    /// next TransformRaw(P1) would see no delta and skip, losing P0→P1.
    /// After fix: SelfRebase replaces only B', leaving LastSuccessfulProjection
    /// at P0 so the next TransformRaw computes the full P0→P1 delta.
    [<Fact>]
    let ``CompanionHost_self_rebase_does_not_advance_projection_baseline`` () =
        task {
            let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
            let mutable output = [ "history" ]
            let mutable condensedOutput = "condensed B prime"
            let childId = SessionId.create "blogger-rebase"

            let host =
                { new ISessionHostPort with
                    member _.SubscribeTerminal(_, listener) =
                        terminal <- Some listener

                        { new IDisposable with
                            member _.Dispose() = terminal <- None }

                    member _.SendPrompt(_, prompt, _) =
                        // selfRebaseBlog sends a prompt containing "Condense"
                        let isSelfRebase = prompt.Contains("Condense")
                        let text = if isSelfRebase then condensedOutput else "blog paragraph"
                        output <- output @ [ text ]

                        terminal
                        |> Option.iter (fun l -> l childId (Completed(MessageId.create "blog")))

                        Task.FromResult(Ok(MessageId.create "accepted"))

                    member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
                    member _.AbortSession(_) = Task.FromResult(Ok())
                    member _.AbortChildren(_) = Task.FromResult(()) :> Task
                    member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
                    member _.GetSessionOutput(_) = output }

            let companion =
                new CompanionHost(SessionId.create "rebase-primary", host, ?bloggerModel = Some(Ok bloggerModel))

            // P0: submit a projection to establish baseline + B.
            let p0 = """{"step":1}"""
            Assert.Equal(Submitted, companion.SubmitProjection(p0))
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some p0, companion.Memory.LastSuccessfulProjection)
            Assert.Equal(Some "blog paragraph", companion.Memory.LatestB)

            // Trigger Y self-rebase. The Blogger condenses old B into B'.
            Assert.Equal(Submitted, companion.SelfRebase())
            do! companion.WaitInFlightAsync()

            // KEY ASSERTION: baseline must still be P0, NOT advanced.
            // Only B is replaced.
            Assert.Equal(Some p0, companion.Memory.LastSuccessfulProjection)
            Assert.Equal(Some "condensed B prime", companion.Memory.LatestB)

            // Now submit P1 (more messages). The delta P0→P1 must be computed,
            // not skipped — because the baseline is still P0.
            let p1 = """{"step":2}"""
            Assert.Equal(Submitted, companion.SubmitProjection(p1))
            do! companion.WaitInFlightAsync()

            // After processing P1, baseline advances to P1 and B grows.
            Assert.Equal(Some p1, companion.Memory.LastSuccessfulProjection)
            Assert.True(companion.Memory.LatestB.IsSome)
            Assert.True(companion.Memory.LatestB.Value.Contains("condensed B prime"))
            Assert.True(companion.Memory.LatestB.Value.Contains("blog paragraph"))
        }
