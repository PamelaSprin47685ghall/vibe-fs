module Wanxiangshu.Tests.ArchitectureTestsOpencodeToolsReview

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.ArchitectureTestsSupport

let opencodeReviewUsesToolCopy () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolCopy" (code.Contains "ToolCopy")
    check "arch: Opencode ReviewTools uses submitReviewNotNeeded" (code.Contains "submitReviewNotNeeded")
    check "arch: Opencode ReviewTools uses opencodeSubmitReviewInProgress" (code.Contains "opencodeSubmitReviewInProgress")
    check "arch: Opencode ReviewTools replays task from session texts" (code.Contains "inferReviewTaskFromTexts")
    check "arch: Opencode ReviewTools must not inline you do not need review"
        (not (code.Contains "You do not need review. Just continue with your work."))

let opencodeReviewUsesFromOpencode () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    check "arch: Opencode ReviewTools opens ToolRuntimeContext" (code.Contains "ToolRuntimeContext")
    check "arch: Opencode ReviewTools submit uses fromOpencode" (code.Contains "fromOpencode")
    check "arch: Opencode ReviewTools uses runtime.Execution for session and directory"
        ((code.Contains "runtime.Execution.SessionId") && (code.Contains "runtime.Execution.Directory"))
    check "arch: Opencode ReviewTools must not extractToolContext in submit" (not (code.Contains "extractToolContext"))
    check "arch: Opencode ReviewTools must not Dyn.str tc sessionID" (not (code.Contains "Dyn.str tc \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str tc directory" (not (code.Contains "Dyn.str tc \"directory\""))
    check "arch: Opencode ReviewTools must not Dyn.str context sessionID" (not (code.Contains "Dyn.str context \"sessionID\""))
    check "arch: Opencode ReviewTools must not Dyn.str context directory" (not (code.Contains "Dyn.str context \"directory\""))
    check "arch: Opencode ReviewTools uses pluginDirectoryFromCtx" (code.Contains "pluginDirectoryFromCtx")
    check "arch: Opencode ReviewTools must not Dyn.str ctx directory" (not (code.Contains "Dyn.str ctx \"directory\""))

let opencodeReviewUsesReviewToolsCodec () =
    let code = requireFile "src/Opencode/ReviewTools.fs" |> nonCommentCode
    let codec = requireFile "src/Shell/ReviewToolsCodec.fs" |> nonCommentCode
    check "arch: ReviewToolsCodec defines decodeSubmitReviewArgs" (codec.Contains "let decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools opens ReviewToolsCodec" (code.Contains "ReviewToolsCodec")
    check "arch: Opencode ReviewTools submit uses decodeSubmitReviewArgs" (code.Contains "decodeSubmitReviewArgs")
    check "arch: Opencode ReviewTools submit must not Dyn.str args report" (not (code.Contains "Dyn.str args \"report\""))
    check "arch: ReviewToolsCodec defines decodeReturnReviewerArgs" (codec.Contains "let decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return uses decodeReturnReviewerArgs" (code.Contains "decodeReturnReviewerArgs")
    check "arch: Opencode ReviewTools return must not Dyn.str args verdict" (not (code.Contains "Dyn.str args \"verdict\""))
    check "arch: Opencode ReviewTools return uses submitReviewResult description" (code.Contains "submitReviewResult")
    check "arch: Opencode ReviewTools return uses Params.returnReviewerVerdict" (code.Contains "Params.returnReviewerVerdict")
    check "arch: Opencode ReviewTools opens ToolExecute" (code.Contains "ToolExecute")
    check "arch: Opencode ReviewTools submit decode uses wireDecodeFailure submit_review"
        (code.Contains "wireDecodeFailure \"submit_review\"")
    check "arch: Opencode ReviewTools return decode uses wireDecodeFailure return_reviewer"
        (code.Contains "wireDecodeFailure \"return_reviewer\"")
    check "arch: Opencode ReviewTools client failure uses wireEncodeToolError OpencodeClient"
        (code.Contains "wireEncodeToolError \"OpencodeClient\"")
    check "arch: Opencode ReviewTools must not ToolHelpers.formatDomainError submit_review"
        (not (code.Contains "formatDomainError \"submit_review\""))
    check "arch: Opencode ReviewTools must not ToolHelpers.formatDomainError return_reviewer"
        (not (code.Contains "formatDomainError \"return_reviewer\""))

let opencodeNudgeDoesNotReadReviewStoreForLoopState () =
    let code = requireFile "src/Opencode/NudgeEffect.fs" |> nonCommentCode
    check "arch: Opencode NudgeEffect must not read live review-state query" (not (code.Contains "isReviewActive"))
    check "arch: Opencode NudgeEffect rebuilds review state from history"
        (code.Contains "reviewTaskFromTexts")
