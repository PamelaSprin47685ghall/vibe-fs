namespace Wanxiangshu.Mission.Review.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Review

/// Process-local rendezvous between one active Finality review CE and JudgeTool.
/// Ownership and one pending TCS are physical resources only; first/second review
/// order lives exclusively in the CE that arms two one-shot judgement requests.
module ReviewJudgementInbox =

    val acquire: sessionId: SessionId -> Result<ReviewJudgementChannel, string>

    val isOwned: sessionId: SessionId -> bool

    val tryDeliver:
        judgement: ReviewJudgement ->
        accept: (unit -> unit) ->
        challenge: (unit -> unit) ->
        reject: (unit -> unit) ->
            unit option
