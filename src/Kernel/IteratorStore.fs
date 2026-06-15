module VibeFs.Kernel.IteratorStore

open System.Collections.Generic

type private Store(maxIterators: int) =
    let iterators = Dictionary<string, obj>()
    let mutable counter = 0
    member _.Iterators = iterators
    member _.Counter
        with get () = counter
        and set value = counter <- value
    member val MaxIterators = maxIterators with get

let createIteratorStore (maxIterators: int) : obj =
    let cap = if maxIterators > 0 then maxIterators else 200
    box (Store(cap))

let globalIteratorStore : obj = createIteratorStore 200

let storeIterator<'t> (store: obj) (scopeId: string) (namespace': string) (value: 't) : string =
    let s = unbox<Store> store
    s.Counter <- s.Counter + 1
    let id =
        if scopeId = "global" then
            namespace' + string s.Counter
        else
            scopeId + ":" + namespace' + ":" + string s.Counter
    s.Iterators.[id] <- box value
    if s.Iterators.Count > s.MaxIterators then
        let first = Seq.head s.Iterators.Keys
        s.Iterators.Remove(first) |> ignore
    id

let consumeIterator<'t> (store: obj) (id: string) : 't option =
    let s = unbox<Store> store
    match s.Iterators.TryGetValue(id) with
    | true, v ->
        s.Iterators.Remove(id) |> ignore
        Some(unbox<'t> v)
    | false, _ -> None

let clearIteratorScope (store: obj) (scopeId: string) : unit =
    let s = unbox<Store> store
    let prefix = scopeId + ":"
    let keys = s.Iterators.Keys |> Seq.filter (fun k -> k.StartsWith(prefix)) |> Seq.toArray
    for key in keys do
        s.Iterators.Remove(key) |> ignore

let clearIteratorStore (store: obj) : unit =
    let s = unbox<Store> store
    s.Iterators.Clear()
    s.Counter <- 0
