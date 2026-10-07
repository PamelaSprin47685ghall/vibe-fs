namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation.Outcome

[<RequireQualifiedAccess>]
module ManagedSessionTermination =
    val terminate:
        cancelSessionChildren: (SessionId -> Task) ->
        sessionPort: ISessionHostPort ->
        eventPort: IEventObservationPort ->
        sessionId: SessionId ->
        authorityRoot: AuthorityRootUserMessageId ->
        reason: string ->
            Task<Result<unit, string>>

type InjectedSessionPort =
    new:
        underlyingPort: IOpenCodePort option *
        eventPort: IEventObservationPort *
        ?familyParent: (SessionId -> SessionId option) *
        ?isLifecycleTerminated: (SessionId -> bool) ->
            InjectedSessionPort

    interface ISessionHostPort
