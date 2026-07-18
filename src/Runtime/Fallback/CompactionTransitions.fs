module Wanxiangshu.Runtime.Fallback.CompactionTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

    member this.GetLastHumanMessageId(sessionID: string) : string =
        (this.GetSession sessionID).LastHumanMessageId

    member this.SetLastHumanMessageId (sessionID: string) (messageId: string) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    LastHumanMessageId = messageId }
        )

    member this.ClearLastHumanMessageId(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with LastHumanMessageId = "" }))

    member this.SetActiveContinuationGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with ActiveContinuationGen = gen }))

    member this.GetActiveContinuationGeneration(sessionID: string) : int =
        (this.GetSession sessionID).ActiveContinuationGen

    member this.SetActiveContinuationCancelGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    ActiveContinuationCancelGen = gen }
        )

    member this.GetActiveContinuationCancelGeneration(sessionID: string) : int =
        (this.GetSession sessionID).ActiveContinuationCancelGen

    member this.SetActiveCompactionId(sessionID: string, id: string, ordinal: int) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    CompactionActiveId = id
                    CompactionActiveOrdinal = ordinal }
        )

    member this.GetActiveCompactionOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).CompactionActiveOrdinal

    member this.TryGetSettleInfo(sessionID: string, expectedCompactionID: string) : (string * int) option =
        let s = this.GetSession sessionID

        if s.CompactionActiveId = expectedCompactionID then
            Some(s.CompactionActiveId, s.CompactionActiveOrdinal)
        else
            None

    member this.ApplySettle(sessionID: string, expectedCompactionID: string) : bool =
        let s = this.GetSession sessionID

        if s.CompactionActiveId = expectedCompactionID then
            this.UpdateSession(
                sessionID,
                fun s ->
                    { s with
                        CompactionActiveId = ""
                        CompactionActiveOrdinal = 0
                        CompactionGeneration = 0
                        CompactionCompacted = false
                        CompactionContinuationObserved = false }
            )

            if s.Owner = SessionOwner.Compaction then
                this.UpdateSession(sessionID, (fun s -> { s with Owner = SessionOwner.NoOwner }))

            true
        else
            false

    member this.SetCompacted(sessionID: string, value: bool) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with CompactionCompacted = value }))
        this.TriggerStateChanged sessionID

    member this.IsCompacted(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionCompacted

    member this.SetCompactionContinuationObserved(sessionID: string, value: bool) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    CompactionContinuationObserved = value }
        )

        this.TriggerStateChanged sessionID

    member this.IsCompactionContinuationObserved(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionContinuationObserved

    member this.SetCompactionGeneration(sessionID: string, gen: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with CompactionGeneration = gen }))

    member this.GetCompactionGeneration(sessionID: string) : int =
        (this.GetSession sessionID).CompactionGeneration

    member this.GetActiveCompactionId(sessionID: string) : string =
        (this.GetSession sessionID).CompactionActiveId

    member this.MarkForceStopped(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with CompactionForceStopped = true }))

    member this.RemoveForceStopped(sessionID: string) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    CompactionForceStopped = false }
        )

    member this.IsForceStopped(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionForceStopped

    member this.SetTaskComplete(sessionID: string) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    Core =
                        { s.Core with
                            Lifecycle = FallbackLifecycle.TaskComplete } }
        )

    /// Atomically check and consume the compaction summary transform bypass flag.
    /// Returns true if the flag was set (meaning this messages.transform is the compaction summary request).
    member this.TryConsumeCompactionSummaryTransform(sessionID: string) : bool =
        let s = this.GetSession sessionID

        if s.CompactionSummaryTransformPending then
            this.UpdateSession(
                sessionID,
                (fun s ->
                    { s with
                        CompactionSummaryTransformPending = false })
            )

            true
        else
            false

    /// Idempotently clear the compaction summary transform bypass flag.
    member this.ClearCompactionSummaryTransformPending(sessionID: string) : unit =
        this.UpdateSession(
            sessionID,
            (fun s ->
                { s with
                    CompactionSummaryTransformPending = false })
        )

    member this.IsCompactionSummaryTransformPending(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionSummaryTransformPending
