namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type ForkCompletionMailbox = ICompletionMailbox<AgentHandleId, PtyJoinItem, JoinInterruptReason, MailboxWakeReason>

[<RequireQualifiedAccess>]
module ForkRuntimeBackend =
    /// Composition-injected timeout capability (raceExit: Task -> int -> Task<bool>) and wall clock.
    val create:
        clock: IClockPort ->
        raceExit: (Task -> int -> Task<bool>) ->
        createMailbox: (obj -> ForkCompletionMailbox) ->
            ForkRuntime
