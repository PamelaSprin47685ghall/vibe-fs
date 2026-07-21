module Wanxiangshu.Tests.ReaderWriterLockTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogFile
open Wanxiangshu.Runtime.EventStore
open Wanxiangshu.Runtime.PromiseQueue
open Wanxiangshu.Runtime.RuntimeScope

[<Emit("performance.now()")>]
let private now () : float = jsNative

[<Global("globalThis")>]
let private globalThis: obj = jsNative

let testReaderWriterLockTimeoutRobustness () =
    promise {
        let lockObj = Wanxiangshu.Runtime.SessionReaderWriterLock()
        let mutable readError = None

        try
            let! _ = lockObj.EnqueueRead((fun () -> Promise.create (fun _ _ -> ())), timeoutMs = 50)
            ()
        with ex ->
            readError <- Some ex

        check "C10 reader timeout" (Option.isSome readError && readError.Value.Message.Contains("ReaderLockTimeout"))

        let mutable writeError = None

        try
            let! _ = lockObj.EnqueueWrite((fun () -> Promise.create (fun _ _ -> ())), timeoutMs = 50)
            ()
        with ex ->
            writeError <- Some ex

        check
            "C11 writer timeout"
            (Option.isSome writeError
             && writeError.Value.Message.Contains("WriterLockTimeout"))

        let! writeResult = lockObj.EnqueueWrite(fun () -> Promise.lift "write success")
        equal "write lock should acquire successfully after reader/writer timeout" "write success" writeResult
        lockObj.Dispose()
    }

let testHangInjections () =
    promise {
        let queue = SerialQueue()
        let mutable firstError = None

        try
            let! _ = queue.Enqueue((fun () -> Promise.create (fun _ _ -> ())), timeoutMs = 50)
            ()
        with ex ->
            firstError <- Some ex

        check "C14 queue timeout" (Option.isSome firstError && firstError.Value.Message.Contains("TimeoutError"))
        check "C14 queue poisoned" queue.Poisoned

        let mutable secondError = None

        try
            let! _ = queue.Enqueue(fun () -> Promise.lift "ok")
            ()
        with ex ->
            secondError <- Some ex

        check "C14 subsequent fails" (Option.isSome secondError && secondError.Value.Message.Contains("QueuePoisoned"))
    }

let testLateCompletionFence () =
    promise {
        let! dir = mkdtempAsync "eventlog-late-"
        let path = eventPath dir
        do! writeFileAsync path ""

        let mutable runWrite = true

        let mockAppend (filePath: string) (ev: WanEvent) =
            promise {
                do! Promise.sleep 200

                if runWrite then
                    do! appendFileAsync filePath (wanEventToLine ev + "\n")
            }

        let store = EventLogStore(dir, appendLineOverride = mockAppend, timeoutMs = 50)

        let ev =
            { V = 1
              Session = "s1"
              Kind = "test"
              At = ""
              Payload = Map []
              EventId = None
              WriterId = None
              Sequence = None
              Checksum = None }

        let! res = store.AppendEvent ev

        match res with
        | Error msg ->
            check "call receives timeout error" (msg.Contains("TimeoutError") || msg.Contains("QueuePoisoned"))
            runWrite <- false
        | Ok _ -> failwith "expected timeout error"

        check "store queue is poisoned" store.Poisoned
        check "store queue generation incremented" (store.Generation > 0)

        // A subsequent write must fail immediately without even attempting I/O
        let! res2 = store.AppendEvent ev

        match res2 with
        | Error msg -> check "subsequent call receives queue poisoned error" (msg.Contains("QueuePoisoned"))
        | Ok _ -> failwith "expected queue poisoned error"

        let! text = readFileAsync path "utf-8"
        equal "physical file remains empty despite late completion" "" text
        do! rmAsync dir
    }

let testReaderLockAbort () =
    promise {
        let lockObj = Wanxiangshu.Runtime.SessionReaderWriterLock()
        let mutable handlers = []

        let signal =
            {| addEventListener =
                System.Action<string, unit -> unit>(fun event handler ->
                    if event = "abort" then
                        handlers <- handler :: handlers)
               removeEventListener = System.Action<string, unit -> unit>(fun _ _ -> ()) |}

        let mutable caught = None

        let p =
            lockObj.EnqueueRead((fun () -> Promise.create (fun _ _ -> ())), abortSignal = signal)

        do! Promise.sleep 10
        handlers |> List.iter (fun h -> h ())

        try
            let! _ = p
            ()
        with ex ->
            caught <- Some ex

        check "C12 reader lock aborted" (Option.isSome caught && caught.Value.Message.Contains("AbortError"))
        lockObj.Dispose()
    }

