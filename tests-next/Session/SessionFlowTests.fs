namespace Wanxiangshu.Next.Tests

open System.Threading
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.SessionFlows
open Wanxiangshu.Next.Tests.SessionTestSupport

module SessionFlowTests =

    [<Fact>]
    let ``finishTodo_multi_round_advances_via_GetTodo`` () =
        task {
            let mutable unfinished = true
            let mutable stamp = 1L

            let continueWork () =
                session {
                    unfinished <- false
                    stamp <- stamp + 1L
                    return ()
                }

            let script =
                { createTestScript
                      { Unfinished = true
                        ProgressStamp = 1L }
                      continueWork with
                    GetTodo =
                        fun () ->
                            { Unfinished = unfinished
                              ProgressStamp = stamp }
                    GetProgressStamp = fun () -> stamp }

            let program = SessionFlows.finishTodo script
            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Ok(), res)
            Assert.False(unfinished)
            Assert.Equal(2L, stamp)
        }

    [<Fact>]
    let ``finishTodo_NoProgress_when_unfinished_without_progress`` () =
        task {
            let script =
                createTestScript
                    { Unfinished = true
                      ProgressStamp = 10L }
                    (fun () -> session { return () })

            let program = SessionFlows.finishTodo script
            let! res = Flow.run script CancellationToken.None program

            match res with
            | Error(SessionError.NoProgress _) -> ()
            | _ -> Assert.Fail(sprintf "Expected NoProgress error but got %A" res)
        }

    [<Fact>]
    let ``passReview_does_not_NoProgress_when_review_passes_without_todo_change`` () =
        task {
            let mutable reviewRequired = true
            let mutable stamp = 10L

            let requestReview () =
                session {
                    reviewRequired <- false
                    stamp <- stamp + 1L
                    return ()
                }

            let script =
                { createTestScript
                      { Unfinished = false
                        ProgressStamp = 10L }
                      (fun () -> session { return () }) with
                    GetReview =
                        fun () ->
                            { Required = reviewRequired
                              Round = 0
                              MaxRound = 3
                              Verdict = None }
                    GetProgressStamp = fun () -> stamp
                    RequestReview = requestReview }

            let program = SessionFlows.passReview script
            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Ok(), res)
            Assert.False(reviewRequired)
            Assert.Equal(11L, stamp)
        }
