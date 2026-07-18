module Wanxiangshu.Runtime.Fallback.SessionPropertyTransitions

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

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

    /// Atomically check whether the stored active nudge nonce matches the
    /// observed nonce, and if so, clear it. Returns true on a successful
    /// match-and-consume. Used by the chat.message hook to decouple the
    /// nonce lifetime from the prompt() Promise: the nonce persists across
    /// the async dispatch gap and is only consumed when the matching
    /// message is actually observed by chat.message.
    member this.TryConsumeActiveNudgeNonce(sessionID: string, observedNonce: string) : bool =
        if observedNonce = "" then
            false
        else
            let s = this.GetSession sessionID

            if s.ActiveNudgeNonce <> "" && s.ActiveNudgeNonce = observedNonce then
                this.UpdateSession(sessionID, (fun s -> { s with ActiveNudgeNonce = "" }))
                true
            else
                false
