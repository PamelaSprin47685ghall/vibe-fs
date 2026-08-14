namespace Wanxiangshu.Execution.Delegation.Fork.Host

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<AutoOpen>]
module HostForkRuntimePty =
    type HostForkRuntime with
        member this.TrackPtyRun(id: PtyId) =
            lock this.Gate (fun () -> this.PtyRuns.Add id.Value |> ignore)

        member this.RegisterPtySnapshot (id: PtyId) (command: string) =
            this.Runtime.RegisterPty
                { PtyId = id.Value
                  AgentId = id.Value
                  Command = command
                  StartedAt = this.Now() }

        member this.UntrackPtyRun(id: string) =
            lock this.Gate (fun () ->
                this.PtyRuns.Remove id |> ignore

                let stale =
                    this.TerminalByName
                    |> Seq.filter (fun kv -> kv.Value = id)
                    |> Seq.map (fun kv -> kv.Key)
                    |> Seq.toList

                for name in stale do
                    this.TerminalByName.Remove name |> ignore)

            this.Runtime.UnregisterPty id

        member this.OwnsPty(id: PtyId) =
            lock this.Gate (fun () -> this.PtyRuns.Contains id.Value)

        member this.IsPtyCompletion(runId: string) =
            lock this.Gate (fun () -> this.PtyRuns.Contains runId)

        member this.TryBindTerminalName(name: string, id: PtyId) : Result<unit, string> =
            if String.IsNullOrWhiteSpace name then
                Error "Terminal name is required"
            else
                lock this.Gate (fun () ->
                    match this.TerminalByName.TryGetValue(name.Trim()) with
                    | true, existing when existing <> id.Value ->
                        Error(sprintf "Terminal name '%s' is already in use" (name.Trim()))
                    | _ ->
                        this.TerminalByName.[name.Trim()] <- id.Value
                        Ok())

        member this.TryPtyByName(name: string) : PtyId option =
            if String.IsNullOrWhiteSpace name then
                None
            else
                lock this.Gate (fun () ->
                    match this.TerminalByName.TryGetValue(name.Trim()) with
                    | true, id when this.PtyRuns.Contains id && this.PtyPort.Known(PtyId.Create id) ->
                        Some(PtyId.Create id)
                    | _ -> None)

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
                None
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
