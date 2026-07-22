namespace Wanxiangshu.Next.Tests

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Session
open Wanxiangshu.Next.Session.SessionFlows

module SessionTestSupport =

    let createTestScript (todo: TodoView) (continueWork: unit -> SessionFlow<unit>) =
        { GetTodo = fun () -> todo
          GetReview =
            fun () ->
                { Required = false
                  Round = 0
                  MaxRound = 3
                  Verdict = None }
          GetProgressStamp = fun () -> todo.ProgressStamp
          Config =
            { FallbackModels = [ "model1"; "model2" ]
              MaxRetriesPerModel = 2
              MaxInvalidRetries = 2 }
          ContinueWork = continueWork
          RequestReview = fun () -> session { return () }
          Finish = fun () -> session { return CompletedSession "ok" } }
