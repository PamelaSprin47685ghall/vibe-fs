namespace Wanxiangshu.Next.Process

open System
open System.Collections.Generic
open System.Text
open Wanxiangshu.Next.Session

/// Public API over PtyPort: fork/send/complete/list helpers and the
/// cross-runtime parent-abort registry. Kept in its own file so Pty.fs stays
/// focused on the typed port boundary (architecture gate: files <= 300 lines).
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
