namespace Wanxiangshu.Journal

open System
open System.Collections.Generic
open Wanxiangshu.Kernel.Identity

/// Process-local shared AgentJournal owners.
///
/// OpenCode loads the plugin once for the root workspace and again for each
/// manager worktree. ReviewGuard, Fallback and Authority facts must remain ONE
/// projection per git common-dir runtime path — two projections over the same
/// facts is the split-brain that FALLBACK-003 and REVIEW-003 both depend on not
/// happening.
module SharedAgentJournal =

    type private SharedJournal =
        { Journal: AgentJournal
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedJournal>()

    /// PERSIST-004: an unfoldable journal stops startup.
    ///
    /// Returns the rejection instead of throwing, so the composition root decides
    /// how to fail closed — it is the only layer that knows how to report a
    /// startup refusal to the Host.
    let acquire (directory: string) (processId: int) (startedAt: DateTimeOffset) : Result<AgentJournal, FoldRejection> =
        lock gate (fun () ->
            match shared.TryGetValue directory with
            | true, entry ->
                entry.RefCount <- entry.RefCount + 1
                Ok entry.Journal
            | false, _ ->
                let boot = Boot.boot directory
                let runtimeId = RuntimeId.create (Guid.NewGuid().ToString("N").Substring(0, 12))

                AgentJournal.createFromBoot directory runtimeId processId startedAt boot
                |> Result.map (fun journal ->
                    shared.[directory] <- { Journal = journal; RefCount = 1 }
                    journal))

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
