namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

/// Process-local EventStore owners keyed by git common-dir.
///
/// Follow-up: bind `IEventStore.Converge` via `GitGateway.bindEventStore` when a
/// sync runner is available at host boot. Until then `EventStore.create` uses
/// unbound Converge (local append/open only).
///
/// Intentionally free of AgentJournal / SharedAgentJournal / JournalWriter /
/// `.ndjson` / `wanxiangshu-next` tokens (unified-store dual-write gate).
module WorkspaceEventStore =

    /// DSL-state-combination: physical — shared raw+store refcount resource
    type private SharedEntry =
        { Raw: IGitRawStore
          Store: IEventStore
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedEntry>()

    /// Acquire (or bump) the process-local store for `commonDir`.
    /// Non-git / ProcessGitRawStore failures surface as exceptions — fail closed.
    let acquire (commonDir: string) : IGitRawStore * IEventStore =
        if String.IsNullOrWhiteSpace commonDir then
            failwith "WorkspaceEventStore.acquire: commonDir is empty"

        lock gate (fun () ->
            match shared.TryGetValue commonDir with
            | true, entry ->
                entry.RefCount <- entry.RefCount + 1
                entry.Raw, entry.Store
            | false, _ ->
                let raw = ProcessGitRawStore.create commonDir
                let store = EventStore.create raw

                shared.[commonDir] <-
                    { Raw = raw
                      Store = store
                      RefCount = 1 }

                raw, store)

    /// Borrow the already-owned process-local store without changing ownership.
    /// Strength uses this only after AgentJournal boot has acquired the workspace;
    /// `None` means durability is unavailable and new speculation must stay K0.
    let tryCurrent (commonDir: string) : (IGitRawStore * IEventStore) option =
        if String.IsNullOrWhiteSpace commonDir then
            None
        else
            lock gate (fun () ->
                match shared.TryGetValue commonDir with
                | true, entry -> Some(entry.Raw, entry.Store)
                | false, _ -> None)

    /// Drop one refcount for `commonDir`. No-op when unknown.
    let release (commonDir: string) =
        if String.IsNullOrWhiteSpace commonDir then
            ()
        else
            lock gate (fun () ->
                match shared.TryGetValue commonDir with
                | true, entry ->
                    let remaining = entry.RefCount - 1

                    if remaining <= 0 then
                        shared.Remove commonDir |> ignore
                    else
                        entry.RefCount <- remaining
                | false, _ -> ())

    /// Boot port whose ResumeOrCreate resumes/creates via EventStoreJournalWriter.
    let bootPort (commonDir: string) : IJournalEventStoreBoot =
        let raw, store = acquire commonDir

        { new IJournalEventStoreBoot with
            member _.ResumeOrCreate(runtimeId, processId, startedAt) =
                EventStoreJournalWriter.resumeOrCreate (runtimeId, processId, startedAt, store, raw) }
