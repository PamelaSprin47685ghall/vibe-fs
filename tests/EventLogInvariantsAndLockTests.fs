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
open Wanxiangshu.Runtime.RuntimeScope

[<Import("readdir", "fs/promises")>]
let private readdirAsync (path: string) : JS.Promise<string[]> = jsNative

[<Import("readFile", "node:fs/promises")>]
let private readFileAsync (path: string) (encoding: string) : JS.Promise<string> = jsNative

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

let testTamperedChecksumTruncation () =
    promise {
        let! dir = mkdtempAsync "eventlog-tampered-"
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

        let tamperedEvent =
            { V = 1
              Session = "s1"
              Kind = eventKindLoopActivated
              At = ""
              Payload = Map [ "task", "tampered" ]
              EventId = Some "eid1"
              WriterId = Some "wid1"
              Sequence = Some 2
              Checksum = Some "wrong-checksum" }

        let tamperedLine = wanEventToLine tamperedEvent
        do! writeFileAsync path (good + "\n" + tamperedLine + "\n")
        let store = EventLogStore dir
        let! events = store.ReadAllEvents()
        equal "events length should be 2 (good + repair)" 2 events.Length
        let repairEvent = events |> List.find (fun e -> e.Kind = "event_log_repaired")

        equal
            "repair reason is Checksum verification failed"
            "Checksum verification failed"
            repairEvent.Payload.["reason"]

        do! rmAsync dir
    }

let run () =
    promise {
        do! testProjectionEquivalence ()
        do! testForensicRepairedEvent ()
        do! testTruncationCases ()
        do! testTamperedChecksumTruncation ()
    }
