namespace Wanxiangshu.Enforcer.Guidance

[<RequireQualifiedAccess>]
module DeliverySurface =
    val empty: obj
    val hasFullDelivered: tipName: string -> state: obj -> bool
    val apply: tipName: string -> presentation: obj -> state: obj -> obj
    val applyReanchor: state: obj -> obj
    val clear: state: obj -> obj
