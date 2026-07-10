module Wanxiangshu.Tests.FallbackInjectionFoldTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.FallbackInjectionFold
open Wanxiangshu.Kernel.EventLog.Fold

let private ev (session: string) (kind: string) (payload: Map<string, string>) =
    { V = 1
      Session = session
      Kind = kind
      At = ""
      Payload = payload }

let foldFallbackInjection_empty () =
    let s = foldFallbackInjection "s1" []
    check "empty: InjectedModel is None" s.InjectedModel.IsNone
    check "empty: InjectedAt is None" s.InjectedAt.IsNone
    check "empty: InjectedCount is 0" (s.InjectedCount = 0)

let foldFallbackInjection_singleEvent () =
    let events =
        [ ev
              "s1"
              eventKindFallbackContinueInjected
              (Map [ "model", "openai/gpt-5"; "agent", "coder"; "at", "1700000000000" ]) ]

    let s = foldFallbackInjection "s1" events
    equal "single: InjectedModel" (Some "openai/gpt-5") s.InjectedModel
    equal "single: InjectedAt" (Some 1700000000000L) s.InjectedAt
    equal "single: InjectedCount" 1 s.InjectedCount

let foldFallbackInjection_multipleEventsAccumulate () =
    let events =
        [ ev "s1" eventKindFallbackContinueInjected (Map [ "model", "openai/gpt-5"; "agent", "coder"; "at", "1000" ])
          ev
              "s1"
              eventKindFallbackContinueInjected
              (Map [ "model", "anthropic/claude-3"; "agent", "coder"; "at", "2000" ]) ]

    let s = foldFallbackInjection "s1" events
    equal "multi: InjectedModel last wins" (Some "anthropic/claude-3") s.InjectedModel
    equal "multi: InjectedAt last wins" (Some 2000L) s.InjectedAt
    equal "multi: InjectedCount" 2 s.InjectedCount

let foldFallbackInjection_crossSessionIsolation () =
    let events =
        [ ev "s1" eventKindFallbackContinueInjected (Map [ "model", "openai/gpt-5"; "agent", "coder"; "at", "1000" ])
          ev
              "s2"
              eventKindFallbackContinueInjected
              (Map [ "model", "anthropic/claude-3"; "agent", "coder"; "at", "2000" ]) ]

    let s1 = foldFallbackInjection "s1" events
    let s2 = foldFallbackInjection "s2" events

    equal "s1 InjectedModel" (Some "openai/gpt-5") s1.InjectedModel
    equal "s1 InjectedCount" 1 s1.InjectedCount
    equal "s2 InjectedModel" (Some "anthropic/claude-3") s2.InjectedModel
    equal "s2 InjectedCount" 1 s2.InjectedCount

let foldFallbackInjection_nonFallbackEventIgnored () =
    let events =
        [ ev "s1" eventKindLoopActivated (Map [ "task", "ship" ])
          ev "s1" eventKindFallbackContinueInjected (Map [ "model", "openai/gpt-5"; "agent", "coder"; "at", "1000" ]) ]

    let s = foldFallbackInjection "s1" events
    equal "loop_activated did not pollute" (Some "openai/gpt-5") s.InjectedModel
    equal "count only fallback events" 1 s.InjectedCount

let applyEventIntegratesFallbackInjection () =
    let st = emptySessionState ()

    let e =
        ev "s1" eventKindFallbackContinueInjected (Map [ "model", "openai/gpt-5"; "agent", "coder"; "at", "1000" ])

    let st2 = applyEvent st e
    equal "SessionState.FallbackInjection.InjectedModel" (Some "openai/gpt-5") st2.FallbackInjection.InjectedModel
    equal "SessionState.FallbackInjection.InjectedCount" 1 st2.FallbackInjection.InjectedCount

let run () =
    foldFallbackInjection_empty ()
    foldFallbackInjection_singleEvent ()
    foldFallbackInjection_multipleEventsAccumulate ()
    foldFallbackInjection_crossSessionIsolation ()
    foldFallbackInjection_nonFallbackEventIgnored ()
    applyEventIntegratesFallbackInjection ()
