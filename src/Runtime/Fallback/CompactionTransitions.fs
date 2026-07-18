module Wanxiangshu.Runtime.Fallback.CompactionTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimeLeasePure

type FallbackRuntimeStore with
    member this.GetLastHumanMessageId(sessionID: string) : string =
        (this.GetSession sessionID).LastHumanMessageId

    member this.SetLastHumanMessageId (sessionID: string) (messageId: string) : unit =
        this.UpdateSession(sessionID, setLastHumanMessageId messageId)

    member this.ClearLastHumanMessageId(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearLastHumanMessageId)

    member this.GetActiveContinuationGeneration(sessionID: string) : int =
        (this.GetSession sessionID).ActiveContinuationGen

    member this.SetActiveContinuationGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, setActiveContinuationGeneration gen)

    member this.GetActiveContinuationCancelGeneration(sessionID: string) : int =
        (this.GetSession sessionID).ActiveContinuationCancelGen

    member this.SetActiveContinuationCancelGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, setActiveContinuationCancelGeneration gen)

    member this.GetActiveCompactionOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).CompactionActiveOrdinal

    member this.GetActiveCompactionId(sessionID: string) : string =
        (this.GetSession sessionID).CompactionActiveId

    member this.SetActiveCompactionId(sessionID: string, id: string, ordinal: int) : unit =
        this.UpdateSession(sessionID, setActiveCompactionId id ordinal)

    member this.TryGetSettleInfo(sessionID: string, expectedCompactionID: string) : (string * int) option =
        tryGetSettleInfo expectedCompactionID (this.GetSession sessionID)

    member this.ApplySettle(sessionID: string, expectedCompactionID: string) : bool =
        let mutable settled = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match applySettle expectedCompactionID s with
                | Some s' ->
                    settled <- true
                    s'
                | None -> s
        )

        settled

    member this.GetCompactionGeneration(sessionID: string) : int =
        (this.GetSession sessionID).CompactionGeneration

    member this.SetCompactionGeneration(sessionID: string, gen: int) : unit =
        this.UpdateSession(sessionID, setCompactionGeneration gen)

    member this.IsCompacted(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionCompacted

    member this.SetCompacted(sessionID: string, value: bool) : unit =
        this.Update(sessionID, setCompacted value)

    member this.IsCompactionContinuationObserved(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionContinuationObserved

    member this.SetCompactionContinuationObserved(sessionID: string, value: bool) : unit =
        this.Update(sessionID, setCompactionContinuationObserved value)

    member this.IsForceStopped(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionForceStopped

    member this.MarkForceStopped(sessionID: string) : unit =
        this.UpdateSession(sessionID, markForceStopped)

    member this.RemoveForceStopped(sessionID: string) : unit =
        this.UpdateSession(sessionID, removeForceStopped)

    member this.SetTaskComplete(sessionID: string) : unit =
        this.UpdateSession(sessionID, setTaskComplete)

    member this.TryConsumeCompactionSummaryTransform(sessionID: string) : bool =
        let mutable consumed = false

        this.UpdateSession(
            sessionID,
            fun s ->
                match tryConsumeCompactionSummaryTransform s with
                | Some s' ->
                    consumed <- true
                    s'
                | None -> s
        )

        consumed

    member this.IsCompactionSummaryTransformPending(sessionID: string) : bool =
        (this.GetSession sessionID).CompactionSummaryTransformPending

    member this.ClearCompactionSummaryTransformPending(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearCompactionSummaryTransformPending)
