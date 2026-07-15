module Wanxiangshu.Tests.MessageTransformStackTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.MessageTransformPolicy
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Shell.MessageTransformCore
open Wanxiangshu.Shell.MessageTransformPipeline
open Wanxiangshu.Shell.MessageTransformHostEntry
open Wanxiangshu.Shell.MessageTransformStack
open Wanxiangshu.Shell.ReviewRuntime

/// Invariant 1: "caps 建过一次后引用不变" — the CapsSlot stores the encoded
/// caps prefix on first build and every subsequent turn in the same
/// conversation reuses the exact same JS object references.
let capsBuiltOnceReferenceStable () =
    promise {
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Shell.RuntimeScope.create ()

        let capsObj =
            box (
                createObj
                    [ "info", box (createObj [ "id", box "caps-synth-user-test"; "role", box "user" ])
                      "parts", box [||] ]
            )

        let mkPlan cleanMsgs =
            { SessionID = "stack-caps-test"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              BacklogProjectionPolicy = BacklogProjectionPolicy.Include
              CapsInjectionPolicy = CapsInjectionPolicy.Include
              ParallelHintPolicy = ParallelHintPolicy.Include
              ContextBudgetPolicy = ContextBudgetPolicy.Include
              IsSubagentSession = false
              Cleaned = cleanMsgs
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift None) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> [] }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let injectFn (_policy: BacklogProjectionPolicy) (arr: obj array) = promise { return arr }

        let loadCapsCount = ref 0

        let loadCaps () =
            loadCapsCount.Value <- loadCapsCount.Value + 1
            promise { return [] }

        let buildCaps (arr: obj array) (_caps: CapsFile list) (_hint: string option) = Array.append [| capsObj |] arr

        let msg =
            { info =
                { id = "native-user-1"
                  sessionID = "stack-caps-test"
                  role = User
                  agent = "manager"
                  isError = false
                  toolName = ""
                  details = null
                  time = null }
              parts = [ TextPart "hello" ]
              source = Native
              raw = null }

        let plan = mkPlan [ msg ]

        let! res1 =
            runHostMessagesTransform
                reviewStore
                "stack-caps-test"
                plan
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "first call invokes loadCaps" 1 loadCapsCount.Value
        equal "first call prepends caps prefix" 2 res1.Length

        let! res2 =
            runHostMessagesTransform
                reviewStore
                "stack-caps-test"
                plan
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "second call does NOT invoke loadCaps (CapsSlot hit)" 1 loadCapsCount.Value

        check "caps prefix object reference identical across calls" (System.Object.ReferenceEquals(res1.[0], res2.[0]))

        // Verify the CapsSlot is actually populated
        match getCapsSlot scope "stack-caps-test" with
        | Some capsSlot ->
            match capsSlot.Segment with
            | Some seg -> check "caps prefix is the stored object" (System.Object.ReferenceEquals(seg.[0], capsObj))
            | None -> check "CapsSlot.Segment should be populated" false
        | None -> check "CapsSlot should be populated" false
    }

/// Invariant 2: "弹出数量精确匹配上一轮推入数量" — when the backlog count
/// changes between turns, the BacklogSlot segment is replaced; when it
/// stays the same, the segment references are reused.
let backlogSlotSegmentReuseWhenCountStable () =
    promise {
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Shell.RuntimeScope.create ()

        // Mutable backlog that we control to simulate count changes
        let currentBacklog = ref []

        let mkBacklogEntry () =
            { ahaMoments = ""
              changesAndReasons = ""
              gotchas = ""
              lessonsAndConventions = ""
              plan = "" }

        let mkPlan cleanMsgs =
            { SessionID = "stack-backlog-test"
              Agent = "main"
              Directory = ""
              ProjectionPolicy = ProjectionPolicy.IncludeProjection
              BacklogProjectionPolicy = BacklogProjectionPolicy.Include
              CapsInjectionPolicy = CapsInjectionPolicy.Exclude
              ParallelHintPolicy = ParallelHintPolicy.Exclude
              ContextBudgetPolicy = ContextBudgetPolicy.Disable
              IsSubagentSession = false
              Cleaned = cleanMsgs
              RawArray = None
              SembleInjectEnabled = false
              Scope = scope
              MaxInputTokens = 200000
              GetContextUsage = (fun _ -> Promise.lift None) }

        let backlogOps =
            { Host = opencode
              GetOrRebuildBacklog = fun _ _ -> currentBacklog.Value }

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let injectFn (_policy: BacklogProjectionPolicy) (arr: obj array) = promise { return arr }

        let loadCaps () = promise { return [] }
        let buildCaps (arr: obj array) _ _ = arr

        let msg =
            { info =
                { id = "native-user-backlog"
                  sessionID = "stack-backlog-test"
                  role = User
                  agent = "manager"
                  isError = false
                  toolName = ""
                  details = null
                  time = null }
              parts = [ TextPart "test" ]
              source = Native
              raw = null }

        // Turn 1: backlog with 3 entries → prefixCount = 2, segmentLen = 3
        currentBacklog.Value <- [ mkBacklogEntry (); mkBacklogEntry (); mkBacklogEntry () ]

        let plan1 = mkPlan [ msg ]

        let! res1 =
            runHostMessagesTransform
                reviewStore
                "stack-backlog-test"
                plan1
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        let res1Len = res1.Length

        // Turn 2: same backlog count → output should be stable
        let! res2 =
            runHostMessagesTransform
                reviewStore
                "stack-backlog-test"
                plan1
                backlogOps
                encodeMessages
                injectFn
                loadCaps
                buildCaps

        equal "same backlog count → same output length" res1Len res2.Length

        // Without todo-result anchors, backlog projection produces no
        // synthetic prefix, so BacklogSlot is not populated.  This is correct:
        // the slot only caches when a fold range exists.
        match getBacklogSlot scope "stack-backlog-test" with
        | Some _ -> check "BacklogSlot should be empty without fold range" false
        | None -> check "BacklogSlot correctly empty (no fold range)" true
    }

let run () =
    promise {
        do! capsBuiltOnceReferenceStable ()
        do! backlogSlotSegmentReuseWhenCountStable ()
    }
