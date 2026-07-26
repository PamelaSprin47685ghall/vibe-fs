namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Session

[<RequireQualifiedAccess>]
type PtySignal =
    | Terminate
    | Kill
    | Interrupt

module PtySignal =
    [<Literal>]
    let TermName = "TERM"

    [<Literal>]
    let KillName = "KILL"

    let tryParse (value: string) =
        match value with
        | TermName -> Ok PtySignal.Terminate
        | KillName -> Ok PtySignal.Kill
        | _ -> Error(sprintf "Unsupported PTY signal: %s" value)

[<RequireQualifiedAccess>]
type PtyCommand =
    | Spawn of command: string * cwd: string
    | Write of bytes: byte[]
    | Read
    | Signal of signal: PtySignal
    | Resize of width: int * height: int

type PtyId =
    | PtyId of id: string

    member this.Value =
        match this with
        | PtyId id -> id

    static member Create(id: string) = PtyId id

type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      AgentId: string option
      Role: AgentRole option }

type PtyRead =
    { Id: PtyId
      Output: string
      Closed: bool }

type PtyBackendHandler = PtyId -> PtyCommand -> unit

[<RequireQualifiedAccess>]
module PtyOutcome =
    [<Literal>]
    let Closed = "closed"

    [<Literal>]
    let Signalled = "signalled"

/// Typed PTY lifecycle boundary. A backend receives commands; completion events
/// are supplied by Complete and share every registered mailbox sender.
type PtyPort
    (?mailboxSender: RunCompletion -> unit, ?handler: PtyBackendHandler, ?agentProvider: unit -> AgentRecord list) =
    let handler = defaultArg handler (fun _ _ -> ())
    let agentProvider = defaultArg agentProvider (fun () -> [])
    let mailboxSenders = ResizeArray<RunCompletion -> unit>()
    let gate = obj ()
    let active = Dictionary<PtyId, PtyHandle * ref<bool>>()
    let readWaiters = Dictionary<PtyId, TaskCompletionSource<string * bool>>()
    do mailboxSender |> Option.iter mailboxSenders.Add

    let closeInternal (id: PtyId) (outcome: Result<string, string>) (sendTerminate: bool) =
        let target =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, value -> Some value
                | _ -> None)

        match target with
        | None -> ()
        | Some(handle, closed) ->
            let alreadyClosed =
                lock closed (fun () ->
                    if closed.Value then
                        true
                    else
                        closed.Value <- true
                        false)

            if not alreadyClosed then
                if sendTerminate then
                    try
                        handler id (PtyCommand.Signal PtySignal.Terminate)
                    with _ ->
                        ()

                lock gate (fun () -> active.Remove id |> ignore)

                let completion =
                    { RunId = id.Value
                      AgentId = defaultArg handle.AgentId id.Value
                      Role = defaultArg handle.Role AgentRole.Executor
                      Outcome = outcome
                      CompletedAt = DateTimeOffset.UtcNow }

                let senders = lock gate (fun () -> mailboxSenders |> Seq.toList)

                for sender in senders do
                    try
                        sender completion
                    with _ ->
                        ()

    member _.AddMailboxSender(sender: RunCompletion -> unit) =
        lock gate (fun () -> mailboxSenders.Add sender)

    member _.MailboxSender = mailboxSender
    member _.Handler = handler
    member _.AgentProvider = agentProvider

    member this.Fork(command: string, ?agentId: string, ?role: AgentRole, ?ptyId: PtyId, ?cwd: string) : PtyId =
        let id =
            defaultArg ptyId (PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8)))

        let handle =
            { Id = id
              Command = command
              StartedAt = DateTimeOffset.UtcNow
              AgentId = agentId
              Role = role }

        lock gate (fun () -> active.[id] <- (handle, ref false))
        handler id (PtyCommand.Spawn(command, defaultArg cwd ""))
        id

    member this.Exists(id: PtyId) =
        lock gate (fun () -> active.ContainsKey id)

    member this.Send(id: PtyId, command: PtyCommand) =
        let live =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, closed) when not closed.Value -> true
                | _ -> false)

        if live then
            // Signal only forwards to the backend; completion belongs to the
            // backend's onExit, never to Send.
            handler id command

    /// Reads the currently buffered PTY output without completing the join.
    /// The backend resolves the waiter with (output, closed); final exit still
    /// belongs to onExit -> Complete.
    member this.Read(id: PtyId) : Task<Result<string * bool, string>> =
        let live =
            lock gate (fun () ->
                match active.TryGetValue id with
                | true, (_, closed) when not closed.Value -> true
                | _ -> false)

        if not live then
            Task.FromResult(Error(sprintf "Unknown PTY id: %s" id.Value))
        else
            let tcs = TaskCompletionSource<string * bool>()
            lock gate (fun () -> readWaiters.[id] <- tcs)
            handler id PtyCommand.Read

            task {
                let! result = tcs.Task
                return Ok result
            }

    /// Resolved by the backend when it has drained the buffer for a Read.
    member _.ReadResult(id: PtyId, output: string, closed: bool) =
        let tcs =
            lock gate (fun () ->
                match readWaiters.TryGetValue id with
                | true, t ->
                    readWaiters.Remove id |> ignore
                    Some t
                | false, _ -> None)

        match tcs with
        | Some t -> t.SetResult((output, closed))
        | None -> ()

    /// Complete from a backend exit, signal notification, or read result.
    member _.Complete(id: PtyId, ?outcome: Result<string, string>) =
        closeInternal id (defaultArg outcome (Ok PtyOutcome.Closed)) false

    member _.Close(id: PtyId, ?outcome: Result<string, string>) =
        closeInternal id (defaultArg outcome (Ok PtyOutcome.Closed)) true

    member this.CloseAll() =
        let ids = lock gate (fun () -> active.Keys |> Seq.toList)

        for id in ids do
            this.Close id

    member _.List() : AgentRecord list * PtyHandle list =
        let agents = agentProvider ()
        let ptys = lock gate (fun () -> active.Values |> Seq.map fst |> Seq.toList)
        agents, ptys

