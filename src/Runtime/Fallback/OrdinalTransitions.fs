module Wanxiangshu.Runtime.Fallback.OrdinalTransitions

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

type FallbackRuntimeStore with
    member this.GetSessionGeneration(sessionID: string) : int =
        (this.GetSession sessionID).SessionGeneration

    member this.SetSessionGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, setSessionGeneration gen)

    member this.GetCancelGeneration(sessionID: string) : int =
        (this.GetSession sessionID).CancelGeneration

    member this.SetCancelGeneration (sessionID: string) (gen: int) : unit =
        this.UpdateSession(sessionID, setCancelGeneration gen)

    member this.IncrementCancelGeneration(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', n = incrementCancelGeneration s
                next <- n
                s'
        )

        next

    member this.GetHumanTurnOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).HumanTurnOrdinal

    member this.SetHumanTurnOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, setHumanTurnOrdinal ordinal)

    member this.IncrementHumanTurnOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', n = incrementHumanTurnOrdinal s
                next <- n
                s'
        )

        next

    member this.GetContinuationOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).ContinuationOrdinal

    member this.SetContinuationOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, setContinuationOrdinal ordinal)

    member this.IncrementContinuationOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', n = incrementContinuationOrdinal s
                next <- n
                s'
        )

        next

    member this.GetNudgeOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).NudgeOrdinal

    member this.SetNudgeOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, setNudgeOrdinal ordinal)

    member this.IncrementNudgeOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', n = incrementNudgeOrdinal s
                next <- n
                s'
        )

        next

    member this.GetCompactionOrdinal(sessionID: string) : int =
        (this.GetSession sessionID).CompactionOrdinal

    member this.SetCompactionOrdinal (sessionID: string) (ordinal: int) : unit =
        this.UpdateSession(sessionID, setCompactionOrdinal ordinal)

    member this.IncrementCompactionOrdinal(sessionID: string) : int =
        let mutable next = 0

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', n = incrementCompactionOrdinal s
                next <- n
                s'
        )

        next
