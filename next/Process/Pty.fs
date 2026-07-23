namespace Wanxiangshu.Next.Process

open System
#if !FABLE_COMPILER
open System.Threading.Channels
#endif
open Wanxiangshu.Next.Session

[<RequireQualifiedAccess>]
type PtySignal =
    | Terminate
    | Kill
    | Interrupt

[<RequireQualifiedAccess>]
type PtyCommand =
    | Spawn of command: string
    | Write of bytes: byte[]
    | Read
    | Signal of signal: PtySignal
    | Resize of width: int * height: int

type PtyId =
    | PtyId of id: string

    member this.Value =
        match this with
        | PtyId id -> id

type PtyHandle =
    { Id: PtyId
      Command: string
      StartedAt: DateTimeOffset
      AgentId: string option
      Role: AgentRole option }

type PtyBackendHandler = PtyId -> PtyCommand -> unit

type PtyPort
    (
#if !FABLE_COMPILER
        ?mailbox: Channel<RunCompletion>,
#else
        ?mailboxSender: RunCompletion -> unit,
#endif
        ?handler: PtyBackendHandler,
        ?agentProvider: unit -> AgentRecord list
    ) =

#if !FABLE_COMPILER
    let mailboxWriter = mailbox |> Option.map (fun m -> m.Writer)
#else
    let mailboxSender = mailboxSender
#endif
    let handler = defaultArg handler (fun _ _ -> ())
    let agentProvider = defaultArg agentProvider (fun () -> [])
    let lockObj = obj ()

    let activePtys =
        System.Collections.Generic.Dictionary<PtyId, PtyHandle * ref<bool>>()

#if !FABLE_COMPILER
    member internal _.MailboxWriter = mailboxWriter
#else
    member internal _.MailboxSender = mailboxSender
#endif
    member internal _.Handler = handler
    member internal _.AgentProvider = agentProvider
    member internal _.Ptys = activePtys

    member this.Fork(command: string, ?agentId: string, ?role: AgentRole, ?ptyId: PtyId) : PtyId =
        let id =
            defaultArg ptyId (PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8)))

        let handle =
            { Id = id
              Command = command
              StartedAt = DateTimeOffset.UtcNow
              AgentId = agentId
              Role = role }

        let isClosedRef = ref false
        lock lockObj (fun () -> activePtys.[id] <- (handle, isClosedRef))
        handler id (PtyCommand.Spawn command)
        id

    member this.Send(id: PtyId, command: PtyCommand) : unit =
        let target =
            lock lockObj (fun () ->
                match activePtys.TryGetValue(id) with
                | true, v -> Some v
                | _ -> None)

        match target with
        | Some(_, isClosedRef) when not isClosedRef.Value ->
            handler id command

            match command with
            | PtyCommand.Signal(PtySignal.Kill | PtySignal.Terminate) -> this.Close(id, outcome = Ok "signalled")
            | _ -> ()
        | _ -> ()

    member this.List() : AgentRecord list * PtyHandle list =
        let agents = agentProvider ()
        let ptys = lock lockObj (fun () -> activePtys.Values |> Seq.map fst |> Seq.toList)
        (agents, ptys)

    member this.Close(id: PtyId, ?outcome: Result<string, string>) : unit =
        let target =
            lock lockObj (fun () ->
                match activePtys.TryGetValue(id) with
                | true, v -> Some v
                | _ -> None)

        match target with
        | Some(handle, isClosedRef) ->
            let wasAlreadyClosed =
                lock isClosedRef (fun () ->
                    if isClosedRef.Value then
                        true
                    else
                        isClosedRef.Value <- true
                        false)

            if not wasAlreadyClosed then
                handler id (PtyCommand.Signal PtySignal.Terminate)
                lock lockObj (fun () -> activePtys.Remove(id) |> ignore)
#if !FABLE_COMPILER
                mailboxWriter
                |> Option.iter (fun writer ->
                    let completion: RunCompletion =
                        { RunId = id.Value
                          AgentId = defaultArg handle.AgentId id.Value
                          Role = defaultArg handle.Role AgentRole.Executor
                          Outcome = defaultArg outcome (Ok "closed")
                          CompletedAt = DateTimeOffset.UtcNow }

                    writer.TryWrite(completion) |> ignore)
#else
                mailboxSender
                |> Option.iter (fun sender ->
                    let completion: RunCompletion =
                        { RunId = id.Value
                          AgentId = defaultArg handle.AgentId id.Value
                          Role = defaultArg handle.Role AgentRole.Executor
                          Outcome = defaultArg outcome (Ok "closed")
                          CompletedAt = DateTimeOffset.UtcNow }

                    sender completion)
#endif
        | None -> ()

    member this.CloseAll() =
        let ids = lock lockObj (fun () -> activePtys.Keys |> Seq.toList)

        for id in ids do
            this.Close(id)

module Pty =

    let forkPty (port: PtyPort) (command: string) : PtyId = port.Fork(command)

    let forkPtyWith
        (port: PtyPort)
        (command: string)
        (agentId: string option)
        (role: AgentRole option)
        (ptyId: PtyId option)
        : PtyId =
        port.Fork(command, ?agentId = agentId, ?role = role, ?ptyId = ptyId)

    let send (port: PtyPort) (id: PtyId) (command: PtyCommand) : unit = port.Send(id, command)

    let list (port: PtyPort) : AgentRecord list * PtyHandle list = port.List()

    let close (port: PtyPort) (id: PtyId) : unit = port.Close(id)
