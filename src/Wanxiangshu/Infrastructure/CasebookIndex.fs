namespace Wanxiangshu.Infrastructure

open Wanxiangshu.Domain
open Wanxiangshu.Infrastructure.Persist

/// Process-local CasebookIndexSnapshot — frozen session-id view for the current
/// index epoch. Not durable; rebuilt from the unified EventStore projection.
/// No feature ref / second authority.
module CasebookIndex =

    type Snapshot =
        { Epoch: int64
          SessionIds: string list }

    let private gate = obj ()
    // DSL-MUTABLE: process-local frozen index
    let mutable private frozen: Snapshot option = None
    let mutable private dirty = true

    let tryGet () : Snapshot option =
        lock gate (fun () -> frozen)

    /// Force the next successful refresh to advance epoch (Captured/Refreshed/Evicted).
    let invalidate () : unit =
        lock gate (fun () -> dirty <- true)

    let private sessionIdsOf (cases: Map<string, Case>) : string list =
        cases |> Map.toList |> List.map fst |> List.sort

    /// Rebuild from store projection. Epoch advances when the session-id set
    /// changes or invalidate was called; otherwise the freeze is reused.
    let refresh (store: IEventStore) (raw: IGitRawStore) (capacity: int) : Snapshot =
        lock gate (fun () ->
            match CasebookStore.loadEvents raw (store.OpenSnapshot()) with
            | Error _ ->
                match frozen with
                | Some s -> s
                | None -> { Epoch = 0L; SessionIds = [] }
            | Ok events ->
                let ids = CasebookStore.project capacity events |> sessionIdsOf

                let prevEpoch =
                    frozen |> Option.map (fun s -> s.Epoch) |> Option.defaultValue -1L

                let setChanged =
                    match frozen with
                    | None -> true
                    | Some s -> s.SessionIds <> ids

                let epoch =
                    if dirty || setChanged then
                        prevEpoch + 1L
                    else
                        prevEpoch

                dirty <- false

                let snap =
                    { Epoch = epoch
                      SessionIds = ids }

                frozen <- Some snap
                snap)
