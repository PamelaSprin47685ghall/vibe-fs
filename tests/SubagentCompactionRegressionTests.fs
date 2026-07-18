module Wanxiangshu.Tests.SubagentCompactionRegressionTests

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionTranscript
open Wanxiangshu.Tests.Assert

let private assistantMessage text =
    createObj
        [ "id" ==> "assistant-before-compaction"
          "info" ==> createObj [ "role" ==> "assistant" ]
          "parts" ==> [| box (createObj [ "type" ==> "text"; "text" ==> text ]) |] ]

let originalTurnAnchorSurvivesSyntheticCompactionContinuation () =
    let messages =
        [| createObj
               [ "id" ==> "original-user"
                 "info" ==> createObj [ "role" ==> "user" ]
                 "parts" ==> [||] ]
           assistantMessage "subagent result before compaction"
           createObj
               [ "id" ==> "synthetic-compaction-continuation"
                 "info" ==> createObj [ "role" ==> "user" ]
                 "parts" ==> [| box (createObj [ "synthetic" ==> true ]) |] ] |]

    match buildTurnEvidence messages (AnchorByUserMessageId "original-user") with
    | Ok evidence ->
        match evidence.Assistant with
        | AssistantSnapshot(_, _, text, _) ->
            equal "compaction keeps original turn evidence" "subagent result before compaction" text
        | other -> check ("expected assistant evidence, got " + string other) false
    | Error failure -> check failure.Message false

let run () =
    originalTurnAnchorSurvivesSyntheticCompactionContinuation ()
