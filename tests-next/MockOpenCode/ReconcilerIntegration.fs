namespace Wanxiangshu.Next.Tests.MockOpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.OpenCode
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Tests.EventDrivenHarness
open Wanxiangshu.Next.Tests.JournalTests.JournalTestSupport

module ReconcilerIntegration =

    let private equal expected actual =
        if not (Unchecked.equals expected actual) then
            failwithf "Expected %A, got %A" expected actual

    let private trueThat condition message =
        if not condition then
            failwith message

    let private textPart text =
        createObj [ "id", box "p1"; "type", box "text"; "text", box text ]

    let private msg id role agent finish parts errorName =
        { Id = MessageId.create id
          Role = role
          Agent = agent
          Finish = finish
          ErrorName = errorName
          Model = None
          Parts = parts
          Raw = createObj [] }

    /// Reconciler + InjectedSessionPort + TerminalPolicies: reconcile
    /// triggers guard nudge through mock port.
    let ``Reconciler triggers guard nudge through mock port`` () =
        withTempDir (fun directory ->
            task {
                let state, eventPort, sessionPort = MockOpenCode.createHost ()
                let sid = SessionId.create "s1"
                use _sub = sessionPort.SubscribeTerminal(sid, (fun _ _ -> ()))

                use journal =
                    AgentJournal.create directory (RuntimeId.create "recon-test") 1 DateTimeOffset.UtcNow

                registerAuthorityRoot journal (SessionId.value sid) "reviewer"

                let onTurn turn =
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

                let snapshot =
                    { new ISessionSnapshotPort with
                        member _.GetMessages _ =
                            Task.FromResult(
                                Ok
                                    [ msg "u1" "user" None None [||] None
                                      msg
                                          "a1"
                                          "assistant"
                                          (Some "reviewer")
                                          (Some "stop")
                                          [| textPart "review done" |]
                                          None ]
                            ) }

                let reconciler = SessionReconciler(snapshot, onTurn)

                reconciler.BindActiveRun
                    { SessionId = sid
                      RunId = None
                      RootUserMessageId = Some(MessageId.create "u1")
                      PhysicalUserMessageId = Some(MessageId.create "u1")
                      ContinuationMessageIds = Set.empty
                      AgentRole = Some AgentRole.Reviewer
                      Directory = "/tmp/ws" }

                reconciler.MarkDirty(sid)
                do! drainMicrotasks 16

                trueThat (state.Sent.Length > 0) "Reconciler flow must produce guard nudge via mock port"
            })

    /// Duplicate idle signals produce exactly one turn.
    let ``Reconciler deduplicates idle signals`` () =
        task {
            let state, eventPort, sessionPort = MockOpenCode.createHost ()
            let sid = SessionId.create "s-dedup"
            use _sub = sessionPort.SubscribeTerminal(sid, (fun _ _ -> ()))

            let turnCount = ref 0
            let onTurn _ = turnCount.Value <- turnCount.Value + 1

            let snapshot =
                { new ISessionSnapshotPort with
                    member _.GetMessages _ =
                        Task.FromResult(
                            Ok
                                [ msg "u1" "user" None None [||] None
                                  msg "a1" "assistant" (Some "coder") (Some "stop") [| textPart "done" |] None ]
                        ) }

            let reconciler = SessionReconciler(snapshot, onTurn)

            reconciler.BindActiveRun
                { SessionId = sid
                  RunId = None
                  RootUserMessageId = Some(MessageId.create "u1")
                  PhysicalUserMessageId = Some(MessageId.create "u1")
                  ContinuationMessageIds = Set.empty
                  AgentRole = Some AgentRole.Coder
                  Directory = "/tmp/ws" }

            reconciler.MarkDirty(sid)
            reconciler.MarkDirty(sid)
            reconciler.MarkDirty(sid)
            do! drainMicrotasks 16

            equal 1 turnCount.Value
        }
