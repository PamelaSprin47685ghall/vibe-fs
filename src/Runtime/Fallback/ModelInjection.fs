module Wanxiangshu.Runtime.Fallback.ModelInjection

/// Model injection helpers over the fallback runtime store.

open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Runtime.Fallback.RuntimeStore

type FallbackRuntimeStore with

    member this.ClearInjected(sessionID: string) : unit =
        this.UpdateSession(
            sessionID,
            fun s ->
                { s with
                    InjectedModel = None
                    InjectedAt = None }
        )

    member this.IsInjectedSince(sessionID: string, sinceMs: int64) : bool =
        (this.GetSession sessionID).InjectedAt
        |> Option.exists (fun at -> at >= sinceMs)

    member this.GetInjectedModel(sessionID: string) : FallbackModel option =
        (this.GetSession sessionID).InjectedModel

    member this.GetInjectedAt(sessionID: string) : int64 option = (this.GetSession sessionID).InjectedAt

    member this.SetInjectedAt (sessionID: string) (ts: int64) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with InjectedAt = Some ts }))

    member this.SetInjectedModel (sessionID: string) (model: FallbackModel) : unit =
        this.UpdateSession(sessionID, (fun s -> { s with InjectedModel = Some model }))

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
