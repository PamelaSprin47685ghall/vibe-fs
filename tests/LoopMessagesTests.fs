module Wanxiangshu.Tests.LoopMessagesTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.LoopMessages

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
    let tpl = buildLoopCommandTemplate "loop" [ "body line" ]
    check "buildLoopCommandTemplate contains body line" (tpl.Contains "body line")

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
    hasDoubleCheckAnchorTrue ()
    hasDoubleCheckAnchorFalse ()
