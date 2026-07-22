namespace Wanxiangshu.Next.Tests

open System.Threading
open Xunit
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.SessionFlows
open Wanxiangshu.Next.Tests.SessionTestSupport

module SessionFallbackTests =

    [<Fact>]
    let ``Fallback_AcceptanceUnknown_does_not_switch_model`` () =
        task {
            let script =
                createTestScript
                    { Unfinished = true
                      ProgressStamp = 1L }
                    (fun () -> session { return () })

            let mutable calls = []

            let sendContinue (model: string) (attempt: int) : SessionFlow<SendOutcome> =
                session {
                    calls <- (model, attempt) :: calls
                    return AcceptanceUnknown("timeout", Some(MessageId.create "msg1"))
                }

            let program = Fallback.tryModels script sendContinue [ "modelA"; "modelB" ]
            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Error SessionError.PromptUncertain, res)
            Assert.Equal<(string * int) list>([ ("modelA", 1) ], List.rev calls)
        }

    [<Fact>]
    let ``Fallback_exhausts_models`` () =
        task {
            let script =
                { createTestScript
                      { Unfinished = true
                        ProgressStamp = 1L }
                      (fun () -> session { return () }) with
                    Config =
                        { FallbackModels = [ "m1"; "m2" ]
                          MaxRetriesPerModel = 2
                          MaxInvalidRetries = 2 } }

            let mutable callCount = 0

            let sendContinue (model: string) (attempt: int) : SessionFlow<SendOutcome> =
                session {
                    callCount <- callCount + 1
                    return Retryable "error"
                }

            let program = Fallback.continueWork script sendContinue

            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Error SessionError.FallbackExhausted, res)
            Assert.Equal(4, callCount)

            let emptyProgram = Fallback.tryModels script sendContinue []
            let! emptyRes = Flow.run script CancellationToken.None emptyProgram
            Assert.Equal(Error SessionError.FallbackExhausted, emptyRes)
        }

    [<Fact>]
    let ``Fallback_continueWork_calls_CommitTodoFrom`` () =
        task {
            let mutable committedOutcome = None

            let script =
                { createTestScript
                      { Unfinished = true
                        ProgressStamp = 1L }
                      (fun () -> session { return () }) with
                    Config =
                        { FallbackModels = [ "modelA" ]
                          MaxRetriesPerModel = 1
                          MaxInvalidRetries = 1 }
                    CommitTodoFrom =
                        fun outcome ->
                            session {
                                committedOutcome <- Some outcome
                                return ()
                            } }

            let sendContinue (model: string) (attempt: int) : SessionFlow<SendOutcome> =
                session { return Delivered(MessageId.create "msg_delivered") }

            let program = Fallback.continueWork script sendContinue
            let! res = Flow.run script CancellationToken.None program

            Assert.Equal(Ok(), res)

            match committedOutcome with
            | Some(Delivered msgId) -> Assert.Equal("msg_delivered", MessageId.value msgId)
            | _ -> Assert.Fail(sprintf "Expected Some (Delivered msg_delivered), got %A" committedOutcome)
        }
