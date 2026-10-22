namespace Wanxiangshu.Mission.Work

open Wanxiangshu.Foundation.Identity

module DeliveryAttemptSurface =

    let establishedDeliveryCallId (sessionId: string) (physicalUserMessageId: string) : string option =
        DeliveryAttemptLedger.tryFindEstablished
            (SessionId.create sessionId)
            (Some(PhysicalUserMessageId.create physicalUserMessageId))
        |> Option.map (fun (fence, _) -> ToolCallId.value fence.CallId)
