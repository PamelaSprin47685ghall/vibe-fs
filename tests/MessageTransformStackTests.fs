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

/// Invariant 1: "caps 建过一次后引用不变" — the CapsSlot stores the encoded
/// caps prefix on first build and every subsequent turn in the same
/// conversation reuses the exact same JS object references.
let capsBuiltOnceReferenceStable () =
    promise {
        let reviewStore = createReviewStore ()
        let scope = Wanxiangshu.Runtime.RuntimeScope.create ()

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

        let encodeMessages (msgs: Message<obj> list) = msgs |> List.map box |> List.toArray

        let injectFn (_policy: ProjectionPolicy) (arr: obj array) = promise { return arr }

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
            runHostMessagesTransform reviewStore "stack-caps-test" plan encodeMessages injectFn loadCaps buildCaps

        equal "first call invokes loadCaps" 1 loadCapsCount.Value
        equal "first call prepends caps prefix" 2 res1.Length

        let! res2 =
            runHostMessagesTransform reviewStore "stack-caps-test" plan encodeMessages injectFn loadCaps buildCaps

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

let run () =
    promise { do! capsBuiltOnceReferenceStable () }
