module Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

type FallbackRuntimeStore with
    member this.GetChain(sessionID: string) : FallbackChain = (this.GetSession sessionID).Chain

    member this.SetChain (sessionID: string) (chain: FallbackChain) : unit =
        this.UpdateSession(sessionID, selectChain chain)

    member this.GetAgentName(sessionID: string) : string = (this.GetSession sessionID).AgentName

    member this.SetAgentName (sessionID: string) (agentName: string) : unit =
        this.UpdateSession(sessionID, recordAgentName agentName)

    member this.GetModel(sessionID: string) : FallbackModel option = (this.GetSession sessionID).Model

    member this.SetModel (sessionID: string) (model: FallbackModel) : unit =
        this.UpdateSession(sessionID, selectModel model)

    member this.ClearModel(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearModel)

    member this.GetBusyCount(sessionID: string) : int = (this.GetSession sessionID).BusyCount

    member this.SetBusyCount (sessionID: string) (n: int) : unit =
        this.UpdateSession(sessionID, markBusy n)

    member this.GetConsumed(sessionID: string) : bool option = (this.GetSession sessionID).Consumed

    member this.SetConsumed (sessionID: string) (value: bool) : unit =
        this.Update(sessionID, recordConsumed value)

    member this.ClearConsumed(sessionID: string) : unit =
        this.Update(sessionID, clearConsumption)

    member this.GetSessionOwner(sessionID: string) : SessionOwner = (this.GetSession sessionID).Owner

    member this.SetSessionOwner (sessionID: string) (owner: SessionOwner) : unit =
        this.UpdateSession(sessionID, transferOwnership owner)

    member this.GetActiveNudgeNonce(sessionID: string) : string =
        (this.GetSession sessionID).ActiveNudgeNonce

    member this.SetActiveNudgeNonce (sessionID: string) (nonce: string) : unit =
        this.UpdateSession(sessionID, armNudgeNonce nonce)

    member this.ClearActiveNudgeNonce(sessionID: string) : unit =
        this.UpdateSession(sessionID, disarmNudgeNonce)

    member this.TryConsumeActiveNudgeNonce(sessionID: string, observedNonce: string) : bool =
        let mutable consumed = false

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', c = tryConsumeNudgeNonce observedNonce s
                consumed <- c
                s'
        )

        consumed
