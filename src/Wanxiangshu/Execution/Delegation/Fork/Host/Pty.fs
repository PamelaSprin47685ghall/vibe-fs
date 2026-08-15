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
open FsToolkit.ErrorHandling
open Wanxiangshu.OpenCode
open Wanxiangshu.Process
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
module private PtyHostHelpers =
    let bindTerminalName
        (gate: obj)
        (terminalByName: System.Collections.Generic.Dictionary<string, string>)
        (name: string)
        (id: PtyId)
        =
        lock gate (fun () ->
            match terminalByName.TryGetValue(name.Trim()) with
            | true, existing when existing <> id.Value ->
                Error(sprintf "Terminal name '%s' is already in use" (name.Trim()))
            | _ ->
                terminalByName.[name.Trim()] <- id.Value
                Ok())

    let ptyByName
        (gate: obj)
        (terminalByName: System.Collections.Generic.Dictionary<string, string>)
        (ptyRuns: System.Collections.Generic.HashSet<string>)
        (known: PtyId -> bool)
        (name: string)
        =
        lock gate (fun () ->
            match terminalByName.TryGetValue(name.Trim()) with
            | true, id when ptyRuns.Contains id && known (PtyId.Create id) -> Some(PtyId.Create id)
            | _ -> None)

    let ensureLf (prompt: string) =
        if
            prompt.EndsWith("\n", StringComparison.Ordinal)
            || prompt.EndsWith("\r", StringComparison.Ordinal)
        then
            prompt
        else
            prompt + "\n"

    let emptyRead (id: PtyId) : PtyRead =
        { Id = id; Output = ""; Closed = false }

    let mapRead (id: PtyId) (output: string, closed: bool) : PtyRead =
        { Id = id
          Output = output
          Closed = closed }

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
                PtyHostHelpers.bindTerminalName this.Gate this.TerminalByName name id

        member this.TryPtyByName(name: string) : PtyId option =
            if String.IsNullOrWhiteSpace name then
                None
            else
                PtyHostHelpers.ptyByName this.Gate this.TerminalByName this.PtyRuns this.PtyPort.Known name

        member this.ForkPty(command: string, agent: ManagedAgent, ?cwd: string) : Task<Result<PtyId, string>> =
            taskResult {
                do!
                    if String.IsNullOrWhiteSpace command then
                        Error "PTY command is required"
                    else
                        Ok()

                let id = Pty.newId ()
                this.TrackPtyRun id
                this.RegisterPtySnapshot id command

                try
                    this.PtyPort.Fork(command, agent, ptyId = id, ?cwd = cwd) |> ignore
                    return id
                with ex ->
                    this.UntrackPtyRun id.Value
                    return! Error ex.Message
            }

        member this.TryPty(id: string) =
            if String.IsNullOrWhiteSpace id then
                None
            elif this.OwnsPty(PtyId.Create id) && this.PtyPort.Known(PtyId.Create id) then
                Some(PtyId.Create id)
            else
                None

        member this.SendPty(id: PtyId, prompt: string, signal: PtySignal option) : Task<Result<PtyRead, string>> =
            taskResult {
                do!
                    if not (this.OwnsPty id) then
                        Error(sprintf "Unknown PTY id: %s" id.Value)
                    elif not (this.PtyPort.Exists id) then
                        Error(sprintf "Unknown PTY id: %s" id.Value)
                    else
                        Ok()

                match signal with
                | Some value ->
                    do! this.PtyPort.Send(id, PtyCommand.Signal value)
                    return PtyHostHelpers.emptyRead id
                | None when String.IsNullOrEmpty prompt ->
                    let! output, closed = this.PtyPort.Read id
                    return PtyHostHelpers.mapRead id (output, closed)
                | None ->
                    // Agents often omit the trailing Enter; shells then hang waiting
                    // for it. Ensure write ends with LF unless CR/LF is already present.
                    do! this.PtyPort.Send(id, PtyCommand.Write(Pty.bytes (PtyHostHelpers.ensureLf prompt)))
                    return PtyHostHelpers.emptyRead id
            }
