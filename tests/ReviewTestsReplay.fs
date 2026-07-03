module Wanxiangshu.Tests.ReviewTestsReplay

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.ReviewSession
open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.LoopMessages
open Wanxiangshu.Kernel.PromptFrontMatter

let disposeSessionTreeTerminatesAll () =
    let mutable verdicts : (string * ReviewResult) list = []
    let mutable suppressedOrder : string list = []
    let resolverFor id = fun result -> verdicts <- (id, result) :: verdicts
    let suppressorFor id = fun () -> suppressedOrder <- id :: suppressedOrder
    let effects =
        emptyEffects
        |> fun e -> setPending e "root" (resolverFor "root")
        |> fun e -> setPending e "child-a" (resolverFor "child-a")
        |> fun e -> setPending e "child-b" (resolverFor "child-b")
        |> fun e ->
            { e with
                abortSuppressors =
                    e.abortSuppressors
                    |> Map.add "root" (suppressorFor "root")
                    |> Map.add "child-a" (suppressorFor "child-a") }
    let next = disposeSessionTree effects [ "root"; "child-a"; "child-b" ]
    check "all resolvers fired" (verdicts |> List.length = 3)
    check "all verdicts are Terminated" (verdicts |> List.forall (fun (_, r) -> r = Terminated))
    check "suppressors fired only where present" (suppressedOrder |> List.length = 2)
    check "no pending resolvers remain" next.pendingResolutions.IsEmpty
    check "no suppressors remain" next.abortSuppressors.IsEmpty
    let next2 = disposeSessionTree next [ "ghost-1"; "ghost-2" ]
    check "disposing absent ids leaves pending empty" next2.pendingResolutions.IsEmpty
    check "disposing absent ids leaves suppressors empty" next2.abortSuppressors.IsEmpty

let inferReviewTaskFromTexts' () =
    let activate task =
        buildLoopMessage task [ "With-Review Mode is active. Complete the task above, then call submit_review with:" ]
    let accept = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult (Accepted "")
    let cancel = loopCancelledMessage
    let needsRevisionMsg = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult (NeedsRevision "fix the tests")
    let terminated = Wanxiangshu.Kernel.ReviewPrompts.formatReviewResult Terminated
    equal "empty -> None" None (inferReviewTaskFromTexts [])
    equal "only activate -> Some task" (Some "ship S1") (inferReviewTaskFromTexts [ activate "ship S1" ])
    equal "activate + accept -> None" None (inferReviewTaskFromTexts [ activate "ship S1"; accept ])
    equal "activate + cancel -> None" None (inferReviewTaskFromTexts [ activate "ship S1"; cancel ])
    equal "activate + needs_revision -> still active" (Some "ship S1") (inferReviewTaskFromTexts [ activate "ship S1"; needsRevisionMsg ])
    equal "activate + terminated -> still active" (Some "ship S1") (inferReviewTaskFromTexts [ activate "ship S1"; terminated ])
    equal "two activates no end -> last task" (Some "ship S2") (inferReviewTaskFromTexts [ activate "ship S1"; activate "ship S2" ])
    equal "activate + accept + activate -> second active" (Some "ship S2") (inferReviewTaskFromTexts [ activate "ship S1"; accept; activate "ship S2" ])
    equal "accept without activate -> None" None (inferReviewTaskFromTexts [ accept ])
    equal "prose mention of accepted does not end review" (Some "ship S1")
        (inferReviewTaskFromTexts [ activate "ship S1"; "I think your changes look accepted to me. With-Review Mode has ended, right?" ])
    equal "prose task line does not activate" None
        (inferReviewTaskFromTexts [ "Here is my plan:\ntask: refactor everything\nlet's go" ])
    let reviewerChildPrompt =
        Wanxiangshu.Kernel.ReviewPrompts.reviewerPrompt "worker task from parent" "self-reported changes" [ "src/a.fs" ]
    equal "reviewerPrompt task must not activate worker With-Review" None
        (inferReviewTaskFromTexts [ reviewerChildPrompt ])
    let reviewerVerdictPrompt =
        Wanxiangshu.Kernel.ReviewPrompts.reviewSubmissionVerdictPrompt "worker task" "report body" [ "b.fs" ]
    equal "front matter task: original_task must not activate worker loop" None
        (inferReviewTaskFromTexts [ reviewerVerdictPrompt ])

let parseFrontMatterScalars' () =
    let scalars = parseFrontMatterScalars (frontMatterPrompt [ yamlField "verdict" "needs_revision"; yamlField "feedback" "line one\n---\nline three" ] "Address the feedback above.")
    equal "scalar verdict parsed" (Some "needs_revision") (Map.tryFind "verdict" scalars)
    equal "block field parsed" (Some "line one\n---\nline three") (Map.tryFind "feedback" scalars)
    let multi = parseFrontMatterScalars (frontMatter [ yamlField "task" "do thing"; yamlField "verdict" "accepted" ])
    equal "first scalar" (Some "do thing") (Map.tryFind "task" multi)
    equal "second scalar" (Some "accepted") (Map.tryFind "verdict" multi)
    let block = parseFrontMatterScalars (frontMatter [ yamlField "task" "line one\nline two\n: [] {} \"quoted\"" ])
    equal "block scalar parsed" (Some "line one\nline two\n: [] {} \"quoted\"") (Map.tryFind "task" block)
    equal "plain prose → empty" Map.empty (parseFrontMatterScalars "just a normal message, no front matter")
    equal "no closing fence → empty" Map.empty (parseFrontMatterScalars "---\ntask: \"x\"\nnever closes")
    let indented = parseFrontMatterScalars "---\n  task: \"indented\"\n---"
    equal "indented task IS valid YAML top-level key" (Some "indented") (Map.tryFind "task" indented)