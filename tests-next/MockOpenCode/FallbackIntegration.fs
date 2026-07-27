namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// Fallback integration scenarios: verifies RetrySignalHandler +
/// AgentJournal + DurableFallback for A/A/B/B model selection,
/// deduplication, and crash recovery.
module FallbackIntegration =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then failwith message

    /// Helper: count fallback failures in a journal snapshot for a session.
    let private fallbackFailures (journal: AgentJournal) sessionId : int =
        match (AgentJournal.snapshot journal).AgentProjections.Sessions.TryFind sessionId with
        | Some session ->
            session.Fallback
            |> Option.map (fun fb -> fb.TotalFailures)
            |> Option.defaultValue 0
        | None -> 0

    /// Helper: query the selected model side from the journal projection.
    let private fallbackSide (journal: AgentJournal) sessionId : string option =
        match (AgentJournal.snapshot journal).AgentProjections.Sessions.TryFind sessionId with
        | Some session ->
            session.Fallback
            |> Option.map (fun fb -> if fb.Side = ModelSide.SideB then "B" else "A")
        | None -> None

    /// Helper: construct a retry signal.
    let private retrySignal (sessionId: string) (attempt: string) (reason: string) : RetrySignal =
        { SessionId = SessionId.create sessionId
          Attempt = attempt
          Reason = reason
          MessageId = None }

    /// One provider retry produces one durable fallback failure. Side stays A.
    let ``First retry records one failure on side A`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-a1"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-a1-runtime")
                        1
                        DateTimeOffset.UtcNow

                RetrySignalHandler.handle
                    (Some journal) recorded userBindings
                    (retrySignal sessionId "1" "first provider failure")

                equal 1 (fallbackFailures journal (SessionId.create sessionId))
                equal (Some "A") (fallbackSide journal (SessionId.create sessionId))
            })

    /// Two retries: side switches to B after the second failure.
    let ``Second retry switches to side B`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-b1"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-b1-runtime")
                        1
                        DateTimeOffset.UtcNow

                // First retry
                RetrySignalHandler.handle
                    (Some journal) recorded userBindings
                    (retrySignal sessionId "1" "failure 1")
                equal (Some "A") (fallbackSide journal (SessionId.create sessionId))

                // Second retry -> switch to B
                RetrySignalHandler.handle
                    (Some journal) recorded userBindings
                    (retrySignal sessionId "2" "failure 2")
                equal 2 (fallbackFailures journal (SessionId.create sessionId))
                equal (Some "B") (fallbackSide journal (SessionId.create sessionId))
            })

    /// Three retries: stays on B, failures accumulate.
    let ``Third retry stays on side B`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-b2"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-b2-runtime")
                        1
                        DateTimeOffset.UtcNow

                for i in 1 .. 3 do
                    RetrySignalHandler.handle
                        (Some journal) recorded userBindings
                        (retrySignal sessionId (string i) (sprintf "failure %d" i))

                equal 3 (fallbackFailures journal (SessionId.create sessionId))
                equal (Some "B") (fallbackSide journal (SessionId.create sessionId))
            })

    /// Four retries: session is dead.
    let ``Fourth retry makes session dead`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-dead"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-dead-runtime")
                        1
                        DateTimeOffset.UtcNow

                for i in 1 .. 4 do
                    RetrySignalHandler.handle
                        (Some journal) recorded userBindings
                        (retrySignal sessionId (string i) (sprintf "failure %d" i))

                equal 4 (fallbackFailures journal (SessionId.create sessionId))

                // Wanxiangshu.Next.Session.DurableFallback.isDead must return true
                let snapshot = AgentJournal.snapshot journal
                trueThat (Wanxiangshu.Next.Session.DurableFallback.isDead (SessionId.create sessionId) snapshot)
                    "Session must be dead after 4 failures"
            })

    /// Identical retry signals are deduplicated: same sessionId +
    /// currentUserMessageId + providerAttempt.
    let ``Duplicate retry signals are deduplicated`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-dedup"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-dedup-runtime")
                        1
                        DateTimeOffset.UtcNow

                // Same retry three times
                for _ in 1 .. 3 do
                    RetrySignalHandler.handle
                        (Some journal) recorded userBindings
                        (retrySignal sessionId "1" "same failure")

                // Only one failure recorded
                equal 1 (fallbackFailures journal (SessionId.create sessionId))
            })

    /// Fallback state survives a journal reboot (crash recovery).
    let ``Fallback state survives journal boot fold after restart`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-restart"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                // Phase 1: record 2 failures before "crash"
                do! task {
                    use journal =
                        AgentJournal.create
                            directory
                            (RuntimeId.create "fallback-restart-old-runtime")
                            1
                            DateTimeOffset.UtcNow

                    RetrySignalHandler.handle
                        (Some journal) recorded userBindings
                        (retrySignal sessionId "1" "failure 1")

                    RetrySignalHandler.handle
                        (Some journal) recorded userBindings
                        (retrySignal sessionId "2" "failure 2")

                    equal (Some "B") (fallbackSide journal (SessionId.create sessionId))
                    ()
                }

                // Phase 2: "restart" - create journal from boot
                let boot = Boot.boot directory

                use restartedJournal =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "fallback-restart-new-runtime")
                        2
                        DateTimeOffset.UtcNow
                        boot

                // Fallback state must be recovered
                equal 2 (fallbackFailures restartedJournal (SessionId.create sessionId))
                equal (Some "B") (fallbackSide restartedJournal (SessionId.create sessionId))
            })

    /// A retry signal without user/assistant identity writes nothing.
    let ``Retry without user binding writes nothing`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-orphan"
                let recorded = HashSet<string>()
                let emptyBindings = Dictionary<string, MessageId>()

                use journal =
                    AgentJournal.create
                        directory
                        (RuntimeId.create "fallback-orphan-runtime")
                        1
                        DateTimeOffset.UtcNow

                RetrySignalHandler.handle
                    (Some journal) recorded emptyBindings
                    (retrySignal sessionId "1" "no identity")

                equal 0 (fallbackFailures journal (SessionId.create sessionId))
            })

    /// User cancellation does NOT count as a fallback failure.
    let ``User cancellation does not increment fallback failures`` () =
        withTempDir (fun directory ->
            task {
                let sessionIdStr = "fallback-cancel"
                let state, eventPort, sessionPort = MockOpenCode.createHost ()
                use _sub = sessionPort.SubscribeTerminal(SessionId.create sessionIdStr, (fun _ _ -> ()))

                use journal =
                    AgentJournal.create directory (RuntimeId.create "fb-cancel-runtime") 1 DateTimeOffset.UtcNow

                let turn: ReconciledTurn =
                    { SessionId = SessionId.create sessionIdStr
                      UserMessageId = MessageId.create "u-cancel"
                      AssistantMessageId = MessageId.create "a-cancel"
                      AgentRole = Some AgentRole.Coder
                      Directory = "/tmp/ws"
                      Parts = [||]
                      Finish = None
                      ErrorName = Some "MessageAbortedError"
                      Model = None
                      Outcome = TurnOutcome.TurnAborted "user pressed escape" }

                TerminalPolicies.apply
                    sessionPort eventPort (Some journal) None
                    (HashSet<string>()) (HashSet<string>()) (HashSet<string>()) (Dictionary<string, string>())
                    (fun _ -> ()) (HashSet<string>())
                    turn

                do! drainMicrotasks 8
                let failures =
                    match (AgentJournal.snapshot journal).AgentProjections.Sessions.TryFind (SessionId.create sessionIdStr) with
                    | Some s -> s.Fallback |> Option.map (fun fb -> fb.TotalFailures) |> Option.defaultValue 0
                    | None -> 0
                if failures <> 0 then failwithf "Expected 0 fallback failures for cancellation, got %d" failures
            })