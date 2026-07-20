module Wanxiangshu.Tests.Wanxiangzhen.TestTypes

open Fable.Core
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope

type FakeState =
    { mutable mergeFfOnlyCalled: bool
      mutable mergeBaseTrueForFirstN: int
      mutable mergeBaseCallCount: int
      revParseRefResult: string
      revParseBranchResult: string
      statusClean: bool
      mutable createSymlinksCount: int
      mutable isPidAliveResult: bool
      mutable killPidCalled: bool
      mutable killPidPid: int option
      mutable killPidSignal: obj option
      mutable waitForPidDeathCalls: (int * int) list
      mutable startPollingCalls: (int * (unit -> unit)) list
      mutable stopPollingCalls: obj list
      mutable promptSessionCalls: (string * string) list

      mutable tryWorktreeAddCalls: (string * string * string * string) list
      mutable tryWorktreeRemoveForceCalls: (string * string) list
      mutable tryBranchDeleteForceCalls: (string * string) list
      mutable showRefExistsCalls: (string * string) list
      mutable revParseHeadCalls: string list
      mutable revParseRefCalls: (string * string) list
      mutable revParseBranchCalls: string list
      mutable isDetachedCalls: string list
      mutable statusIsCleanCalls: string list
      mutable mergeBaseIsAncestorCalls: (string * string * string) list
      mutable mergeFfOnlyCalls: (string * string) list
      mutable spawnSlaveCalls: (string * string * obj * string) list
      mutable revParseRefOverrides: Map<string, string>
      log: string list ref
      mutable orphanWarningSent: bool
      mutable mergeBaseOverride: (string -> string -> string -> bool) option
      mutable revParseRefOverride: (string -> string -> string) option
      mutable revParseBranchOverride: (string -> string) option
      mutable statusIsCleanOverride: (string -> bool) option
      mutable tryWorktreeAddOverride: (string -> string -> string -> string -> Result<string, string>) option
      mutable promptSessionOverride: (obj -> string -> string -> JS.Promise<unit>) option

      mutable getLatestSquadSessionIdOverride: (unit -> JS.Promise<string option>) option
      mutable getSquadDagOverride: (string -> JS.Promise<Dag>) option
      mutable getSquadSessionsOverride: (unit -> JS.Promise<Map<string, Dag>>) option
      mutable appendSquadEventCalls: SquadEvent list
      mutable appendWanEventCalls: WanEvent list
      mutable readWanEventsResult: WanEvent list
      mutable startPollingOverride: (int -> (unit -> unit) -> obj) option
      mutable stopPollingOverride: (obj -> unit) option
      mutable killPidOverride: (int -> obj -> unit) option
      mutable hasCommitsResult: bool
      mutable hasCommitsOverride: (string -> bool) option
      mutable randomSeed: int }

let mkFake () : FakeState =
    let log = ref []

    { mergeFfOnlyCalled = false
      mergeBaseTrueForFirstN = 1
      mergeBaseCallCount = 0
      revParseRefResult = "deadbeef"
      revParseBranchResult = "main"
      statusClean = true
      createSymlinksCount = 0
      isPidAliveResult = true
      killPidCalled = false
      killPidPid = None
      killPidSignal = None
      waitForPidDeathCalls = []
      startPollingCalls = []
      stopPollingCalls = []
      promptSessionCalls = []

      tryWorktreeAddCalls = []
      tryWorktreeRemoveForceCalls = []
      tryBranchDeleteForceCalls = []
      showRefExistsCalls = []
      revParseHeadCalls = []
      revParseRefCalls = []
      revParseBranchCalls = []
      isDetachedCalls = []
      statusIsCleanCalls = []
      mergeBaseIsAncestorCalls = []
      mergeFfOnlyCalls = []
      spawnSlaveCalls = []
      revParseRefOverrides = Map.empty
      log = log
      orphanWarningSent = false
      mergeBaseOverride = None
      revParseRefOverride = None
      revParseBranchOverride = None
      statusIsCleanOverride = None
      tryWorktreeAddOverride = None
      promptSessionOverride = None

      getLatestSquadSessionIdOverride = None
      getSquadDagOverride = None
      getSquadSessionsOverride = None
      appendSquadEventCalls = []
      appendWanEventCalls = []
      readWanEventsResult = []
      startPollingOverride = None
      stopPollingOverride = None
      killPidOverride = None
      hasCommitsResult = true
      hasCommitsOverride = None
      randomSeed = 42 }
