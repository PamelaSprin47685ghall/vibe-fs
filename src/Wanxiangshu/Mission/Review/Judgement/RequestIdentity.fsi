namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module JudgementRequestIdentity =
    val key: reviewerSessionId: SessionId -> physicalUserMessageId: PhysicalUserMessageId -> string
    val belongsTo: reviewerSessionId: SessionId -> requestKey: string -> bool
