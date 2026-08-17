namespace Wanxiangshu.Process

open System
open System.Collections.Generic
open System.Text

/// PTY id minting, byte encoding and the cross-runtime parent-abort registry.
///
/// The per-operation wrappers (`forkPty`, `send`, `complete`, `list`, `close`) are
/// gone: `HostForkPty` calls `PtyPort` directly, so each wrapper was a second
/// spelling of one member with no caller. `forkPtyWith` in particular still passed
/// an optional `Role`, which is the signature EXEC-015 replaced with a
/// required `ManagedAgent` — keeping it would preserve a way to open a PTY without
/// a managed identity.
module Pty =
    [<Literal>]
    let AgentName = "pty"

    let bytes (text: string) = Encoding.UTF8.GetBytes text

    let newId () =
        PtyId("pty-" + Guid.NewGuid().ToString("N").Substring(0, 8))

    let private parentGate = obj ()
    let private parentAborters = Dictionary<string, Dictionary<int, unit -> unit>>()
    // DSL-MUTABLE: resource — monotonic abort-token counter under parentGate
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

    let private removeCallback parentId token (callbacks: Dictionary<int, unit -> unit>) =
        callbacks.Remove token |> ignore

        if callbacks.Count = 0 then
            parentAborters.Remove parentId |> ignore

    let unregisterParentAbort (parentId: string) (token: int) =
        lock parentGate (fun () ->
            match parentAborters.TryGetValue parentId with
            | true, callbacks -> removeCallback parentId token callbacks
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
