namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type ForkCompletionMailbox = ICompletionMailbox<AgentHandleId, PtyJoinItem, JoinInterruptReason, MailboxWakeReason>

[<RequireQualifiedAccess>]
module ForkRuntimeBackend =
    val create: clock: IClockPort -> createMailbox: (obj -> ForkCompletionMailbox) -> ForkRuntime
