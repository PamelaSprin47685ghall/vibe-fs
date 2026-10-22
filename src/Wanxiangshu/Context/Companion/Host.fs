namespace Wanxiangshu.Context.Companion

open System
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Session
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.HostSession
open Wanxiangshu.Persistence.Journal

type CompanionHost(primaryId: SessionId, sessions: ISessionHostPort, ?durable: ICompanionDurablePort) =
    let companion = Companion(?durable = durable, ?sessionId = Some primaryId)

    member _.Memory = companion.Memory
    member _.RefreshXTrace(state: XTraceProjectionState) = companion.RefreshXTrace(state)

    interface IDisposable with
        member _.Dispose() = ()
