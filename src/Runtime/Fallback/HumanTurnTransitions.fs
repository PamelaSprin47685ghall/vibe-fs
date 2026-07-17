module Wanxiangshu.Runtime.Fallback.HumanTurnTransitions

open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

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
