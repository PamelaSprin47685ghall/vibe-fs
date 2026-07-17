module Wanxiangshu.Runtime.Fallback.OrdinalTransitions

open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

    member this.GetSessionGeneration(sessionID: string) : int =
        (this.GetSession sessionID).SessionGeneration

    member this.SetSessionGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with SessionGeneration = gen }))

    member this.GetCancelGeneration(sessionID: string) : int =
        (this.GetSession sessionID).CancelGeneration

    member this.SetCancelGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with CancelGeneration = gen }))

    member this.IncrementCancelGeneration(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                next <- s.CancelGeneration + 1
                { s with CancelGeneration = next }
        )

        next

    member this.GetHumanTurnOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).HumanTurnOrdinal

    member this.SetHumanTurnOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with HumanTurnOrdinal = ordinal }))

    member this.IncrementHumanTurnOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                next <- s.HumanTurnOrdinal + 1
                { s with HumanTurnOrdinal = next }
        )

        next

    member this.GetContinuationOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).ContinuationOrdinal

    member this.SetContinuationOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with ContinuationOrdinal = ordinal }))

    member this.IncrementContinuationOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                next <- s.ContinuationOrdinal + 1
                { s with ContinuationOrdinal = next }
        )

        next

    member this.GetNudgeOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).NudgeOrdinal

    member this.SetNudgeOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with NudgeOrdinal = ordinal }))

    member this.IncrementNudgeOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                next <- s.NudgeOrdinal + 1
                { s with NudgeOrdinal = next }
        )

        next

    member this.GetCompactionOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).CompactionOrdinal

    member this.SetCompactionOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with CompactionOrdinal = ordinal }))

    member this.IncrementCompactionOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                next <- s.CompactionOrdinal + 1
                { s with CompactionOrdinal = next }
        )

        next
