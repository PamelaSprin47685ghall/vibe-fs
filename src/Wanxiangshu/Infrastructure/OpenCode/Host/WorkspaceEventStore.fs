namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open Wanxiangshu.Infrastructure
open Wanxiangshu.Infrastructure.Persist
open Wanxiangshu.Persistence.Journal

/// Process-local EventStore owners keyed by git common-dir.
/// One acquired entry owns exactly one WriterId.ndjson and one CanonicalIntegrator.
/// No GitRawStore is created on the runtime append/replay path.
module WorkspaceEventStore =

    /// DSL-state-combination: physical — shared local writer + canonical Current.
    type private SharedEntry =
        { Store: IEventStore
          mutable RefCount: int }

    let private gate = obj ()
    let private shared = Dictionary<string, SharedEntry>()

    let acquire (commonDir: string) : IEventStore =
        if String.IsNullOrWhiteSpace commonDir then
            failwith "WorkspaceEventStore.acquire: commonDir is empty"

        lock gate (fun () ->
            match shared.TryGetValue commonDir with
            | true, entry ->
                entry.RefCount <- entry.RefCount + 1
                entry.Store
            | false, _ ->
                let writerId = Guid.NewGuid().ToString("N")
                let integrator = CanonicalIntegrator.create ()
                let store = EventStore.createLocal commonDir writerId integrator
                JsToolsTransactionStore.recoverCurrent store

                shared.[commonDir] <-
                    { Store = store
                      RefCount = 1 }

                store)

    /// Borrow the already-owned process-local store without changing ownership.
    let tryCurrent (commonDir: string) : IEventStore option =
        if String.IsNullOrWhiteSpace commonDir then
            None
        else
            lock gate (fun () ->
                match shared.TryGetValue commonDir with
                | true, entry -> Some entry.Store
                | false, _ -> None)

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

    let bootPort (commonDir: string) : IJournalEventStoreBoot =
        let store = acquire commonDir

        { new IJournalEventStoreBoot with
            member _.ResumeOrCreate(runtimeId, processId, startedAt) =
                EventStoreJournalWriter.resumeOrCreate (runtimeId, processId, startedAt, store) }
