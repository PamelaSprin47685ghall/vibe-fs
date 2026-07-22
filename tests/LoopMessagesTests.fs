module Wanxiangshu.Tests.LoopMessagesTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.LoopMessages

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
    check "buildLoopMessage contains task field" (msg.Contains "fix login bug")

let buildLoopMessageContainsContext () =
    let msg = buildLoopMessage "do work" [ "step 1"; "step 2" ]
    check "buildLoopMessage contains context line" (msg.Contains "step 1")
    check "buildLoopMessage contains second context line" (msg.Contains "step 2")

// ── buildLoopCommandTemplate ──────────────────────────────────────────────────

let buildLoopCommandTemplateContainsCommand () =
    let tpl = buildLoopCommandTemplate "with-review" [ "rule line" ]
    check "buildLoopCommandTemplate contains command field" (tpl.Contains "with-review")

let buildLoopCommandTemplateContainsRules () =
    let tpl = buildLoopCommandTemplate "loop" [ "rule line" ]
    check "buildLoopCommandTemplate contains rule line" (tpl.Contains "rule line")

let run () =
    isEndVerdictAcceptsAccepted ()
    isEndVerdictAcceptsCancelled ()
    isEndVerdictNeedsRevisionNotEnd ()
    isEndVerdictRejectsTerminated ()
    buildLoopMessageContainsTaskField ()
    buildLoopMessageContainsContext ()
    buildLoopCommandTemplateContainsCommand ()
    buildLoopCommandTemplateContainsRules ()
