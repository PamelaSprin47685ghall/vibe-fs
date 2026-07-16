module Wanxiangshu.Runtime.Fallback.GateTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.FallbackRuntimeFlags
open Wanxiangshu.Runtime.Fallback.GateState
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with
    member this.SetNudgeActive (sessionID: string) (value: bool) : unit =
        this.ActiveGates <- setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.NudgeActive value
        this.TriggerStateChanged sessionID

    member this.IsNudgeActive(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.NudgeActive

    member this.SetEventHandlingActive (sessionID: string) (value: bool) : unit =
        this.ActiveGates <- setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.EventHandlingActive value
        this.TriggerStateChanged sessionID

    member this.IsEventHandlingActive(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.EventHandlingActive

    member this.SetMainContinuationAwaitingStart (sessionID: string) (value: bool) : unit =
        this.ActiveGates <-
            setGateActive this.ActiveGates sessionID FallbackSessionGateFlag.MainContinuationAwaitingStart value

        this.TriggerStateChanged sessionID

    member this.IsMainContinuationAwaitingStart(sessionID: string) : bool =
        isGateActive this.ActiveGates sessionID FallbackSessionGateFlag.MainContinuationAwaitingStart

    member this.GetActiveGates(sessionID: string) : Set<FallbackSessionGateFlag> =
        Map.tryFind sessionID this.ActiveGates |> Option.defaultValue emptyActiveGates

    member this.SetLatestHumanModel (sessionID: string) (model: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with LatestHumanModel = Some model }))

    member this.GetLatestHumanModel(sessionID: string) : string option =
        (this.GetSession sessionID).LatestHumanModel

    member this.ClearLatestHumanModel(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with LatestHumanModel = None }))

    member this.GetHumanTurnId(sessionID: string) : string = (this.GetSession sessionID).HumanTurnId

    member this.SetHumanTurnId (sessionID: string) (turnId: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with HumanTurnId = turnId }))

    member this.IncrementHumanTurnId(sessionID: string) : string =
        let nextId = "turn-" + System.Guid.NewGuid().ToString("N")

        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    HumanTurnId = nextId
                    CancelGeneration = s.CancelGeneration + 1
                    HumanTurnOrdinal = s.HumanTurnOrdinal + 1 }
        )

        nextId

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

    member this.GetChain(sessionID: string) : FallbackChain = (this.GetSession sessionID).Chain

    member this.SetChain (sessionID: string) (chain: FallbackChain) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Chain = chain }))

    member this.SetAgentName (sessionID: string) (agentName: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with AgentName = agentName }))

    member this.GetAgentName(sessionID: string) : string = (this.GetSession sessionID).AgentName

    member this.SetModel (sessionID: string) (model: FallbackModel) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Model = Some model }))

    member this.GetModel(sessionID: string) : FallbackModel option = (this.GetSession sessionID).Model

    member this.ClearModel(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Model = None }))

    member this.GetBusyCount(sessionID: string) : int = (this.GetSession sessionID).BusyCount

    member this.SetBusyCount (sessionID: string) (n: int) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with BusyCount = n }))

    member this.SetConsumed (sessionID: string) (value: bool) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Consumed = Some value }))
        this.TriggerStateChanged sessionID

    member this.GetConsumed(sessionID: string) : bool option = (this.GetSession sessionID).Consumed

    member this.ClearConsumed(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Consumed = None }))
        this.TriggerStateChanged sessionID

    member this.GetSessionOwner(sessionID: string) : SessionOwner = (this.GetSession sessionID).Owner

    member this.SetSessionOwner (sessionID: string) (owner: SessionOwner) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with Owner = owner }))

    member this.SetActiveNudgeNonce (sessionID: string) (nonce: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with ActiveNudgeNonce = nonce }))

    member this.GetActiveNudgeNonce(sessionID: string) : string =
        (this.GetSession sessionID).ActiveNudgeNonce

    member this.ClearActiveNudgeNonce(sessionID: string) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with ActiveNudgeNonce = "" }))

    member this.ClearInjected(sessionID: string) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    InjectedModel = None
                    InjectedAt = None }
        )

    member this.IsInjectedSince(sessionID: string, sinceTimestamp: int64) : bool =
        let s = this.GetSession sessionID

        match s.InjectedAt with
        | Some t -> t >= sinceTimestamp
        | None -> false

    member this.GetInjectedModel(sessionID: string) : FallbackModel option =
        (this.GetSession sessionID).InjectedModel

    member this.GetInjectedAt(sessionID: string) : int64 option = (this.GetSession sessionID).InjectedAt

    member this.SetInjectedAt (sessionID: string) (ts: int64) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with InjectedAt = Some ts }))

    member this.SetInjectedModel (sessionID: string) (model: FallbackModel) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with InjectedModel = Some model }))

    member this.SetTaskComplete (sessionID: string) (value: bool) : unit = this.SetCompacted(sessionID, value)
