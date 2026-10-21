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

    [<Import("CasebookIndexLaw", "wanxiangli/casebook")>]
    let private indexLawConstructor: obj = jsNative

    let private indexLaw: obj = createNew indexLawConstructor ()

    let private gate = obj ()
    // DSL-MUTABLE: resource
    let mutable private frozen: Snapshot option = None
    // DSL-MUTABLE: resource
    let mutable private dirty = true

    let tryGet () : Snapshot option = lock gate (fun () -> frozen)

    /// Force the next successful refresh to advance epoch (Captured/Refreshed/Evicted).
    let invalidate () : unit = lock gate (fun () -> dirty <- true)

    /// Stable public locator. The suffix is a one-way catalog discriminator,
    /// never the durable session identity itself.
    let shelfmarkFor (sessionId: string) (canonicalQuestion: string) : string =
        indexLaw?shelfmarkFor (sessionId, canonicalQuestion) |> unbox<string>

    let private caseViews (cases: (string * Case) array) : obj array =
        cases
        |> Array.map (fun (identity, case) -> createObj [ "sessionId" ==> identity; "question" ==> case.Q ])

    let private entryView (entry: Entry) : obj =
        createObj [ "shelfmark" ==> entry.Shelfmark; "question" ==> entry.Question ]

    let private publicEntries (cases: Map<string, Case>) =
        let views = cases |> Map.toArray |> caseViews

        indexLaw?entries (views)
        |> unbox<obj array>
        |> Array.map (fun entry ->
            { Shelfmark = unbox<string> entry?shelfmark
              Question = unbox<string> entry?question })
        |> Array.toList

    let private frozenOrEmpty () =
        match frozen with
        | Some snapshot -> snapshot
        | None -> { Epoch = 0L; Cases = [] }

    let private freezeEntries entries =
        let previousEpoch =
            frozen |> Option.map (fun snapshot -> snapshot.Epoch) |> Option.defaultValue -1L

        let previous =
            frozen
            |> Option.map (fun snapshot -> snapshot.Cases |> List.map entryView |> List.toArray |> box)
            |> Option.defaultValue null

        let next = entries |> List.map entryView |> List.toArray

        let epoch =
            if indexLaw?advancesEpoch (previous, dirty, next) |> unbox<bool> then
                previousEpoch + 1L
            else
                previousEpoch

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
    /// durable session key. Historical hash collisions select the first namesake.
    let resolve (store: IEventStore) (capacity: int) (shelfmark: string) : Task<Result<Case option, string>> =
        task {
            match project store capacity with
            | Error error -> return Error error
            | Ok cases ->
                let ordered = Map.toArray cases
                let position = indexLaw?locate (caseViews ordered, shelfmark) |> unbox<int>

                return Ok(ordered |> Array.tryItem position |> Option.map snd)
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
