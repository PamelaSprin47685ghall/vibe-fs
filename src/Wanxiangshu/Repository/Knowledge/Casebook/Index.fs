namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

/// Process-local Casebook index frozen for the current provider epoch.
/// Provider entries expose only a shelfmark plus canonical Q; durable session
/// identity remains an internal lookup key and never crosses the Casebook wire.
module CasebookIndex =

    type Entry = { Shelfmark: string; Question: string }

    type Snapshot = { Epoch: int64; Cases: Entry list }

    let private gate = obj ()
    // DSL-MUTABLE: resource
    let mutable private frozen: Snapshot option = None
    // DSL-MUTABLE: resource
    let mutable private dirty = true

    /// knowledge-reuse-008/012: titles, shelfmarks, entry order, shelfmark
    /// location and the epoch-advance verdict are wanxiangli/casebook
    /// `CasebookIndexLaw` (CUT-27). This module keeps only the process-wide
    /// frozen snapshot, the dirty mark and the EventStore read.
    [<Import("CasebookIndexLaw", "wanxiangli/casebook")>]
    let private lawClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    let private law: obj = construct lawClass

    let tryGet () : Snapshot option = lock gate (fun () -> frozen)

    /// Force the next successful refresh to advance epoch (Captured/Refreshed/Evicted).
    let invalidate () : unit = lock gate (fun () -> dirty <- true)

    /// Stable public locator. The suffix is a one-way catalog discriminator,
    /// never the durable session identity itself.
    let shelfmarkFor (sessionId: string) (canonicalQuestion: string) : string =
        unbox<string> (law?shelfmarkFor (sessionId, canonicalQuestion))

    let private casesInOrder (cases: Map<string, Case>) : Case array = cases |> Map.toArray |> Array.map snd

    let private indexCasesOf (cases: Case array) : obj array =
        cases
        |> Array.map (fun case -> createObj [ "sessionId" ==> case.Identity; "question" ==> case.Q ])

    let private entryOfJs (value: obj) : Entry =
        { Shelfmark = unbox<string> value?shelfmark
          Question = unbox<string> value?question }

    let private entryToJs (entry: Entry) : obj =
        createObj [ "shelfmark" ==> entry.Shelfmark; "question" ==> entry.Question ]

    let private publicEntries (cases: Map<string, Case>) : Entry list =
        unbox<obj array> (law?entries (indexCasesOf (casesInOrder cases)))
        |> Array.map entryOfJs
        |> Array.toList

    let private frozenOrEmpty () =
        match frozen with
        | Some snapshot -> snapshot
        | None -> { Epoch = 0L; Cases = [] }

    let private freezeEntries (entries: Entry list) =
        let previous: obj =
            match frozen with
            | None -> null
            | Some snapshot -> box (snapshot.Cases |> List.map entryToJs |> List.toArray)

        let advances =
            unbox<bool> (law?advancesEpoch (previous, dirty, entries |> List.map entryToJs |> List.toArray))

        let previousEpoch =
            frozen |> Option.map (fun snapshot -> snapshot.Epoch) |> Option.defaultValue -1L

        let epoch = if advances then previousEpoch + 1L else previousEpoch
        dirty <- false

        let snapshot = { Epoch = epoch; Cases = entries }
        frozen <- Some snapshot
        snapshot

    let private project (store: IEventStore) (capacity: int) : Result<Map<string, Case>, string> =
        match store.TryCurrent "Casebook" with
        | None -> Ok Map.empty
        | Some current ->
            let state = unbox<CasebookProjection.State> current
            let cases = CasebookProjection.evict capacity state.Cases |> fst
            Ok cases

    /// Resolve a public shelfmark to its internal Case without exposing the
    /// durable session key. Namesakes (same title and a colliding 32-bit
    /// discriminator) resolve to the first in index order.
    let resolve (store: IEventStore) (capacity: int) (shelfmark: string) : Task<Result<Case option, string>> =
        task {
            match project store capacity with
            | Error error -> return Error error
            | Ok cases ->
                let ordered = casesInOrder cases
                let position = unbox<int> (law?locate (indexCasesOf ordered, shelfmark))
                return Ok(if position < 0 then None else Some ordered.[position])
        }

    /// Rebuild from the unified EventStore projection. Epoch advances when the
    /// provider-visible index changes or an explicit invalidation occurred.
    let refresh (store: IEventStore) (capacity: int) : Task<Snapshot> =
        task {
            let projected = project store capacity

            return
                lock gate (fun () ->
                    match projected with
                    | Error _ -> frozenOrEmpty ()
                    | Ok cases -> publicEntries cases |> freezeEntries)
        }
