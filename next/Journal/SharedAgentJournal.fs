namespace Wanxiangshu.Next.Journal

open System
open System.Collections.Generic
open Wanxiangshu.Next.Kernel.Identity

/// Process-local shared AgentJournal owners.
///
/// OpenCode can load the plugin once for the root workspace and again for each
/// manager worktree. ReviewGuard / Fallback / Authority facts must remain one
/// projection for that git common-dir runtime path.
module SharedAgentJournal =

    type private SharedJournal =
        { Journal: AgentJournal
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedJournal>()

    let acquire (directory: string) (processId: int) (startedAt: DateTimeOffset) : AgentJournal =
        lock gate (fun () ->
            match shared.TryGetValue directory with
            | true, entry ->
                entry.RefCount <- entry.RefCount + 1
                entry.Journal
            | false, _ ->
                let boot = Boot.boot directory
                let runtimeId = RuntimeId.create (Guid.NewGuid().ToString("N").Substring(0, 12))

                let journal =
                    AgentJournal.createFromBoot directory runtimeId processId startedAt boot

                shared.[directory] <- { Journal = journal; RefCount = 1 }
                journal)

    let release (journal: AgentJournal option) =
        match journal with
        | None -> ()
        | Some target ->
            lock gate (fun () ->
                for KeyValue(directory, entry) in shared |> Seq.toList do
                    if obj.ReferenceEquals(entry.Journal, target) then
                        let remaining = entry.RefCount - 1

                        if remaining <= 0 then
                            shared.Remove directory |> ignore
                            (entry.Journal :> IDisposable).Dispose()
                        else
                            entry.RefCount <- remaining)
