namespace Wanxiangshu.Mission.Work

module DeliveryAttemptSurface =

    /// The latest established call on this physical input, from the live attempt ledger.
    val establishedDeliveryCallId: sessionId: string -> physicalUserMessageId: string -> string option