let testC13MockHangs () =
    promise {
        let lockObj = Wanxiangshu.Runtime.SessionReaderWriterLock()
        let mutable readerStarted = false

        let readerPromise =
            lockObj.EnqueueRead(fun () ->
                promise {
                    readerStarted <- true
                    do! Promise.sleep 1000
                })

        do! Promise.sleep 10
        check "reader active" readerStarted
        let mutable writeError = None

        try
            let! _ = lockObj.EnqueueWrite((fun () -> Promise.lift "write"), timeoutMs = 50)
            ()
        with ex ->
            writeError <- Some ex

        check
            "writer timed out waiting for reader"
            (Option.isSome writeError
             && writeError.Value.Message.Contains("WriterLockTimeout"))

        lockObj.Dispose()
    }

let testInitStateSuccessAndDoubleAssign () =
    promise {
        let scope = RuntimeScope()
        equal "initial state Uninitialized" Uninitialized scope.InitState

        let mutable resolveInit = fun () -> ()
        let initPromise = Promise.create (fun r _ -> resolveInit <- r)
        scope.OnInit <- Some(fun _ -> initPromise)

        let mutable assignError = None

        try
            scope.OnInit <- Some(fun _ -> Promise.lift ())
        with ex ->
            assignError <- Some ex

        check
            "OnInit single-assignment"
            (Option.isSome assignError
             && assignError.Value.Message.Contains("OnInit can only be assigned once"))

        scope.TriggerInit("mock-workspace")

        match scope.InitState with
        | Initializing _ -> check "state is Initializing" true
        | _ -> failwith "Expected Initializing state"

        resolveInit ()
        do! scope.WaitInit()
        equal "state becomes Ready" Ready scope.InitState
    }

let testInitStateRejectAndReinit () =
    promise {
        let scope = RuntimeScope()
        scope.OnInit <- Some(fun _ -> Promise.reject (exn "mock init fail"))
        scope.TriggerInit("mock-workspace")
        let mutable waitError = None

        try
            do! scope.WaitInit()
        with ex ->
            waitError <- Some ex

        check
            "WaitInit throws on reject"
            (Option.isSome waitError && waitError.Value.Message.Contains("mock init fail"))

        match scope.InitState with
        | Degraded err -> check "state is Degraded" (err.Contains("mock init fail"))
        | _ -> failwith "Expected Degraded state"

        let mutable reinitCalled = false
        scope.OnInit <- None
        scope.OnInit <- Some(fun _ -> promise { reinitCalled <- true })
        scope.TriggerInit("mock-workspace")

        match scope.InitState with
        | Initializing _ -> check "TriggerInit during Degraded re-initializes" true
        | _ -> failwith "Expected Initializing state"
    }

let testInitStateWatchdogTimeout () =
    promise {
        let originalPerformance = globalThis?performance

        try
            let mutable fakeTime = 100.0
            globalThis?performance <- {| now = fun () -> fakeTime |}

            let scope = RuntimeScope()
            scope.OnInit <- Some(fun _ -> Promise.create (fun _ _ -> ()))
            scope.TriggerInit("mock-workspace")

            fakeTime <- 10200.0
            let mutable timeoutError = None

            try
                do! scope.WaitInit()
            with ex ->
                timeoutError <- Some ex

            check
                "watchdog throws Timeout"
                (Option.isSome timeoutError
                 && timeoutError.Value.Message.Contains("watchdog triggered"))

            match scope.InitState with
            | Degraded err -> check "state is Degraded on timeout" (err.Contains("watchdog triggered"))
            | _ -> failwith "Expected Degraded state"
        finally
            globalThis?performance <- originalPerformance
    }

let run () =
    promise {
        do! testReaderWriterLockTimeoutRobustness ()
        do! testHangInjections ()
        do! testLateCompletionFence ()
        do! testReaderLockAbort ()
        do! testC13MockHangs ()
        do! testInitStateSuccessAndDoubleAssign ()
        do! testInitStateRejectAndReinit ()
        do! testInitStateWatchdogTimeout ()
    }
