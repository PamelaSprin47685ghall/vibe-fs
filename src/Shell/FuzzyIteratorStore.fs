module Wanxiangshu.Shell.FuzzyIteratorStore

open Wanxiangshu.Kernel.FuzzyQuery

type GrepIteratorState = { core: FuzzyGrepState; cursor: obj option }

let findIteratorNamespace = "ffi_f"
let grepIteratorNamespace = "ffi_i"

type TypedIteratorStore =
    private
        { mutable findIterators: Map<string, FuzzyFindState>
          mutable grepIterators: Map<string, GrepIteratorState>
          mutable counter: int
          maxIterators: int }

let createTypedIteratorStore (maxIterators: int) : TypedIteratorStore =
    { findIterators = Map.empty
      grepIterators = Map.empty
      counter = 0
      maxIterators = if maxIterators > 0 then maxIterators else 200 }

let private nextId (store: TypedIteratorStore) (scopeId: string) (namespace': string) : string =
    store.counter <- store.counter + 1
    if scopeId = "global" then namespace' + string store.counter
    else scopeId + ":" + namespace' + ":" + string store.counter

let private trim (max: int) (m: Map<string, 't>) : Map<string, 't> =
    if Map.count m > max then
        let firstKey = m |> Map.toSeq |> Seq.head |> fst
        Map.remove firstKey m
    else m

let private storeTyped (m: Map<string, 't>) (store: TypedIteratorStore) (scopeId: string) (namespace': string) (state: 't) : string * Map<string, 't> =
    let id = nextId store scopeId namespace'
    let updated = m |> Map.add id state |> trim store.maxIterators
    (id, updated)

let private consumeTyped (m: Map<string, 't>) (id: string) : 't option * Map<string, 't> =
    match Map.tryFind id m with
    | Some state -> Some state, Map.remove id m
    | None -> None, m

let storeFindIterator (store: TypedIteratorStore) (scopeId: string) (state: FuzzyFindState) : string =
    let (id, updated) = storeTyped store.findIterators store scopeId findIteratorNamespace state
    store.findIterators <- updated
    id

let storeGrepIterator (store: TypedIteratorStore) (scopeId: string) (state: GrepIteratorState) : string =
    let (id, updated) = storeTyped store.grepIterators store scopeId grepIteratorNamespace state
    store.grepIterators <- updated
    id

let consumeFindIterator (store: TypedIteratorStore) (id: string) : FuzzyFindState option =
    let (opt, updated) = consumeTyped store.findIterators id
    store.findIterators <- updated
    opt

let consumeGrepIterator (store: TypedIteratorStore) (id: string) : GrepIteratorState option =
    let (opt, updated) = consumeTyped store.grepIterators id
    store.grepIterators <- updated
    opt

let clearTypedIteratorScope (store: TypedIteratorStore) (scopeId: string) : unit =
    let prefix = scopeId + ":"
    let keep (k: string) _ = not (k.StartsWith prefix)
    store.findIterators <- Map.filter keep store.findIterators
    store.grepIterators <- Map.filter keep store.grepIterators

let clearTypedIteratorStore (store: TypedIteratorStore) : unit =
    store.findIterators <- Map.empty
    store.grepIterators <- Map.empty
    store.counter <- 0
