namespace Wanxiangshu.Next.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Process-local terminal fan-out keyed by the same workspace runtime path as
/// SharedAgentJournal. OpenCode loads the plugin once for the root workspace and
/// again for each manager worktree; AwaitAgent subscriptions live on the parent
/// instance while child terminal NotifyTerminal often fires on the worktree
/// instance. Without a shared bus, dual-PERFECT can confirm in the journal while
/// OrchestratorHost.reverify never observes the reviewer completion.
module SharedTerminalBus =

    type private SharedPort =
        { Port: Events.HostEventPort
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedPort>()

    let acquire (directory: string) : Events.HostEventPort =
        lock gate (fun () ->
            match shared.TryGetValue directory with
            | true, entry ->
                entry.RefCount <- entry.RefCount + 1
                entry.Port
            | false, _ ->
                let port = Events.HostEventPort()
                shared.[directory] <- { Port = port; RefCount = 1 }
                port)

    let release (directory: string option) (port: Events.HostEventPort option) =
        match directory, port with
        | Some key, Some target ->
            lock gate (fun () ->
                match shared.TryGetValue key with
                | true, entry when obj.ReferenceEquals(entry.Port, target) ->
                    let remaining = entry.RefCount - 1

                    if remaining <= 0 then
                        shared.Remove key |> ignore
                    else
                        entry.RefCount <- remaining
                | _ -> ())
        | _ -> ()

    let tryAcquireForWorkspace (workspace: string option) : (string * Events.HostEventPort) option =
        match workspace with
        | None -> None
        | Some path when String.IsNullOrWhiteSpace path -> None
        | Some path ->
            let key = RuntimePath.forWorkspace path
            Some(key, acquire key)
