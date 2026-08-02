namespace Wanxiangshu.Next.Process

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Session

/// Production PTY backend: drives bun-pty under the OpenCode/Bun host.
/// All per-PTY state lives in PtySession/PtySupervisor; this file keeps the
/// command pipeline and the port assembly.
module PtyBackend =

    open PtySession
    open PtySupervisor

    let private handle
        (super: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (command: PtyCommand)
        : Task<Result<unit, string>> =
        task {
            match command with
            | PtyCommand.Spawn(cmd, cwd) ->
                let exitTcs = TaskCompletionSource<unit>()
                port.RegisterExitTask(id, exitTcs.Task)

                try
                    do! ensureSpawn super
                    let term = spawnSync super cmd cwd
                    attach super port id term exitTcs
                    return Ok()
                with ex ->
                    try
                        exitTcs.SetResult(())
                    with _ ->
                        ()

                    let msg = sprintf "PTY spawn failed: %s" ex.Message
                    // Flush any parked reader and pending pre-attach writes.
                    port.FailRead(id, msg)

                    for (_, tcsOpt) in takePending super id do
                        tcsOpt |> Option.iter (fun t -> t.SetResult(Error msg))

                    drop super id |> ignore
                    port.Complete(id, Error msg)
                    return Error msg
            | other -> return! applyLive super port id other
        }

    /// Builds a PtyPort whose handler drives real bun-pty sessions. Each call
    /// yields a port with fully isolated backend state.
    let createPort () : PtyPort =
        let super = create ()
        let mutable portRef: PtyPort option = None

        let handler (id: PtyId) (command: PtyCommand) : Task<Result<unit, string>> =
            match portRef with
            | None -> Task.FromResult(Ok())
            | Some port -> handle super port id command

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port
