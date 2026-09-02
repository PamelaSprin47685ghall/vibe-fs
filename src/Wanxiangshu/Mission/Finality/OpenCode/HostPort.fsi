namespace Wanxiangshu.Mission.Finality.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Finality
open Wanxiangshu.OpenCode

/// Physical OpenCode adapter for Application Finality workflows.
module FinalityHostPort =

    val create:
        scope: ToolRuntimeScope ->
        managerSessionId: SessionId ->
        reviewerTimeoutMs: int ->
            FinalityReviewerPort * FinalityTreePort
