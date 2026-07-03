module Wanxiangshu.Tests.LoopMessagesTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.LoopMessages

// ── Helpers ──────────────────────────────────────────────────────────────────

let private taskText t = $"---\ntask: {t}\n---"
let private verdictText v = $"---\nverdict: {v}\n---"
let private doubleCheckText = "---\ndouble-check: review-passed\n---"

// ── isEndVerdict ─────────────────────────────────────────────────────────────

let isEndVerdictAcceptsAccepted () =
    check "isEndVerdict accepted → true" (isEndVerdict "accepted")

let isEndVerdictAcceptsCancelled () =
    check "isEndVerdict cancelled → true" (isEndVerdict "cancelled")

let isEndVerdictNeedsRevisionNotEnd () =
    check "isEndVerdict needs_revision → false" (not (isEndVerdict "needs_revision"))

let isEndVerdictRejectsTerminated () =
    check "isEndVerdict terminated → false" (not (isEndVerdict "terminated"))

// ── buildLoopMessage ──────────────────────────────────────────────────────────

let buildLoopMessageContainsTaskField () =
    let msg = buildLoopMessage "fix login bug" [ "step 1"; "step 2" ]
    check "buildLoopMessage contains task field" (msg.Contains "task: fix login bug")

let buildLoopMessageContainsBody () =
    let msg = buildLoopMessage "do work" [ "step 1"; "step 2" ]
    check "buildLoopMessage contains body line" (msg.Contains "step 1")
    check "buildLoopMessage contains second body line" (msg.Contains "step 2")

// ── buildLoopCommandTemplate ──────────────────────────────────────────────────

let buildLoopCommandTemplateContainsCommand () =
    let tpl = buildLoopCommandTemplate "with-review" [ "body line" ]
    check "buildLoopCommandTemplate contains command field" (tpl.Contains "command: with-review")

let buildLoopCommandTemplateContainsBody () =
    let tpl = buildLoopCommandTemplate "with-review-precheck" [ "body line" ]
    check "buildLoopCommandTemplate contains body line" (tpl.Contains "body line")

// ── inferReviewTaskFromTexts ──────────────────────────────────────────────────

let inferReviewTaskFromTextsEmpty () =
    equal "empty → None" None (inferReviewTaskFromTexts [])

let inferReviewTaskFromTextsFromTask () =
    equal "task field → Some task" (Some "fix login") (inferReviewTaskFromTexts [ taskText "fix login" ])

let inferReviewTaskFromTextsCancelledClears () =
    equal "task then cancelled → None"
        None (inferReviewTaskFromTexts [ taskText "old task"; verdictText "cancelled" ])

let inferReviewTaskFromTextsAcceptedClears () =
    equal "task then accepted → None"
        None (inferReviewTaskFromTexts [ taskText "old task"; verdictText "accepted" ])

let inferReviewTaskFromTextsNeedsRevisionKeeps () =
    equal "task then needs_revision → keeps task"
        (Some "surviving task") (inferReviewTaskFromTexts [ taskText "surviving task"; verdictText "needs_revision" ])

let inferReviewTaskFromTextsNonFrontMatterKeeps () =
    equal "task then prose → keeps task"
        (Some "my task") (inferReviewTaskFromTexts [ taskText "my task"; "just some plain chat text" ])

let inferReviewTaskFromTextsTaskThenCancelled () =
    equal "task, cancelled, prose → None"
        None (inferReviewTaskFromTexts [ taskText "feature-x"; verdictText "cancelled"; "after cancellation" ])

let inferReviewTaskFromTextsTaskInSecondBlock () =
    let first = "---\nmode: chat\n---"
    let second = "---\ntask: second-task\n---"
    equal "task in second block activates"
        (Some "second-task") (inferReviewTaskFromTexts [ first + "\n" + second + "\nbody" ])

let inferReviewTaskFromTextsVerdictInSecondBlockClears () =
    let first = "---\ntask: old-task\n---"
    let second = "---\nverdict: accepted\n---"
    equal "verdict in second block clears task"
        None (inferReviewTaskFromTexts [ first + "\n" + second + "\nbody" ])

// ── hasDoubleCheckAnchor ──────────────────────────────────────────────────────

let hasDoubleCheckAnchorTrue () =
    check "hasDoubleCheckAnchor true" (hasDoubleCheckAnchor [ doubleCheckText ])

let hasDoubleCheckAnchorFalse () =
    check "hasDoubleCheckAnchor false" (not (hasDoubleCheckAnchor [ taskText "some work" ]))

// ── run ───────────────────────────────────────────────────────────────────────

let run () =
    isEndVerdictAcceptsAccepted ()
    isEndVerdictAcceptsCancelled ()
    isEndVerdictNeedsRevisionNotEnd ()
    isEndVerdictRejectsTerminated ()
    buildLoopMessageContainsTaskField ()
    buildLoopMessageContainsBody ()
    buildLoopCommandTemplateContainsCommand ()
    buildLoopCommandTemplateContainsBody ()
    inferReviewTaskFromTextsEmpty ()
    inferReviewTaskFromTextsFromTask ()
    inferReviewTaskFromTextsCancelledClears ()
    inferReviewTaskFromTextsAcceptedClears ()
    inferReviewTaskFromTextsNeedsRevisionKeeps ()
    inferReviewTaskFromTextsNonFrontMatterKeeps ()
    inferReviewTaskFromTextsTaskThenCancelled ()
    inferReviewTaskFromTextsTaskInSecondBlock ()
    inferReviewTaskFromTextsVerdictInSecondBlockClears ()
    hasDoubleCheckAnchorTrue ()
    hasDoubleCheckAnchorFalse ()
