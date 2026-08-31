namespace Wanxiangshu.Execution.Delegation.Fork.Host

open System
open System.Text
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Process

[<RequireQualifiedAccess>]
module HostForkPtySurface =
    let private callView command =
        match command with
        | PtyCommand.Write bytes ->
            Some(
                box
                    {| kind = "write"
                       text = Encoding.UTF8.GetString bytes |}
            )
        | PtyCommand.Signal signal ->
            Some(
                box
                    {| kind = "signal"
                       signal = PtySupervisor.signalName signal |}
            )
        | _ -> None

    let private outcomeView calls owned known =
        function
        | Ok read ->
            box
                {| ok = true
                   id = read.Id.Value
                   output = read.Output
                   closed = read.Closed
                   owned = owned
                   known = known
                   calls = calls |}
        | Error error ->
            box
                {| ok = false
                   error = error
                   owned = owned
                   known = known
                   calls = calls |}

    let private forkView (calls: obj array) (runtime: HostForkRuntime) (port: PtyPort) =
        function
        | Ok(id: PtyId) ->
            box
                {| ok = true
                   id = id.Value
                   owned = runtime.OwnsPty id
                   known = port.Known id
                   calls = calls |}
        | Error error ->
            box
                {| ok = false
                   error = error
                   owned = runtime.SnapshotOutstandingPtyRuns() |> List.isEmpty |> not
                   calls = calls |}

    let scenario (action: string) (input: string) (failure: string) : Task<obj> =
        task {
            let calls = ResizeArray<obj>()
            let mutable portRef: PtyPort option = None

            let handler id command =
                if action = "fork-error" then
                    match command with
                    | PtyCommand.Spawn _ -> raise (InvalidOperationException failure)
                    | _ -> ()

                callView command |> Option.iter calls.Add

                match command, portRef with
                | PtyCommand.Read, Some port -> port.ReadResult(id, "terminal text", true)
                | _ -> ()

                if String.IsNullOrEmpty failure then
                    Task.FromResult(Ok())
                else
                    Task.FromResult(Error failure)

            let port = PtyPort(handler = handler)
            portRef <- Some port

            let runtime =
                HostForkRuntime(
                    SessionId.create "pty-owner",
                    Unchecked.defaultof<ISessionHostPort>,
                    (fun _ _ _ -> Task.FromResult None),
                    ptyPort = port
                )

            let agent = ManagedAgent.make AgentTier.Fast Role.DevOps

            match action with
            | "lookup-unknown" ->
                return
                    box
                        {| ok = true
                           known = runtime.TryPty(input).IsSome
                           calls = calls.ToArray() |}
            | "send-unowned" ->
                let id = PtyId.Create "foreign"
                let! outcome = runtime.SendPty(id, input, None)
                return outcomeView (calls.ToArray()) (runtime.OwnsPty id) (port.Known id) outcome
            | "track-untrack" ->
                let id = PtyId.Create input
                runtime.TrackPtyRun id
                let ownedBefore = runtime.OwnsPty id
                runtime.UntrackPtyRun id.Value

                return
                    box
                        {| ok = true
                           ownedBefore = ownedBefore
                           ownedAfter = runtime.OwnsPty id
                           calls = calls.ToArray() |}
            | "blank-fork"
            | "fork"
            | "fork-error" ->
                let! outcome = runtime.ForkPty(input, agent)
                return forkView (calls.ToArray()) runtime port outcome
            | _ ->
                match! runtime.ForkPty("shell", agent) with
                | Error error ->
                    return
                        box
                            {| ok = false
                               error = error
                               calls = calls.ToArray() |}
                | Ok id ->
                    if action = "send-closed" then
                        port.Complete id

                    let signal =
                        if action = "signal" then
                            PtySignal.tryParse input |> Result.toOption
                        else
                            None

                    let prompt = if action = "write" || action = "send-closed" then input else ""
                    let! outcome = runtime.SendPty(id, prompt, signal)
                    return outcomeView (calls.ToArray()) (runtime.OwnsPty id) (port.Known id) outcome
        }
