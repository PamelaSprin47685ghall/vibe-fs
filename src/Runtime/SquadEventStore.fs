module Wanxiangshu.Runtime.SquadEventStore

open Fable.Core
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.ProjectionCache
open Wanxiangshu.Runtime.EventStore
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventWanCodec

type EventLogStore with
    member this.GetSquadDag(sessionId: string) : JS.Promise<Dag> =
        promise {
            do! this.EnsureSynced()
            return this.ProjectionCache.GetSquadDag(sessionId)
        }

    member this.GetLatestSquadSessionId() : JS.Promise<string option> =
        promise {
            do! this.EnsureSynced()
            return this.ProjectionCache.GetLatestSquadSessionId()
        }

    member this.GetSquadSessions() : JS.Promise<Map<string, Dag>> =
        promise {
            do! this.EnsureSynced()
            return this.ProjectionCache.GetSquadSessions()
        }

    member this.AppendSquadEvent (at: string) (e: SquadEvent) : JS.Promise<Result<unit, string>> =
        this.AppendEvent(squadEventToWanEvent at e)
