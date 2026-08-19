namespace Wanxiangshu.Mission.Review.Judgement

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module JudgementRequestIdentity =

    let private sessionPrefix (reviewerSessionId: SessionId) =
        let session = SessionId.value reviewerSessionId
        sprintf "%d:%s" session.Length session

    let key (reviewerSessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        let physical = PhysicalUserMessageId.value physicalUserMessageId
        sprintf "%s%d:%s" (sessionPrefix reviewerSessionId) physical.Length physical

    let belongsTo (reviewerSessionId: SessionId) (requestKey: string) =
        requestKey.StartsWith(sessionPrefix reviewerSessionId, System.StringComparison.Ordinal)
