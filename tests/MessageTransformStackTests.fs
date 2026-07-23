module Wanxiangshu.Tests.MessageTransformStackTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.MessageTransform.Plan
open Wanxiangshu.Runtime.MessageTransform.Pipeline
open Wanxiangshu.Runtime.MessageTransform.HostEntry
open Wanxiangshu.Runtime.MessageTransform.Stack
open Wanxiangshu.Runtime.ReviewRuntime

let private mkNativeUser (sessionID: string) (id: string) : Message<obj> =
    { info =
        { id = id
          sessionID = sessionID
          role = User
          agent = "manager"
          isError = false
          toolName = ""
          details = null
          time = null }
      parts = [ TextPart "hello" ]
      source = Native
      raw = null }

let private mkPlan
    (scope: Wanxiangshu.Runtime.RuntimeScope.RuntimeScope)
    (sessionID: string)
    (cleanMsgs: Message<obj> list)
    =
    { SessionID = sessionID
      Agent = "main"
      Directory = ""
      ProjectionPolicy = ProjectionPolicy.IncludeProjection
      CapsInjectionPolicy = CapsInjectionPolicy.Include
      ParallelHintPolicy = ParallelHintPolicy.Include
      IsSubagentSession = false
      Cleaned = cleanMsgs
      RawArray = None
      SembleInjectEnabled = false
      Scope = scope
      MaxInputTokens = 200000
      ModelKey = "openai/gpt-4o:default"
      LimitSource = "openai-session-model"
      ObserveLatestUsage = (fun () -> Promise.lift ()) }

let private encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray
let private injectFn (_policy: ProjectionPolicy) (arr: obj array) = promise { return arr }

let private mkCapsObj (id: string) =
    box (
        createObj
            [ "info", box (createObj [ "id", box id; "role", box "user" ])
              "parts", box [||] ]
    )

/// Invariant 1: "caps 建过一次后引用不变" — the CapsSlot stores the encoded
/// caps prefix on first build and every subsequent turn in the same
/// conversation reuses the exact same JS object references.
let capsBuiltOnceReferenceStable () =
    promise {
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Runtime.RuntimeScope.create ()
        let sessionID = "stack-caps-test"
        let capsObj = mkCapsObj "caps-synth-user-test"
        let loadCapsCount = ref 0

        let loadCaps () =
            loadCapsCount.Value <- loadCapsCount.Value + 1
            promise { return [] }

        let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) = Array.append [| capsObj |] arr

        let plan = mkPlan scope sessionID [ mkNativeUser sessionID "native-user-1" ]

        let! res1 = runHostMessagesTransform reviewStore sessionID plan encodeMessages injectFn loadCaps buildCaps

        equal "first call invokes loadCaps" 1 loadCapsCount.Value
        equal "first call prepends caps prefix" 2 res1.Length

        let! res2 = runHostMessagesTransform reviewStore sessionID plan encodeMessages injectFn loadCaps buildCaps

        equal "second call does NOT invoke loadCaps (CapsSlot hit)" 1 loadCapsCount.Value
        check "caps prefix object reference identical across calls" (System.Object.ReferenceEquals(res1.[0], res2.[0]))

        match getCapsSlot scope sessionID with
        | Some capsSlot ->
            match capsSlot.Segment with
            | Some seg -> check "caps prefix is the stored object" (System.Object.ReferenceEquals(seg.[0], capsObj))
            | None -> check "CapsSlot.Segment should be populated" false
        | None -> check "CapsSlot should be populated" false
    }

/// Compaction must drop CapsSlot so the next transform reloads from disk.
let capsInvalidatedAfterCompactionReloads () =
    promise {
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Runtime.RuntimeScope.create ()
        let sessionID = "stack-caps-invalidate"
        let capsObj1 = mkCapsObj "caps-synth-user-v1"
        let capsObj2 = mkCapsObj "caps-synth-user-v2"
        let loadCapsCount = ref 0
        let currentCaps = ref capsObj1

        let loadCaps () =
            loadCapsCount.Value <- loadCapsCount.Value + 1
            promise { return [] }

        let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) =
            Array.append [| currentCaps.Value |] arr

        let plan = mkPlan scope sessionID [ mkNativeUser sessionID "native-user-1" ]

        let! res1 = runHostMessagesTransform reviewStore sessionID plan encodeMessages injectFn loadCaps buildCaps

        equal "first loadCaps" 1 loadCapsCount.Value
        check "first prefix is capsObj1" (System.Object.ReferenceEquals(res1.[0], capsObj1))

        Wanxiangshu.Runtime.MessageTransform.CapsStage.invalidateCapsAfterCompaction scope sessionID
        currentCaps.Value <- capsObj2

        let! res2 = runHostMessagesTransform reviewStore sessionID plan encodeMessages injectFn loadCaps buildCaps

        equal "post-compaction reloads caps" 2 loadCapsCount.Value
        check "second prefix is rebuilt capsObj2" (System.Object.ReferenceEquals(res2.[0], capsObj2))
        check "prefix object changed after invalidation" (not (System.Object.ReferenceEquals(res1.[0], res2.[0])))
    }

let run () =
    promise {
        do! capsBuiltOnceReferenceStable ()
        do! capsInvalidatedAfterCompactionReloads ()
    }
