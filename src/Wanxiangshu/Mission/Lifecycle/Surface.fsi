namespace Wanxiangshu.Mission.Lifecycle

module LifecycleSurface =
    val authorityInitial: workId: string -> string
    val encodeOpening: kind: string -> workId: string -> root: string -> head: int64 -> string
    val readProbe: text: string -> obj
    val reviewProbe: text: string -> incumbencyId: string -> outcome: string -> obj
    val finalPromotionProbe: text: string -> obj
    val deliverProbe: text: string -> providerRunId: string -> toolCallId: string -> head: int64 -> obj
    val replayProbe: text: string -> providerRunId: string -> toolCallId: string -> obj
    val detachedOpeningProbe: text: string -> obj
    val commitProbe: name: string -> string
    val decodeDocument: text: string -> obj
    val advance: workId: string -> prior: string -> expected: string -> next: string -> obj
