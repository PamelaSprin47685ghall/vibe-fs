namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module ReviewGuardIntegration =

    let private trueThat condition message =
        if not condition then
            failwith message

    /// Reviewer terminal without verdict: TerminalPolicies.apply must
    /// send a guard nudge through the mock port when a journal is present.
    let ``Reviewer no-verdict triggers guard nudge via TerminalPolicies`` () =
        withTempDir (fun directory ->
            task {
                let state, eventPort, sessionPort = MockOpenCode.createHost ()
                let sessionId = SessionId.create "r-no-verdict"
                use _sub = sessionPort.SubscribeTerminal(sessionId, (fun _ _ -> ()))

                use journal =
                    AgentJournal.create directory (RuntimeId.create "rg-test-runtime") 1 DateTimeOffset.UtcNow

                registerAuthorityRoot journal (SessionId.value sessionId) "reviewer"

                let turn: ReconciledTurn =
                    { SessionId = sessionId
                      UserMessageId = MessageId.create "u1"
                      RootUserMessageId = MessageId.create "u1"
                      AssistantMessageId = MessageId.create "a1"
                      AgentRole = Some AgentRole.Reviewer
                      Directory = "/tmp/ws"
                      Parts = [| MessagePart.Text "reviewed" |]
                      Finish = Some "stop"
                      ErrorName = None
                      Model = None
                      Outcome = TurnOutcome.TurnCompleted }

                TerminalPolicies.apply
                    sessionPort
                    eventPort
                    (Some journal)
                    None
                    (HashSet<string>())
                    (HashSet<string>())
                    (HashSet<string>())
                    (Dictionary<string, string>())
                    (fun _ -> ())
                    (HashSet<string>())
                    turn

                do! drainMicrotasks 16
                trueThat (state.Sent.Length > 0) "Guard nudge must send a prompt"
            })

    /// Aborted turn: must NOT trigger a guard nudge even with journal.
    let ``Aborted turn fires terminal but no guard nudge`` () =
        withTempDir (fun directory ->
            task {
                let state, eventPort, sessionPort = MockOpenCode.createHost ()
                let sessionId = SessionId.create "a-no-nudge"
                use _sub = sessionPort.SubscribeTerminal(sessionId, (fun _ _ -> ()))

                use journal =
                    AgentJournal.create directory (RuntimeId.create "rg-abort-runtime") 1 DateTimeOffset.UtcNow

                let turn: ReconciledTurn =
                    { SessionId = sessionId
                      UserMessageId = MessageId.create "u1"
                      RootUserMessageId = MessageId.create "u1"
                      AssistantMessageId = MessageId.create "a1"
                      AgentRole = Some AgentRole.Coder
                      Directory = "/tmp/ws"
                      Parts = [||]
                      Finish = None
                      ErrorName = Some "MessageAbortedError"
                      Model = None
                      Outcome = TurnOutcome.TurnAborted "user cancelled" }

                TerminalPolicies.apply
                    sessionPort
                    eventPort
                    (Some journal)
                    None
                    (HashSet<string>())
                    (HashSet<string>())
                    (HashSet<string>())
                    (Dictionary<string, string>())
                    (fun _ -> ())
                    (HashSet<string>())
                    turn

                do! drainMicrotasks 8
                trueThat (state.Sent.Length = 0) "Aborted turn must not send guard nudge"
            })
