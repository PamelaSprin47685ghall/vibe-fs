namespace Wanxiangshu.Next.Session

open System
open System.Threading.Tasks
open Wanxiangshu.Next.Process
open Wanxiangshu.Next.Kernel.Identity

[<AutoOpen>]
module HostForkRuntimePty =
    type HostForkRuntime with
        member this.ForkPty(command: string) : Task<Result<PtyId, string>> =
            task {
                if String.IsNullOrWhiteSpace command then
                    return Error "PTY command is required"
                else
                    let id = Pty.newId ()
                    this.TrackPtyRun id
                    this.RegisterPtySnapshot id command

                    try
                        this.PtyPort.Fork(command, ptyId = id) |> ignore
                        return Ok id
                    with ex ->
                        this.UntrackPtyRun id.Value
                        return Error ex.Message
            }

        member this.TryPty(id: string) =
            let candidate = PtyId.Create id

            if this.PtyPort.Exists candidate then
                Some candidate
            else
                None

        member this.SendPty(id: PtyId, prompt: string, signal: PtySignal option) : Task<Result<PtyId, string>> =
            task {
                if not (this.PtyPort.Exists id) then
                    return Error(sprintf "Unknown PTY id: %s" id.Value)
                else
                    match signal with
                    | Some value -> this.PtyPort.Send(id, PtyCommand.Signal value)
                    | None when String.IsNullOrEmpty prompt -> this.PtyPort.Send(id, PtyCommand.Read)
                    | None -> this.PtyPort.Send(id, PtyCommand.Write(Pty.bytes prompt))

                    return Ok id
            }
