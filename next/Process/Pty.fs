namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel

module Pty =

    let private sessionTable = Dictionary<string, ProcessHandle>()

    let pty_spawn
        (sessionId: string)
        (cmd: Command)
        (ctx: ProcessContext option)
        (cancellation: CancellationToken)
        : Task<Result<ProcessHandle, ProcessError>> =
        task {
            if sessionTable.ContainsKey(sessionId) then
                let old = sessionTable.[sessionId]
                sessionTable.Remove(sessionId) |> ignore
                try do! old.Kill() with _ -> ()
                try old.Dispose() with _ -> ()

            let ptyCmd =
                match cmd.PtyOptions with
                | Some _ -> cmd
                | None -> { cmd with PtyOptions = Some { Cols = 80; Rows = 24 } }

            let! res = ProcessSpawn.spawn ptyCmd ctx cancellation
            match res with
            | Ok handle ->
                sessionTable.[sessionId] <- handle
                return Ok handle
            | Error err ->
                return Error err
        }

    let pty_write (sessionId: string) (data: string) : Task<Result<unit, ProcessError>> =
        task {
            if not (sessionTable.ContainsKey(sessionId)) then
                return Error(ProcessError.ExecutionFailed(sprintf "Session '%s' not found" sessionId))
            else
                let handle = sessionTable.[sessionId]
                let ok = handle.WriteStdin(data)
                if ok then
                    return Ok ()
                else
                    return Error(ProcessError.ExecutionFailed(sprintf "Failed to write to session '%s'" sessionId))
        }

    let pty_read (sessionId: string) : Task<Result<string * bool, ProcessError>> =
        task {
            if not (sessionTable.ContainsKey(sessionId)) then
                return Error(ProcessError.ExecutionFailed(sprintf "Session '%s' not found" sessionId))
            else
                let handle = sessionTable.[sessionId]
                let! (stdoutText, truncated) = handle.StdoutTask
                return Ok(stdoutText, truncated)
        }

    let pty_kill (sessionId: string) : Task<unit> =
        task {
            if sessionTable.ContainsKey(sessionId) then
                let handle = sessionTable.[sessionId]
                sessionTable.Remove(sessionId) |> ignore
                try do! handle.Kill() with _ -> ()
                try handle.Dispose() with _ -> ()
        }
