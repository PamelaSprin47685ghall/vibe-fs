module Wanxiangshu.Runtime.Fallback.ModelInjection

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore
open Wanxiangshu.Runtime.Fallback.SessionRuntimePropertyPure

type FallbackRuntimeStore with
    member this.ClearInjected(sessionID: string) : unit =
        this.UpdateSession(sessionID, clearInjected)

    member this.IsInjectedSince(sessionID: string, sinceMs: int64) : bool =
        isInjectedSince sinceMs (this.GetSession sessionID)

    member this.GetInjectedModel(sessionID: string) : FallbackModel option =
        (this.GetSession sessionID).InjectedModel

    member this.GetInjectedAt(sessionID: string) : int64 option = (this.GetSession sessionID).InjectedAt

    member this.SetInjectedAt (sessionID: string) (ts: int64) : unit =
        this.UpdateSession(sessionID, setInjectedAt ts)

    member this.SetInjectedModel (sessionID: string) (model: FallbackModel) : unit =
        this.UpdateSession(sessionID, setInjectedModel model)
