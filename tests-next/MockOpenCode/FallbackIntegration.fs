namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

/// RetrySignalHandler + journal fold: infinite A/A/B/B, dedupe, restart.
module FallbackIntegration =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private fbOf (journal: AgentJournal) sessionId =
        (AgentJournal.snapshot journal).AgentProjections.Sessions.TryFind sessionId
        |> function
            | Some s -> s.Fallback
            | None -> None

    let private fallbackFailures journal sessionId =
        fbOf journal sessionId
        |> Option.map (fun fb -> List.length fb.RecentFailureIds)
        |> Option.defaultValue 0

    let private fallbackOffset journal sessionId =
        fbOf journal sessionId |> Option.map (fun fb -> fb.Offset)

    let private fallbackSide journal sessionId =
        fbOf journal sessionId
        |> Option.map (fun fb ->
            match AgentPairCursor.side fb.Offset with
            | AgentPairCursor.ModelSide.SideB -> "B"
            | AgentPairCursor.ModelSide.SideA -> "A")

    let private retrySignal sessionId attempt reason : RetrySignal =
        { SessionId = SessionId.create sessionId
          Attempt = attempt
          Reason = reason
          MessageId = None }

    let private withRetryJournal
        name
        (body: string -> HashSet<string> -> Dictionary<string, MessageId> -> AgentJournal -> Task<unit>)
        =
        withTempDir (fun directory ->
            task {
                let sessionId = name
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                use journal =
                    AgentJournal.create directory (RuntimeId.create (name + "-rt")) 1 DateTimeOffset.UtcNow

                do! body sessionId recorded userBindings journal
            })

    let ``First retry records one failure on side A`` () =
        withRetryJournal "fallback-a1" (fun sessionId recorded bindings journal ->
            task {
                RetrySignalHandler.handle (Some journal) recorded bindings (retrySignal sessionId "1" "first")
                let sid = SessionId.create sessionId
                equal 1 (fallbackFailures journal sid)
                equal (Some 1uy) (fallbackOffset journal sid)
                equal (Some "A") (fallbackSide journal sid)
            })

    let ``Second retry switches to side B`` () =
        withRetryJournal "fallback-b1" (fun sessionId recorded bindings journal ->
            task {
                let sid = SessionId.create sessionId
                RetrySignalHandler.handle (Some journal) recorded bindings (retrySignal sessionId "1" "f1")
                equal (Some "A") (fallbackSide journal sid)
                RetrySignalHandler.handle (Some journal) recorded bindings (retrySignal sessionId "2" "f2")
                equal 2 (fallbackFailures journal sid)
                equal (Some 2uy) (fallbackOffset journal sid)
                equal (Some "B") (fallbackSide journal sid)
            })

    let ``Third retry stays on side B`` () =
        withRetryJournal "fallback-b2" (fun sessionId recorded bindings journal ->
            task {
                for i in 1..3 do
                    RetrySignalHandler.handle
                        (Some journal)
                        recorded
                        bindings
                        (retrySignal sessionId (string i) (sprintf "f%d" i))

                let sid = SessionId.create sessionId
                equal 3 (fallbackFailures journal sid)
                equal (Some 3uy) (fallbackOffset journal sid)
                equal (Some "B") (fallbackSide journal sid)
            })

    let ``Fourth retry wraps to side A and is not dead`` () =
        withRetryJournal "fallback-wrap" (fun sessionId recorded bindings journal ->
            task {
                for i in 1..4 do
                    RetrySignalHandler.handle
                        (Some journal)
                        recorded
                        bindings
                        (retrySignal sessionId (string i) (sprintf "f%d" i))

                let sid = SessionId.create sessionId
                equal 4 (fallbackFailures journal sid)
                equal (Some 0uy) (fallbackOffset journal sid)
                equal (Some "A") (fallbackSide journal sid)

            // 0.5.0: there is no Dead state; the cursor is still alive at offset 0.
            })

    let ``Duplicate retry signals are deduplicated`` () =
        withRetryJournal "fallback-dedup" (fun sessionId recorded bindings journal ->
            task {
                for _ in 1..3 do
                    RetrySignalHandler.handle (Some journal) recorded bindings (retrySignal sessionId "1" "same")

                equal 1 (fallbackFailures journal (SessionId.create sessionId))
            })

    let ``Fallback state survives journal boot fold after restart`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-restart"
                let recorded = HashSet<string>()
                let userBindings = Dictionary<string, MessageId>()
                userBindings.[sessionId] <- MessageId.create "user-1"

                do!
                    task {
                        use journal =
                            AgentJournal.create
                                directory
                                (RuntimeId.create "fallback-restart-old")
                                1
                                DateTimeOffset.UtcNow

                        RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "1" "f1")
                        RetrySignalHandler.handle (Some journal) recorded userBindings (retrySignal sessionId "2" "f2")
                        equal (Some 2uy) (fallbackOffset journal (SessionId.create sessionId))
                        equal (Some "B") (fallbackSide journal (SessionId.create sessionId))
                    }

                let boot = Boot.boot directory

                use restarted =
                    AgentJournal.createFromBoot
                        directory
                        (RuntimeId.create "fallback-restart-new")
                        2
                        DateTimeOffset.UtcNow
                        boot

                let sid = SessionId.create sessionId
                equal 2 (fallbackFailures restarted sid)
                equal (Some 2uy) (fallbackOffset restarted sid)
                equal (Some "B") (fallbackSide restarted sid)
            })

    let ``Retry without user binding writes nothing`` () =
        withTempDir (fun directory ->
            task {
                let sessionId = "fallback-orphan"

                use journal =
                    AgentJournal.create directory (RuntimeId.create "fallback-orphan-rt") 1 DateTimeOffset.UtcNow

                RetrySignalHandler.handle
                    (Some journal)
                    (HashSet())
                    (Dictionary())
                    (retrySignal sessionId "1" "no identity")

                equal 0 (fallbackFailures journal (SessionId.create sessionId))
            })

    let ``User cancellation does not increment fallback failures`` () =
        withTempDir (fun directory ->
            task {
                let sessionIdStr = "fallback-cancel"
                let _, eventPort, sessionPort = MockOpenCode.createHost ()

                use _sub =
                    sessionPort.SubscribeTerminal(SessionId.create sessionIdStr, (fun _ _ -> ()))

                use journal =
                    AgentJournal.create directory (RuntimeId.create "fb-cancel-rt") 1 DateTimeOffset.UtcNow

                let turn: ReconciledTurn =
                    { SessionId = SessionId.create sessionIdStr
                      UserMessageId = MessageId.create "u-cancel"
                      RootUserMessageId = MessageId.create "u-cancel"
                      AssistantMessageId = MessageId.create "a-cancel"
                      AgentRole = Some AgentRole.Coder
                      Directory = "/tmp/ws"
                      Parts = [||]
                      Finish = None
                      ErrorName = Some "MessageAbortedError"
                      Model = None
                      Outcome = TurnOutcome.TurnAborted "user pressed escape" }

                TerminalPolicies.apply
                    sessionPort
                    eventPort
                    (Some journal)
                    None
                    (HashSet())
                    (HashSet())
                    (HashSet())
                    (Dictionary())
                    (fun _ -> ())
                    (HashSet())
                    turn

                do! drainMicrotasks 8
                equal 0 (fallbackFailures journal (SessionId.create sessionIdStr))
            })