module Pty =
    [<Literal>]
    let AgentName = "pty"

    let forkPty (port: PtyPort) (command: string) : PtyId = port.Fork command

    let forkPtyWith
        (port: PtyPort)
        (command: string)
        (agentId: string option)
        (role: AgentRole option)
        (ptyId: PtyId option)
        : PtyId =
        port.Fork(command, ?agentId = agentId, ?role = role, ?ptyId = ptyId)

    let send (port: PtyPort) (id: PtyId) (command: PtyCommand) = port.Send(id, command)
    let complete (port: PtyPort) (id: PtyId) (outcome: Result<string, string>) = port.Complete(id, outcome = outcome)
    let list (port: PtyPort) = port.List()
    let close (port: PtyPort) (id: PtyId) = port.Close id
    let bytes (text: string) = Encoding.UTF8.GetBytes text

    let newId () =
        PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8))

    let private parentGate = obj ()
    let private parentAborters = Dictionary<string, Dictionary<int, unit -> unit>>()
    let mutable private nextAbortToken = 0

    let registerParentAbort (parentId: string) (abort: unit -> unit) =
        lock parentGate (fun () ->
            nextAbortToken <- nextAbortToken + 1
            let token = nextAbortToken

            let callbacks =
                match parentAborters.TryGetValue parentId with
                | true, values -> values
                | _ ->
                    let values = Dictionary<int, unit -> unit>()
                    parentAborters.[parentId] <- values
                    values

            callbacks.[token] <- abort
            token)

    let unregisterParentAbort (parentId: string) (token: int) =
        lock parentGate (fun () ->
            match parentAborters.TryGetValue parentId with
            | true, callbacks ->
                callbacks.Remove token |> ignore

                if callbacks.Count = 0 then
                    parentAborters.Remove parentId |> ignore
            | _ -> ())

    let abortParent (parentId: string) =
        let callbacks =
            lock parentGate (fun () ->
                match parentAborters.TryGetValue parentId with
                | true, values -> values.Values |> Seq.toList
                | _ -> [])

        callbacks
        |> List.iter (fun abort ->
            try
                abort ()
            with _ ->
                ())
