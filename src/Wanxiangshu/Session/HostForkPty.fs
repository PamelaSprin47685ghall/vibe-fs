namespace Wanxiangshu.Session

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

[<AutoOpen>]
module HostForkRuntimePty =
    type HostForkRuntime with
        member this.TrackPtyRun(id: PtyId) =
            lock this.Gate (fun () ->
                this.PtyRuns.Add id.Value |> ignore
                this.LastPtyId <- Some id.Value)

        member this.RegisterPtySnapshot (id: PtyId) (command: string) =
            this.Runtime.RegisterPty
                { PtyId = id.Value
                  AgentId = id.Value
                  Command = command
                  StartedAt = this.Now() }

        member this.UntrackPtyRun(id: string) =
            lock this.Gate (fun () -> this.PtyRuns.Remove id |> ignore)
            this.Runtime.UnregisterPty id

        member this.OwnsPty(id: PtyId) =
            lock this.Gate (fun () -> this.PtyRuns.Contains id.Value)

        member this.IsPtyCompletion(runId: string) =
            lock this.Gate (fun () -> this.PtyRuns.Contains runId)

        member this.ForkPty(command: string, agent: ManagedAgent, ?cwd: string) : Task<Result<PtyId, string>> =
            task {
                if String.IsNullOrWhiteSpace command then
                    return Error "PTY command is required"
                else
                    let id = Pty.newId ()
                    this.TrackPtyRun id
                    this.RegisterPtySnapshot id command

                    try
                        this.PtyPort.Fork(command, agent, ptyId = id, ?cwd = cwd) |> ignore
                        return Ok id
                    with ex ->
                        this.UntrackPtyRun id.Value
                        return Error ex.Message
            }

        member this.TryPty(id: string) =
            if String.IsNullOrWhiteSpace id then
                // 无 agent：作用于最近创建的 PTY（pty-stress 剧本的写/读/signal 语义）。
                // `OwnsPty` 再查一次，防止 lastPtyId 指向已被 join 清掉的 PTY。
                match this.LastPtyId with
                | Some last when this.OwnsPty(PtyId.Create last) && this.PtyPort.Known(PtyId.Create last) ->
                    Some(PtyId.Create last)
                | _ -> None
            else
                let candidate = PtyId.Create id

                if this.OwnsPty candidate && this.PtyPort.Known candidate then
                    Some candidate
                else
                    None

        member this.SendPty(id: PtyId, prompt: string, signal: PtySignal option) : Task<Result<PtyRead, string>> =
            task {
                if not (this.OwnsPty id) then
                    return Error(sprintf "Unknown PTY id: %s" id.Value)
                elif not (this.PtyPort.Exists id) then
                    return Error(sprintf "Unknown PTY id: %s" id.Value)
                else
                    match signal with
                    | Some value ->
                        let! sent = this.PtyPort.Send(id, PtyCommand.Signal value)

                        match sent with
                        | Ok() -> return Ok { Id = id; Output = ""; Closed = false }
                        | Error err -> return Error err
                    | None when String.IsNullOrEmpty prompt ->
                        let! read = this.PtyPort.Read id

                        match read with
                        | Ok(output, closed) ->
                            return
                                Ok
                                    { Id = id
                                      Output = output
                                      Closed = closed }
                        | Error err -> return Error err
                    | None ->
                        // Agents often omit the trailing Enter; shells then hang waiting
                        // for it. Ensure write ends with LF unless CR/LF is already present.
                        let payload =
                            if
                                prompt.EndsWith("\n", StringComparison.Ordinal)
                                || prompt.EndsWith("\r", StringComparison.Ordinal)
                            then
                                prompt
                            else
                                prompt + "\n"

                        let! writeResult = this.PtyPort.Send(id, PtyCommand.Write(Pty.bytes payload))

                        match writeResult with
                        | Ok() -> return Ok { Id = id; Output = ""; Closed = false }
                        | Error err -> return Error err
            }
