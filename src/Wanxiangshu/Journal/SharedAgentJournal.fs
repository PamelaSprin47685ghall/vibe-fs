namespace Wanxiangshu.Journal

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

/// Process-local shared AgentJournal owners.
///
/// OpenCode loads the plugin once for the root workspace and again for each
/// manager worktree. ReviewGuard, Fallback and Authority facts must remain ONE
/// projection per git common-dir runtime path — two projections over the same
/// facts is the split-brain that FALLBACK-003 and REVIEW-003 both depend on not
/// happening.
module SharedAgentJournal =

    /// DSL-state-combination: physical — shared journal owner refcount resource
    type private SharedJournal =
        { Ready: Task<Result<AgentJournal, FoldRejection>>
          mutable Instance: AgentJournal option
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedJournal>()

    /// PERSIST-004: an unfoldable journal stops startup.
    ///
    /// Returns the rejection instead of throwing, so the composition root decides
    /// how to fail closed — it is the only layer that knows how to report a
    /// startup refusal to the Host.
    ///
    /// `openJournal` is supplied by the composition root (EventStore boot port +
    /// `AgentJournal.createFromProjection`). This module stays free of
    /// `IEventStore` / `AppendCandidate` / `EventStore.create*` tokens.
    let acquire
        (directory: string)
        (processId: int)
        (startedAt: DateTimeOffset)
        (openJournal: RuntimeId -> int -> DateTimeOffset -> Task<Result<AgentJournal, FoldRejection>>)
        : Task<Result<AgentJournal, FoldRejection>> =
        let ready =
            lock gate (fun () ->
                match shared.TryGetValue directory with
                | true, entry ->
                    entry.RefCount <- entry.RefCount + 1
                    entry.Ready
                | false, _ ->
                    let runtimeId = RuntimeId.create (Guid.NewGuid().ToString("N").Substring(0, 12))
                    let opening = openJournal runtimeId processId startedAt

                    shared.[directory] <-
                        { Ready = opening
                          Instance = None
                          RefCount = 1 }

                    opening)

        task {
            match! ready with
            | Ok journal ->
                lock gate (fun () ->
                    match shared.TryGetValue directory with
                    | true, entry when obj.ReferenceEquals(entry.Ready, ready) ->
                        entry.Instance <- Some journal
                    | _ -> ())

                return Ok journal
            | Error err ->
                lock gate (fun () ->
                    match shared.TryGetValue directory with
                    | true, entry when obj.ReferenceEquals(entry.Ready, ready) ->
                        shared.Remove directory |> ignore
                    | _ -> ())

                return Error err
        }

    let release (journal: AgentJournal option) =
        match journal with
        | None -> ()
        | Some target ->
            lock gate (fun () ->
                for KeyValue(directory, entry) in shared |> Seq.toList do
                    match entry.Instance with
                    | Some instance when obj.ReferenceEquals(instance, target) ->
                        let remaining = entry.RefCount - 1

                        if remaining <= 0 then
                            shared.Remove directory |> ignore
                            (target :> IDisposable).Dispose()
                        else
                            entry.RefCount <- remaining
                    | _ -> ())
