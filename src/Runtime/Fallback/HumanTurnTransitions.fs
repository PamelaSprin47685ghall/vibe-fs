module Wanxiangshu.Runtime.Fallback.HumanTurnTransitions

open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

type FallbackRuntimeStore with
    member this.GetHumanTurnId(sessionID: string) : string = (this.GetSession sessionID).HumanTurnId

    member this.SetHumanTurnId (sessionID: string) (turnId: string) : unit =
        this.UpdateSession(sessionID, setHumanTurnId turnId)

    member this.IncrementHumanTurnId(sessionID: string) : string =
        let mutable turnId = ""

        this.UpdateSession(
            sessionID,
            fun s ->
                let s', id = advanceHumanTurn s
                turnId <- id
                s'
        )

        turnId

    member this.GetLatestHumanModel(sessionID: string) : string option =
        (this.GetSession sessionID).LatestHumanModel

    member this.SetLatestHumanModel (sessionID: string) (model: string) : unit =
        this.UpdateSession(sessionID, recordLatestHumanModel model)

    member this.ClearLatestHumanModel(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearLatestHumanModel)
