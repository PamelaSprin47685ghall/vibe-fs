module Wanxiangshu.Tests.EventLogInvariantsAndLockTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec
open Wanxiangshu.Runtime.EventLogRecovery
open Wanxiangshu.Runtime.EventStore
open Wanxiangshu.Runtime.EventLogFile
open Wanxiangshu.Runtime.ProjectionCache
open Wanxiangshu.Runtime.PromiseQueue

[<Import("readdir", "fs/promises")>]
let private readdirAsync (path: string) : JS.Promise<string[]> = jsNative

[<Import("readFile", "node:fs/promises")>]
let private readFileAsync (path: string) (encoding: string) : JS.Promise<string> = jsNative

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

let testProjectionEquivalence () =
    promise {
        let! dir = mkdtempAsync "eventlog-equiv-"
        let path = eventPath dir

        let good =
            wanEventToLine
                { V = 1
                  Session = "s1"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "ok" ]
                  EventId = None
                  WriterId = None
                  Sequence = None
                  Checksum = None }

        do! writeFileAsync path (good + "\n{broken\n" + good + "\n")
        let store = EventLogStore dir
        let! events1 = store.ReadAllEvents()
        let cache1 = ProjectionCache()

        for e in events1 do
            cache1.FoldWan e

        let! cleanEvents = readEventsFile path
        let cache2 = ProjectionCache()

        for e in cleanEvents do
            cache2.FoldWan e

        equal "cache 1 revision" cache1.Revision cache2.Revision
        equal "cache 1 states count" (cache1.GetAllSessionStates().Count) (cache2.GetAllSessionStates().Count)
        do! rmAsync dir
    }

let testForensicRepairedEvent () =
    promise {
        let! dir = mkdtempAsync "eventlog-forensic-"
        let path = eventPath dir

        let good =
            wanEventToLine
                { V = 1
                  Session = "s1"
                  Kind = eventKindLoopActivated
                  At = ""
                  Payload = Map [ "task", "ok" ]
                  EventId = None
                  WriterId = None
                  Sequence = None
                  Checksum = None }

        do! writeFileAsync path (good + "\n{broken\n" + good + "\n")
        let store = EventLogStore dir
        let! events = store.ReadAllEvents()
        let repairEventOpt = events |> List.tryFind (fun e -> e.Kind = "event_log_repaired")
        check "has repaired event" (Option.isSome repairEventOpt)
        let repairEvent = repairEventOpt.Value

        let keys =
            [ "badOffset"
              "removedBytes"
              "badLine"
              "reason"
              "tailHash"
              "repairVersion" ]

        for k in keys do
            check (sprintf "has %s" k) (repairEvent.Payload.ContainsKey k)

        let tailHash = repairEvent.Payload.["tailHash"]
        let recoveryDir = sprintf "%s/.wanxiangshu-recovery" dir
        let! recoveryFiles = readdirAsync recoveryDir
        let matchingFile = recoveryFiles |> Array.tryFind (fun f -> f.Contains(tailHash))
        check "recovery tail file exists on disk" (Option.isSome matchingFile)
        do! rmAsync dir
    }

let private testTruncationCasesPart1 (dir: string) (path: string) (line1: string) (ev1: WanEvent) =
    promise {
        do! writeFileAsync path ""
        do! repairAndTruncateFile dir path
        let! res1 = readEventsFile path
        equal "A1 empty" 0 res1.Length

        do! writeFileAsync path line1
        do! repairAndTruncateFile dir path
        let! res2 = readEventsFile path
        equal "A2 single complete" 1 res2.Length

        do! writeFileAsync path (wanEventToLine ev1)
        do! repairAndTruncateFile dir path
        let! res3 = readEventsFile path
        equal "A3 missing newline" 1 res3.Length

        do! writeFileAsync path (line1 + "{broken\n")
        do! repairAndTruncateFile dir path
        let! res4 = readEventsFile path
        equal "A4 corrupt tail" 2 res4.Length

        do! writeFileAsync path (line1 + "{broken\n" + line1)
        do! repairAndTruncateFile dir path
        let! res5 = readEventsFile path
        equal "A5 corrupt middle" 2 res5.Length
    }

let private testTruncationCasesPart2 (dir: string) (path: string) (line1: string) =
    promise {
        do! writeFileAsync path (line1.Replace("\n", "\r\n"))
        do! repairAndTruncateFile dir path
        let! res6 = readEventsFile path
        equal "A6 CRLF" 1 res6.Length

        let evChinese =
            { V = 1
              Session = "中文"
              Kind = "emoji🌟"
              At = ""
              Payload = Map []
              EventId = None
              WriterId = None
              Sequence = None
              Checksum = None }

        do! writeFileAsync path (wanEventToLine evChinese + "\n{broken中文\n")
        do! repairAndTruncateFile dir path
        let! res7 = readEventsFile path
        equal "A7 Chinese" 2 res7.Length

        do! writeFileAsync path ""
        do! repairAndTruncateFile dir path
        let! res8 = readEventsFile path
        equal "A8 zero length" 0 res8.Length

        do! writeFileAsync path (line1 + "{broken\n")
        do! repairAndTruncateFile dir path
        do! repairAndTruncateFile dir path
        let! res9 = readEventsFile path
        let repairs = res9 |> List.filter (fun e -> e.Kind = "event_log_repaired")
        equal "A9 & A12 repair event count" 1 repairs.Length

        do! writeFileAsync path "   \n\n   \n"
        do! repairAndTruncateFile dir path
        let! res10 = readEventsFile path
        equal "A10 whitespace" 0 res10.Length

        do! writeFileAsync path "{\"invalid\":true}\n"
        do! repairAndTruncateFile dir path
        let! res11 = readEventsFile path
        equal "A11 missing fields" 1 res11.Length
    }

let testTruncationCases () =
    promise {
        let! dir = mkdtempAsync "eventlog-trunc-"
        let path = eventPath dir

        let ev1 =
            { V = 1
              Session = "s"
              Kind = "k"
              At = ""
              Payload = Map []
              EventId = None
              WriterId = None
              Sequence = None
              Checksum = None }

        let line1 = wanEventToLine ev1 + "\n"
        do! testTruncationCasesPart1 dir path line1 ev1
        do! testTruncationCasesPart2 dir path line1
        do! rmAsync dir
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
        let queue = SerialQueue()
        let mutable triggerLateResolve = fun (_: string) -> ()
        let latePromise = Promise.create (fun resolve _ -> triggerLateResolve <- resolve)
        let mutable error = None

        try
            let! _ = queue.Enqueue((fun () -> latePromise), timeoutMs = 50)
            ()
        with ex ->
            error <- Some ex

        check "task timed out" (Option.isSome error)
        let timedOutGen = queue.Generation
        triggerLateResolve "late success"
        check "generation incremented" (timedOutGen > 0)
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

let run () =
    promise {
        do! testReaderWriterLockTimeoutRobustness ()
        do! testProjectionEquivalence ()
        do! testForensicRepairedEvent ()
        do! testTruncationCases ()
        do! testHangInjections ()
        do! testLateCompletionFence ()
        do! testReaderLockAbort ()
        do! testC13MockHangs ()
    }
