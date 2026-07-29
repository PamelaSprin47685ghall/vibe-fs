namespace Wanxiangshu.Next.Tests.SessionTests

open System
open System.Collections.Generic
open System.Threading.Tasks
open Xunit
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// Provider-prefix cache gates for Companion epoch freeze.
module CompanionCacheTests =
    let private makeHost (nextText: string ref) (condenseText: string) =
        let mutable terminal: (SessionId -> TerminalOutcome -> unit) option = None
        let mutable output = [ "history" ]
        let childId = SessionId.create "blogger-cache"

        { new ISessionHostPort with
            member _.SubscribeTerminal(_, listener) =
                terminal <- Some listener

                { new IDisposable with
                    member _.Dispose() = terminal <- None }

            member _.SendPrompt(_, prompt, _) =
                let text =
                    if prompt.Contains("Condense") then
                        condenseText
                    else
                        nextText.Value

                let fullOutput = "blogger private thought\n\n" + text
                output <- output @ [ fullOutput ]

                terminal
                |> Option.iter (fun l ->
                    l
                        childId
                        (TerminalOutcome.Completed(
                            { SessionId = SessionId.create "blog"
                              RootUserMessageId = MessageId.create "blog"
                              AssistantMessageId = MessageId.create "blog"
                              Role = "test"
                              Directory = ""
                              FinalText = fullOutput
                              Parts =
                                [| createObj
                                       [ "type", box "reasoning"
                                         "text", box "blogger private thought" ]
                                   createObj [ "type", box "text"; "text", box text ] |] }
                        )))

                Task.FromResult(Ok(MessageId.create "accepted"))

            member _.SendChildPromptFireAndForget(_, _, _, _) = Task.FromResult(Ok())
            member _.AbortSession(_) = Task.FromResult(Ok())
            member _.AbortChildren(_) = Task.FromResult(()) :> Task
            member _.CreateChildSession(_, _) = Task.FromResult(Ok childId)
            member _.GetSessionOutput(_) = output }

    let private headText (messages: obj list) =
        let head = List.head messages
        unbox<string> (unbox<obj array> head?parts).[0]?text

    let private msg sessionId id text =
        createObj
            [ "info", box (createObj [ "id", box id; "role", box "user"; "sessionID", box sessionId ])
              "parts", box [| createObj [ "type", box "text"; "text", box text ] |] ]

    let private messageIds (messages: obj list) =
        messages
        |> List.choose (fun m ->
            if isNull m || isNull m?info || isNull m?info?id then
                None
            else
                Some(unbox<string> m?info?id))

    /// P0: FrozenB stays byte-identical across blog success AND Y self-rebase.
    /// Only SwitchEpoch may change the X-visible head.
    [<Fact>]
    let ``FrozenB_stable_across_blog_and_self_rebase`` () =
        task {
            let nextText = ref "B1"
            let host = makeHost nextText "B-condensed"
            let sid = "freeze-primary"

            let companion = new CompanionHost(SessionId.create sid, host)

            let baseMsgs = [ msg sid "u1" "old"; msg sid "u2" "mid" ]
            companion.TransformRaw baseMsgs |> ignore
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "B1", companion.Memory.LatestB)

            Assert.True(companion.EnablePrefixReplacement())

            // First freeze happens on transform with watermark coverage.
            let extended = baseMsgs @ [ msg sid "u3" "tail" ]
            let t1 = companion.TransformRaw extended
            do! companion.WaitInFlightAsync()
            Assert.True(companion.Memory.ActivePrefixEpoch.IsSome)
            let frozen = companion.Memory.ActivePrefixEpoch.Value.FrozenB
            let cutoff = companion.Memory.ActivePrefixEpoch.Value.CutoffMessageIndex
            Assert.Equal("B1", frozen)
            Assert.True(cutoff > 0)
            Assert.True((unbox<string> t1.Head?info?id).StartsWith("companion-b-head"))
            Assert.Equal("B1", headText t1)

            // Blogger accumulates LatestB; FrozenB and injected head stay B1.
            nextText.Value <- "B2"
            let extended2 = extended @ [ msg sid "u4" "more" ]
            let t2 = companion.TransformRaw extended2
            do! companion.WaitInFlightAsync()
            Assert.True(companion.Memory.LatestB.Value.Contains("B2"))
            Assert.Equal(frozen, companion.Memory.ActivePrefixEpoch.Value.FrozenB)
            Assert.Equal(cutoff, companion.Memory.ActivePrefixEpoch.Value.CutoffMessageIndex)
            Assert.Equal("B1", headText t2)

            // Self-rebase updates LatestB immediately; FrozenB stays unchanged
            // (no automatic SwitchEpoch — only real threshold-based SwitchEpoch
            // may update FrozenB).
            Assert.Equal(Submitted, companion.SelfRebase())
            do! companion.WaitInFlightAsync()
            Assert.Equal(Some "B-condensed", companion.Memory.LatestB)
            Assert.Equal(frozen, companion.Memory.ActivePrefixEpoch.Value.FrozenB)

            let t3 = companion.TransformRaw(extended2 @ [ msg sid "u5" "later" ])
            Assert.Equal(frozen, companion.Memory.ActivePrefixEpoch.Value.FrozenB)
            Assert.Equal(cutoff, companion.Memory.ActivePrefixEpoch.Value.CutoffMessageIndex)
            Assert.Equal("B1", headText t3)
        }

    /// P0: epoch cutoff is frozen. Later blogger baseline growth must not
    /// enlarge the deleted prefix range (no context loss).
    [<Fact>]
    let ``Epoch_cutoff_fixed_despite_blogger_baseline_advance`` () =
        task {
            let nextText = ref "B1"
            let host = makeHost nextText "x"
            let sid = "cutoff-primary"

            let companion = new CompanionHost(SessionId.create sid, host)

            let baseMsgs = [ msg sid "u1" "old"; msg sid "u2" "mid"; msg sid "u3" "keep" ]
            companion.TransformRaw baseMsgs |> ignore
            do! companion.WaitInFlightAsync()
            Assert.True(companion.EnablePrefixReplacement())

            let afterFreeze = baseMsgs @ [ msg sid "u4" "tail" ]
            let t1 = companion.TransformRaw afterFreeze
            do! companion.WaitInFlightAsync()
            let epoch = companion.Memory.ActivePrefixEpoch.Value
            Assert.True(epoch.CutoffMessageIndex > 0)
            Assert.True(epoch.CutoffMessageIndex < List.length afterFreeze)

            // Advance baseline by blogging more messages.
            nextText.Value <- "B2"

            let longer =
                afterFreeze
                @ [ msg sid "u5" "extra1"; msg sid "u6" "extra2"; msg sid "u7" "extra3" ]

            let t2 = companion.TransformRaw longer
            do! companion.WaitInFlightAsync()

            Assert.Equal(epoch.CutoffMessageIndex, companion.Memory.ActivePrefixEpoch.Value.CutoffMessageIndex)
            Assert.Equal(epoch.FrozenB, companion.Memory.ActivePrefixEpoch.Value.FrozenB)

            // Messages at/after original cutoff must still appear in the tail.
            let ids = messageIds t2
            Assert.True(ids |> List.exists (fun id -> id.StartsWith("companion-b-head")))
            // u3 was at index 2; if cutoff was 2, u3 is first raw tail message.
            // Regardless, u7 (new tail) must be present.
            Assert.True(List.contains "u7" ids)
            // Critical: if cutoff was frozen at 2, messages u3.. must remain.
            // Dynamic watermark growth must NOT delete u3..u6 while FrozenB is B1.
            Assert.True(List.contains "u3" ids || epoch.CutoffMessageIndex > 2)
            Assert.Equal("B1", headText t2)
        }

    /// Dual-hook safety: second transform with b-head present is a no-op.
    [<Fact>]
    let ``Transform_idempotent_when_b_head_already_present`` () =
        withTempDir (fun directory ->
            task {
                use journal =
                    AgentJournal.create directory (RuntimeId.create "rt-companion-cache") 1 DateTimeOffset.UtcNow

                let sid = "primary"
                registerAuthorityRoot journal sid "fast-manager"

                let nextText = ref "blog paragraph"
                let host = makeHost nextText "x"
                let companions = Dictionary<string, CompanionHost>()
                let gate = obj ()
                let sessionRoles = Dictionary<string, string>()
                let sessionBudgets = Dictionary<string, int>()
                sessionBudgets.["primary"] <- 100
                let sessionOutputLimits = Dictionary<string, int>()

                let companion = new CompanionHost(SessionId.create sid, host)

                companions.[sid] <- companion

                let baseMsgs = [| msg sid "u1" "old"; msg sid "u2" "mid" |]
                let out1 = createObj [ "messages", box baseMsgs ]
                let inObj = createObj [ "sessionID", box sid; "agent", box "fast-manager" ]

                CompanionTransform.handleCompanionTransform
                    companions
                    gate
                    host
                    (Some journal)
                    sessionBudgets
                    sessionOutputLimits
                    sessionRoles
                    None
                    inObj
                    out1

                do! companion.WaitInFlightAsync()
                Assert.True(companion.EnablePrefixReplacement())

                let extended = Array.append baseMsgs [| msg sid "u3" "tail" |]
                let out2 = createObj [ "messages", box extended ]

                CompanionTransform.handleCompanionTransform
                    companions
                    gate
                    host
                    (Some journal)
                    sessionBudgets
                    sessionOutputLimits
                    sessionRoles
                    None
                    inObj
                    out2

                do! companion.WaitInFlightAsync()
                let after1 = unbox<obj array> out2?messages
                Assert.True((unbox<string> after1.[0]?info?id).StartsWith("companion-b-head"))

                CompanionTransform.handleCompanionTransform
                    companions
                    gate
                    host
                    (Some journal)
                    sessionBudgets
                    sessionOutputLimits
                    sessionRoles
                    None
                    inObj
                    out2

                let after2 = unbox<obj array> out2?messages
                Assert.equal (after1.Length, after2.Length)

                let heads =
                    after2
                    |> Array.filter (fun m ->
                        not (isNull m)
                        && not (isNull m?info)
                        && not (isNull m?info?id)
                        && (unbox<string> m?info?id).StartsWith("companion-b-head"))

                Assert.equal (1, heads.Length)
            })
