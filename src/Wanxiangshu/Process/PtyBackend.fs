namespace Wanxiangshu.Process

open System
open System.Threading
open System.Threading.Tasks

/// Production PTY backend: drives bun-pty under the OpenCode/Bun host.
/// All per-PTY state lives in PtySession/PtySupervisor; this file keeps the
/// command pipeline and the port assembly.
module PtyBackend =

    open PtySession
    open PtySupervisor

    let private completeExit (exitTcs: TaskCompletionSource<unit>) : unit =
        try
            exitTcs.SetResult(())
        with _ ->
            ()

    let private failPendingCommands (super: PtySupervisor) (id: PtyId) (msg: string) =
        for (_, tcsOpt) in takePending super id do
            tcsOpt |> Option.iter (fun t -> t.SetResult(Error msg))

    let private failPendingWrites (super: PtySupervisor) (port: PtyPort) (id: PtyId) (msg: string) : unit =
        port.FailRead(id, msg)
        failPendingCommands super id msg
        drop super id |> ignore
        port.Complete(id, Error msg)

    let private spawnLoaded super (port: PtyPort) id cmd cwd exitTcs =
        task {
            try
                let term = spawnSync super cmd cwd
                attach super port id term exitTcs
                return Ok()
            with ex ->
                let msg = sprintf "PTY startup outcome is unknown: %s" ex.Message
                port.FailExitObservation(id, msg)
                failPendingCommands super id msg
                return Error msg
        }

    let private spawnCommand
        (super: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (cmd: string)
        (cwd: string)
        : Task<Result<unit, string>> =
        task {
            let exitTcs = TaskCompletionSource<unit>()
            port.RegisterExitTask(id, exitTcs.Task)

            let! loaded =
                task {
                    try
                        do! ensureSpawn super
                        return Ok()
                    with ex ->
                        return Error ex.Message
                }

            match loaded with
            | Error reason ->
                completeExit exitTcs
                let msg = sprintf "PTY backend load failed before spawn: %s" reason
                failPendingWrites super port id msg
                return Error msg
            | Ok() -> return! spawnLoaded super port id cmd cwd exitTcs
        }

    let private handle
        (super: PtySupervisor)
        (port: PtyPort)
        (id: PtyId)
        (command: PtyCommand)
        : Task<Result<unit, string>> =
        task {
            match command with
            | PtyCommand.Spawn(cmd, cwd) -> return! spawnCommand super port id cmd cwd
            | other -> return! applyLive super port id other
        }

    /// Builds a PtyPort whose handler drives real bun-pty sessions. Each call
    /// yields a port with fully isolated backend state.
    let createPort () : PtyPort =
        let super = create ()
        // DSL-MUTABLE: resource — back-reference to the created port (cycle closure)
        let mutable portRef: PtyPort option = None

        let handler (id: PtyId) (command: PtyCommand) : Task<Result<unit, string>> =
            match portRef with
            | None -> Task.FromResult(Ok())
            | Some port -> handle super port id command

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port
