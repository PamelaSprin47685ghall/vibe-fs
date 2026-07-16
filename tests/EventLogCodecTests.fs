module Wanxiangshu.Tests.EventLogCodecTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventLogCodec

let roundtripLine () =
    let e =
        { V = 1
          Session = "sess-a"
          Kind = eventKindLoopActivated
          At = "2026-01-01T00:00:00Z"
          Payload = Map [ "task", "do thing" ] }

    let line = wanEventToLine e

    match tryParseEventLine line with
    | None -> check "parse roundtrip" false
    | Some parsed ->
        check "session" (parsed.Session = "sess-a")
        check "kind" (parsed.Kind = eventKindLoopActivated)
        check "task payload" (parsed.Payload |> Map.tryFind "task" = Some "do thing")

let parseRejectsGarbage () =
    check "garbage" (tryParseEventLine "not json").IsNone

    check
        "empty kind"
        (tryParseEventLine """{"v":1,"session":"s","kind":"","at":"","payload":{}}""")
            .IsNone

let run () =
    roundtripLine ()
    parseRejectsGarbage ()
