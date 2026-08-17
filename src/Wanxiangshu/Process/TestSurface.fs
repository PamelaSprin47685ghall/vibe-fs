namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Foundation
open Wanxiangshu.OpenCode

/// Test-harness primitives for process-execution JS tests.
/// These are NOT production semantic APIs — they construct mock child
/// processes, task completion sources, and completion mailboxes that tests
/// drive as opaque fixtures. Production code never calls this module.
module ProcessTestSurface =

    let mockWaitChild (onKill: obj) : obj =
        let exit = TaskCompletionSource<int>()
        let exited = ref false
        let callbacks = ResizeArray<unit -> unit>()
        // DSL-MUTABLE: resource — mock child kill count
        let mutable killCount = 0

        let kill () =
            killCount <- killCount + 1

            if not (ProcessSurface.isNullish onKill) then
                ProcessSurface.call0 onKill |> ignore

        ProcessSurface.ChildHandle(
            { Process = null
              Exit = exit
              Kill = kill
              Exited = exited
              OnExited = callbacks },
            (fun () -> killCount)
        )
        :> obj

    let completionMailboxCreate () : obj =
        ProcessSurface.MailboxHandle(CompletionMailbox(obj ())) :> obj

    let completionMailboxPublishPty (mailbox: obj) (item: obj) : unit =
        (mailbox :?> ProcessSurface.MailboxHandle)
            .Mailbox.PublishPtyCompletion(unbox<PtyJoinItem> item)

    let completionMailboxDrainPty (mailbox: obj) (maxCount: int) : obj array =
        (mailbox :?> ProcessSurface.MailboxHandle).Mailbox
        |> fun value ->
            value.DrainPtyCompletions maxCount
            |> List.map ProcessSurface.completionViewItem
            |> List.toArray

    let completionMailboxPendingCount (mailbox: obj) : int =
        (mailbox :?> ProcessSurface.MailboxHandle).Mailbox.PendingCount

    let unitTaskSource () : obj =
        ProcessSurface.UnitTaskHandle(TaskCompletionSource<unit>()) :> obj

    let unitTaskResolve (source: obj) : unit =
        (source :?> ProcessSurface.UnitTaskHandle).Source.SetResult()

    let unitTask (source: obj) : Task =
        (source :?> ProcessSurface.UnitTaskHandle).Source.Task :> Task

    let resultTaskSourceCreate () : obj =
        ProcessSurface.ResultTaskHandle(TaskCompletionSource<Result<unit, string>>()) :> obj

    let resultTask (source: obj) : Task<obj> =
        task {
            let! value = (source :?> ProcessSurface.ResultTaskHandle).Source.Task

            match value with
            | Ok() -> return box {| ok = true |}
            | Error error -> return box {| ok = false; error = error |}
        }
