namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

/// Production PTY backend: drives bun-pty under the OpenCode/Bun host.
/// All per-port backend state lives inside the record built by createPort so
/// that two PtyPorts never share live/pending/spawn state. The live-handle
/// registry and spawn bookkeeping live in PtyBackendRegistry; this file keeps
/// the command pipeline (handle) and the port assembly (createPort).
module PtyBackend =

    open PtyBackendRegistry

    let private handle
        (state: BackendState)
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
                    do! ensureSpawn state
                    let term = spawnSync state cmd cwd
                    attach state port id term exitTcs
                    return Ok()
                with ex ->
                    try
                        exitTcs.SetResult(())
                    with _ ->
                        ()

                    let msg = sprintf "PTY spawn failed: %s" ex.Message
                    // Flush any parked reader and pending pre-attach writes.
                    port.FailRead(id, msg)

                    for (_, tcsOpt) in takePending state id do
                        tcsOpt |> Option.iter (fun t -> t.SetResult(Error msg))

                    drop state id |> ignore
                    port.Complete(id, Error msg)
                    return Error msg
            | other -> return! applyLive state port id other
        }

    /// Builds a PtyPort whose handler drives real bun-pty sessions. Each call
    /// yields a port with fully isolated backend state.
    let createPort () : PtyPort =
        let state = BackendState.Create()
        let mutable portRef: PtyPort option = None

        let handler (id: PtyId) (command: PtyCommand) : Task<Result<unit, string>> =
            match portRef with
            | None -> Task.FromResult(Ok())
            | Some port -> handle state port id command

        let port = PtyPort(handler = handler)
        portRef <- Some port
        port
