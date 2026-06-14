module VibeFs.Kernel.IteratorStore

open Fable.Core

/// Create a fresh iterator store bound to a max size.
[<Emit("(() => { const s = { iterators: new Map(), counter: 0, maxIterators: $0 || 200 }; return s; })()")>]
let createIteratorStore (maxIterators: int) : obj = jsNative

let globalIteratorStore : obj = createIteratorStore 200

/// Store a value under a fresh scope-keyed id, evicting the oldest when full.
/// Args: $0=store, $1=scopeId, $2=namespace, $3=value.
[<Emit("(() => { const s = $0; s.counter = s.counter + 1; const id = ($1 === 'global') ? ($2 + s.counter) : ($1 + ':' + $2 + ':' + s.counter); s.iterators.set(id, $3); if (s.iterators.size > s.maxIterators) { const first = s.iterators.keys().next().value; if (first !== undefined) s.iterators.delete(first); } return id; })($0, $1, $2, $3)")>]
let storeIterator<'t> (store: obj) (scopeId: string) (namespace': string) (value: 't) : string = jsNative

/// Consume (read-and-remove) an iterator value; single-use semantics.
[<Emit("(() => { const s = $0; const v = s.iterators.get($1); if (v === undefined) return null; s.iterators.delete($1); return v; })($0, $1)")>]
let private consumeRaw (store: obj) (id: string) : obj = jsNative

let consumeIterator<'t> (store: obj) (id: string) : 't option =
    let v = consumeRaw store id
    if isNull v then None else Some(unbox<'t> v)

/// Remove every iterator belonging to a scope.
[<Emit("(() => { const s = $0; const prefix = $1 + ':'; for (const key of Array.from(s.iterators.keys())) { if (key.startsWith(prefix)) s.iterators.delete(key); } })($0, $1)")>]
let clearIteratorScope (store: obj) (scopeId: string) : unit = jsNative

/// Wipe the entire store.
[<Emit("(() => { const s = $0; s.iterators.clear(); s.counter = 0; })($0)")>]
let clearIteratorStore (store: obj) : unit = jsNative
