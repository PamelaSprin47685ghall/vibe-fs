namespace Wanxiangshu.Persistence.EventStore

open Fable.Core
open Fable.Core.JsInterop

/// durable-convergence-001..003 / durable-events-014.
/// The single k-way merge implementation lives in wanxiangli/convergence
/// (CUT-19). This module is the typed F# projection over EventEnvelope /
/// StorageInvalid; it owns no ordering rule, reads no files and folds no
/// business projection.
[<RequireQualifiedAccess>]
module EventKWayMerge =

    type MergeObservation =
        { Result: Result<EventEnvelope list, StorageInvalid>
          ReadyComparisons: int64 }

    [<Import("KWayMerge", "wanxiangli/convergence")>]
    let private mergeClass: obj = jsNative

    [<Emit("new $0()")>]
    let private construct (cls: obj) : obj = jsNative

    // Stateless library object; one shared instance.
    let private merger: obj = construct mergeClass

    [<Emit("new Map()")>]
    let private newIdentityMap () : obj = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private remember (map: obj) (key: obj) (value: EventEnvelope) : unit = jsNative

    [<Emit("$0.get($1)")>]
    let private recall (map: obj) (key: obj) : EventEnvelope = jsNative

    /// The library returns the caller's event objects by reference; an identity
    /// map turns them back into the exact envelopes that were supplied.
    let private observe (operation: string) (streams: (string * EventEnvelope list) list) : MergeObservation =
        let originals = newIdentityMap ()

        let input =
            streams
            |> List.map (fun (writerId, events) ->
                let converted =
                    events
                    |> List.map (fun envelope ->
                        let value = CanonicalEventCodec.toLibraryEvent envelope
                        remember originals value envelope
                        value)
                    |> Array.ofList

                box [| box writerId; box converted |])
            |> Array.ofList

        let outcome: obj =
            if operation = "mergeRetained" then
                merger?mergeRetained input
            else
                merger?merge input

        let comparisons = int64 (unbox<float> outcome?readyComparisons)

        let result =
            if unbox<bool> outcome?ok then
                Ok(
                    (unbox<obj array> outcome?events)
                    |> Array.map (recall originals)
                    |> Array.toList
                )
            else
                Error(CanonicalEventCodec.invalidOfLibraryError outcome?error)

        { Result = result
          ReadyComparisons = comparisons }

    /// Strict merge for complete histories. Any parent absent from the supplied
    /// writer set is storage corruption.
    let merge (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        (observe "merge" streams).Result

    /// Retention-window merge. A parent absent from the entire retained set is a
    /// causal predecessor before the truncation boundary and is considered
    /// satisfied. Dependencies that are present inside the retained set still
    /// participate in ordering and cycle detection.
    let mergeRetained (streams: (string * EventEnvelope list) list) : Result<EventEnvelope list, StorageInvalid> =
        (observe "mergeRetained" streams).Result

    let mergeWithDiagnostics (streams: (string * EventEnvelope list) list) : MergeObservation = observe "merge" streams
