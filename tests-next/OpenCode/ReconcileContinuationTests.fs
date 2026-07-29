namespace Wanxiangshu.Next.Tests.OpenCode

open System
open System.Collections.Generic
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport
open Wanxiangshu.Next.Tests.OpenCode.ReconcileContinuationSupport

module ReconcileContinuationTests =

    [<Fact>]
    let ``DevOps reasoning-only idle dispatches one interaction repair`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = SessionId.create "deep-devops-reasoning-only"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                let eventPort = Events.HostEventPort()

                use journal =
                    AgentJournal.create directory (RuntimeId.create "deep-devops-runtime") 1 DateTimeOffset.UtcNow

                registerAuthorityRoot journal (SessionId.value sessionId) "deep-devops"

                let snapshot = FixedSnapshot(reasoningOnlyMessages "deep-devops")

                let onTurn turn =
                    TerminalPolicies.apply
                        sessionPort
                        eventPort
                        (Some journal)
                        None
                        (HashSet())
                        (HashSet())
                        (HashSet())
                        (Dictionary())
                        ignore
                        (HashSet())
                        turn

                let reconciler = SessionReconciler(snapshot, onTurn)
                bind reconciler sessionId AgentRole.DevOps

                reconciler.HandleSignal(SessionIdle sessionId)
                reconciler.HandleSignal(SessionIdle sessionId)
                do! drainMicrotasks 24

                Assert.Single(prompts) |> ignore
                Assert.Contains("no final task report was produced", snd prompts.[0])
                Assert.Equal(0, fallbackFailures journal sessionId)

                let authority =
                    (AgentJournal.snapshot journal).AgentProjections.Sessions
                    |> Map.find sessionId
                    |> fun session -> session.PromptAuthority
                    |> Option.get

                Assert.equal (
                    Some "deep-devops",
                    authority.ActiveLogicalRun |> Option.map (fun profile -> profile.SelectedAgent)
                )

                Assert.True(
                    authority.PendingClaims
                    |> Map.exists (fun _ claim -> claim.EffectiveAgent = Some "deep-devops")
                )
            })

    [<Fact>]
    let ``Contains broken XML formal text is interaction repair not fallback`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = SessionId.create "deep-devops-contains-xml"
                let prompts = ResizeArray<string * string>()
                let sessionPort = recordingPort prompts
                let eventPort = Events.HostEventPort()

                use journal =
                    AgentJournal.create directory (RuntimeId.create "deep-devops-xml-runtime") 1 DateTimeOffset.UtcNow

                registerAuthorityRoot journal (SessionId.value sessionId) "deep-devops"

                let snapshot = FixedSnapshot(brokenXmlMessages "deep-devops")

                let onTurn turn =
                    TerminalPolicies.apply
                        sessionPort
                        eventPort
                        (Some journal)
                        None
                        (HashSet())
                        (HashSet())
                        (HashSet())
                        (Dictionary())
                        ignore
                        (HashSet())
                        turn

                let reconciler = SessionReconciler(snapshot, onTurn)
                bind reconciler sessionId AgentRole.DevOps
                reconciler.HandleSignal(SessionIdle sessionId)
                do! drainMicrotasks 24

                Assert.Single(prompts) |> ignore
                Assert.Contains("no final task report was produced", snd prompts.[0])
                Assert.Equal(0, fallbackFailures journal sessionId)

                match outcomeOf (brokenXmlMessages "deep-devops") with
                | TurnOutcome.TurnNeedsContinuation reason -> Assert.Contains("XML", reason)
                | other -> Assert.True(false, sprintf "expected contains-XML continuation, got %A" other)
            })
