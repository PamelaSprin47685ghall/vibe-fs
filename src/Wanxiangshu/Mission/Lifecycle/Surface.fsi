namespace Wanxiangshu.Mission.Lifecycle

open System.Threading.Tasks

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
    /// Seed one opened work (WorkOpened + first IncumbencyOpened) into a live workspace store.
    val seedOpen: store: obj -> kind: string -> workId: string -> root: string -> head: int64 -> Task<obj>
    /// Append the one review for the active incumbency of a seeded work.
    val seedReview: store: obj -> workId: string -> outcome: string -> Task<obj>
    /// Drive one deliver for a seeded work through the unified kernel.
    val seedDeliver: store: obj -> workId: string -> providerRunId: string -> toolCallId: string -> head: int64 -> Task<obj>
    /// Seed the illegal fact "confirmation without review" directly.
    val seedConfirmation: store: obj -> workId: string -> providerRunId: string -> toolCallId: string -> Task<obj>
    /// Read-only folded view of one seeded work.
    val workView: store: obj -> workId: string -> obj
