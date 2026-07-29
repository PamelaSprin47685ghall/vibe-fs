namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.AsyncSupport

/// Deadline-aware wait-for-exit logic extracted from NodeProcessHost to
/// keep each unit under the 300-line ArchitectureGate limit.
module NodeProcessWait =

    let waitForExit (child: NodeProcessHost.ChildProcess) (deadline: Deadline) (ct: CancellationToken) : Task<int * bool> =
        task {
            if child.Exited.Value then
                return! child.Exit.Task
            elif ct.IsCancellationRequested then
                child.Kill()
                trySetCanceled child.Exit |> ignore
                return! child.Exit.Task
            else
                let clock = fun () -> DateTimeOffset.UtcNow
                let ms = Deadline.nextWaitMs clock deadline

                if ms <= 0 then
                    child.Kill()
                    trySetResult child.Exit (-1, true) |> ignore
                    return! child.Exit.Task
                else
                    let mutable timerCleared = false
                    let mutable timerId = None

                    let clearTimer () =
                        if not timerCleared then
                            timerCleared <- true

                            match timerId with
                            | Some id -> emitJsExpr id "clearTimeout($0)" |> ignore
                            | None -> ()

                    let onTimeout =
                        fun () ->
                            clearTimer ()
                            child.Kill()
                            trySetResult child.Exit (-1, true) |> ignore

                    let id = emitJsExpr (ms, onTimeout) "setTimeout($1, $0)"

                    timerId <- Some id

                    use _ =
                        ct.Register(fun () ->
                            clearTimer ()
                            child.Kill()
                            trySetCanceled child.Exit |> ignore)

                    try
                        let! result = child.Exit.Task
                        clearTimer ()
                        return result
                    finally
                        clearTimer ()
        }
