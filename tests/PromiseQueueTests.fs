module Wanxiangshu.Tests.PromiseQueueTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.PromiseQueue

type TestObserver() =
    let messages = ResizeArray<string>()
    interface IExceptionObserver with
        member _.OnException(ex: exn) =
            messages.Add(ex.Message)
    member _.Messages = messages

let serialQueueReportsExceptionToObserver () = promise {
    let observer = TestObserver()
    let queue = SerialQueue(observer = observer)
    // First task throws; second task must still run and report via observer.
    let failing = queue.Enqueue(fun () -> promise { failwith "boom" })
    let succeeding = queue.Enqueue(fun () -> promise { return 42 })
    let! result = succeeding
    do! failing |> Promise.catch (fun _ -> ())
    equal "second task result" 42 result
    equal "observer received one exception" 1 observer.Messages.Count
    equal "observer exception message" "boom" observer.Messages.[0]
}

let serialQueueContinuesWithoutObserver () = promise {
    let queue = SerialQueue()
    let failing = queue.Enqueue(fun () -> promise { failwith "kaboom" })
    let succeeding = queue.Enqueue(fun () -> promise { return 99 })
    let! result = succeeding
    do! failing |> Promise.catch (fun _ -> ())
    equal "second task result without observer" 99 result
}

let run () = promise {
    do! serialQueueReportsExceptionToObserver ()
    do! serialQueueContinuesWithoutObserver ()
}
