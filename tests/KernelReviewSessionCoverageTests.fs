module Wanxiangshu.Tests.KernelReviewSessionCoverageTests

open Fable.Core
open Wanxiangshu.Tests.Assert
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
    equal "addChild new" [ "c1" ] withChild.childIds
    equal "addChild version bumped" (e.version + 1) withChild.version
    let dupChild = addChild withChild "c1"
    equal "addChild duplicate" [ "c1" ] dupChild.childIds
    equal "addChild dup version same" withChild.version dupChild.version

let run () = rsTypes ()
