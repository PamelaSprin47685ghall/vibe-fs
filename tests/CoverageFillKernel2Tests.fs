module Wanxiangshu.Tests.CoverageFillKernel2Tests

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.ReviewSession.Types

// ── Kernel.ReviewSession.Types ─────────────────────────────────────────────

let rsTypes () =
    let e = empty "s1" 1L
    equal "empty id" "s1" e.id
    equal "empty state" ReviewState.Inactive e.state
    equal "empty version" 0 e.version
    equal "empty parentId" None e.parentId
    let once = withTask "t1" e
    equal "withTask first version" 1 once.version
    equal "withTask first task" (Some "t1") once.originalTask
    let same = withTask "t1" once
    equal "withTask same unchanged" once.id same.id
    equal "withTask same version" once.version same.version
    let diff = withTask "t2" once
    equal "withTask diff version bumped" 2 diff.version
    equal "withTask diff task" (Some "t2") diff.originalTask
    let fb = withFeedback e "good"
    equal "withFeedback set" (Some "good") fb.lastFeedback
    let fbSame = withFeedback fb "good"
    equal "withFeedback same version" fb.version fbSame.version
    let fbNew = withFeedback fb "bad"
    equal "withFeedback new version" (fb.version + 1) fbNew.version
    equal "withFeedback new text" (Some "bad") fbNew.lastFeedback
    let withChild = addChild e "c1"
    equal "addChild new" ["c1"] withChild.childIds
    equal "addChild version bumped" (e.version + 1) withChild.version
    let dupChild = addChild withChild "c1"
    equal "addChild duplicate" ["c1"] dupChild.childIds
    equal "addChild dup version same" withChild.version dupChild.version


// ── Kernel.BacklogProjectionCore ───────────────────────────────────────────

let blpIsTodoResultFor () =
    let okTool = ToolPart("todowrite", "c1", Some { status = "completed"; output = ""; error = ""; input = null; operationAction = "" }, null)
    check "opencode completed" (isTodoResultFor Opencode okTool)
    let errTool = ToolPart("todowrite", "c1", Some { status = "error"; output = ""; error = "fail"; input = null; operationAction = "" }, null)
    check "opencode error not result" (not (isTodoResultFor Opencode errTool))
    let muxOk = ToolPart("todowrite", "c1", Some { status = "completed"; output = ""; error = ""; input = null; operationAction = "" }, null)
    check "mux completed" (isTodoResultFor Mux muxOk)
    let otherTool = ToolPart("other", "c1", Some { status = "completed"; output = ""; error = ""; input = null; operationAction = "" }, null)
    check "other tool not result" (not (isTodoResultFor Opencode otherTool))

let blpIsTodoErrorFor () =
    let errPart = ToolPart("todowrite", "c1", Some { status = "error"; output = ""; error = "bad"; input = null; operationAction = "" }, null)
    check "opencode error" (isTodoErrorFor Opencode errPart)
    let okPart = ToolPart("todowrite", "c1", Some { status = "completed"; output = ""; error = ""; input = null; operationAction = "" }, null)
    check "opencode ok not error" (not (isTodoErrorFor Opencode okPart))
    let muxErr = ToolPart("todowrite", "c1", Some { status = "error"; output = ""; error = "bad"; input = null; operationAction = "" }, null)
    check "mux error" (isTodoErrorFor Mux muxErr)
    let otherErr = ToolPart("other", "c1", Some { status = "error"; output = ""; error = "bad"; input = null; operationAction = "" }, null)
    check "other tool not error" (not (isTodoErrorFor Opencode otherErr))

let blpLastTodoErrorText () =
    let errPart = ToolPart("todowrite", "c1", Some { status = "error"; output = ""; error = "boom"; input = null; operationAction = "" }, null)
    let flat = [ { msgIndex = 0; partIndex = 0; isUser = false; part = errPart } ]
    equal "last error text" (Some "boom") (lastTodoErrorTextFor Opencode flat)
    let noError = ToolPart("todowrite", "c1", Some { status = "completed"; output = ""; error = ""; input = null; operationAction = "" }, null)
    let flatOk = [ { msgIndex = 0; partIndex = 0; isUser = false; part = noError } ]
    equal "no error text" None (lastTodoErrorTextFor Opencode flatOk)
    let textPart = TextPart "hi"
    let flatText = [ { msgIndex = 0; partIndex = 0; isUser = false; part = textPart } ]
    equal "text part no error" None (lastTodoErrorTextFor Opencode flatText)

let blpBuildBacklogText () =
    let backlog : BacklogEntry list = [ { report = "fix bug" }; { report = "add feat" } ]
    let text = buildBacklogText backlog ["q1"]
    check "text non-empty" (text <> "")
    check "text has report" (text.Contains "fix bug")
    let textErr = buildBacklogTextWithError backlog ["q1"] (Some "write failed")
    check "error notice present" (textErr.Contains "write failed")


let run () =
    rsTypes ()
    blpIsTodoResultFor ()
    blpIsTodoErrorFor ()
    blpLastTodoErrorText ()
    blpBuildBacklogText ()
