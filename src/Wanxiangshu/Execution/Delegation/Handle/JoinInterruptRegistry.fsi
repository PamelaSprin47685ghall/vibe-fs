namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation.Identity

type JoinAttemptLease =
    new: interrupt: JoinInterrupt * unregister: (unit -> unit) -> JoinAttemptLease
    member Wait: Task<JoinInterruptReason>
    member SignalOperatorAbort: unit -> unit
    member SignalUserMessage: unit -> unit
    member SignalDeadline: unit -> unit
    interface IDisposable

type IJoinAttemptRegistry =
    abstract Begin: SessionId * ToolCallId option -> JoinAttemptLease
    abstract SignalUserMessage: SessionId -> unit
    abstract ClearSession: SessionId -> unit

type JoinAttemptRegistry =
    new: unit -> JoinAttemptRegistry
    interface IJoinAttemptRegistry
