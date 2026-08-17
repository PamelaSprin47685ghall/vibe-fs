namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Persistence.EventStore

/// JS-native Casebook index boundary. Public entries contain only a shelfmark
/// and canonical question; durable session identity remains inside the owner.
module CasebookIndexSurface =

    let private storeOf (value: obj) : IEventStore = (unbox<EventStoreHandle> value).Store

    let private observationToJs (observation: Observation) : obj =
        match observation with
        | Observation.FileRead(path, contentHash) ->
            box
                {| kind = "file-read"
                   path = path
                   contentHash = contentHash |}
        | Observation.GlobResult(pattern, paths) ->
            box
                {| kind = "glob-result"
                   pattern = pattern
                   paths = List.toArray paths |}
        | Observation.GrepResult(pattern, matches) ->
            let values =
                matches
                |> List.map (fun (path, index, text) -> box [| box path; box index; box text |])
                |> List.toArray

            box
                {| kind = "grep-result"
                   pattern = pattern
                   matches = values |}

    let private caseToJs (case: Case) : obj =
        box
            {| sessionId = case.SessionId
               q = case.Q
               a = case.A
               observations = case.Observations |> List.map observationToJs |> List.toArray
               lastAccessOrder = case.LastAccessOrder |}

    let private entryToJs (entry: CasebookIndex.Entry) : obj =
        box
            {| shelfmark = entry.Shelfmark
               question = entry.Question |}

    let private snapshotToJs (snapshot: CasebookIndex.Snapshot) : obj =
        box
            {| epoch = snapshot.Epoch
               cases = snapshot.Cases |> List.map entryToJs |> List.toArray |}

    /// Current frozen provider-visible snapshot, or `null` before first refresh.
    let tryGet () : obj =
        match CasebookIndex.tryGet () with
        | None -> null
        | Some snapshot -> snapshotToJs snapshot

    /// Mark the provider-visible index dirty for its next refresh.
    let invalidate () : unit = CasebookIndex.invalidate ()

    /// Stable provider-visible shelfmark for a durable Case identity.
    let shelfmarkFor (sessionId: string) (canonicalQuestion: string) : string =
        CasebookIndex.shelfmarkFor sessionId canonicalQuestion

    /// Rebuild the provider-visible snapshot from unified EventStore Current.
    let refresh (store: obj) (capacity: int) : Task<obj> =
        task {
            let! snapshot = CasebookIndex.refresh (storeOf store) capacity
            return snapshotToJs snapshot
        }

    /// Resolve a shelfmark without exposing internal index records.
    let resolve (store: obj) (capacity: int) (shelfmark: string) : Task<obj> =
        task {
            match! CasebookIndex.resolve (storeOf store) capacity shelfmark with
            | Error message -> return box {| ok = false; error = message |}
            | Ok None -> return box {| ok = true; value = null |}
            | Ok(Some case) -> return box {| ok = true; value = caseToJs case |}
        }
