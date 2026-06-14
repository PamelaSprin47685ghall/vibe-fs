module VibeFs.Kernel.Lru

/// An LRU cache as immutable data.  The item list is insertion-ordered:
/// head = least-recently-used, tail = most-recently-used.  A pure list backbone
/// replaces JS Map so semantics survive compilation unchanged.
type LruStore<'t> = { items: (string * 't) list; maxSize: int }

let create (maxSize: int) : LruStore<'t> = { items = []; maxSize = maxSize }

let private removeKey key items = items |> List.filter (fun (k, _) -> k <> key)
let private trimHead items maxSize =
    if List.length items > maxSize then List.tail items else items

let set (store: LruStore<'t>) (key: string) (value: 't) : LruStore<'t> =
    let added = removeKey key store.items @ [(key, value)]
    { store with items = trimHead added store.maxSize }

/// Read and promote to most-recent; returns the new store and the value.
let get (store: LruStore<'t>) (key: string) : LruStore<'t> * 't option =
    match List.tryFind (fun (k, _) -> k = key) store.items with
    | None -> store, None
    | Some(_, value) -> set store key value, Some value

let peek (store: LruStore<'t>) (key: string) : 't option =
    store.items |> List.tryFind (fun (k, _) -> k = key) |> Option.map snd

/// Read and remove (single-use tokens).
let consume (store: LruStore<'t>) (key: string) : LruStore<'t> * 't option =
    match List.tryFind (fun (k, _) -> k = key) store.items with
    | None -> store, None
    | Some(_, value) -> { store with items = removeKey key store.items }, Some value

let delete (store: LruStore<'t>) (key: string) : LruStore<'t> =
    { store with items = removeKey key store.items }

let clear (store: LruStore<'t>) : LruStore<'t> = { store with items = [] }
let size (store: LruStore<'t>) : int = List.length store.items

/// A two-level LRU: per-scope stores, themselves LRU-evicted globally.
/// Scopes list is insertion-ordered too (head = oldest scope).
type ScopedLruStore<'t> =
    { scopes: (string * LruStore<'t>) list
      perScopeLimit: int
      globalScopeLimit: int }

let createScoped (perScopeLimit: int) (globalScopeLimit: int) : ScopedLruStore<'t> =
    { scopes = []; perScopeLimit = perScopeLimit; globalScopeLimit = globalScopeLimit }

let private removeScope scopeId scopes = scopes |> List.filter (fun (s, _) -> s <> scopeId)

let private ensureScope (store: ScopedLruStore<'t>) (scopeId: string)
                        : ScopedLruStore<'t> * LruStore<'t> =
    match List.tryFind (fun (s, _) -> s = scopeId) store.scopes with
    | Some(_, scope) ->
        let promoted = { store with scopes = trimHead (removeScope scopeId store.scopes @ [(scopeId, scope)]) store.globalScopeLimit }
        promoted, scope
    | None ->
        let fresh = create store.perScopeLimit : LruStore<'t>
        { store with scopes = trimHead (store.scopes @ [(scopeId, fresh)]) store.globalScopeLimit }, fresh

let scopedSet (store: ScopedLruStore<'t>) (scopeId: string) (token: string) (value: 't)
              : ScopedLruStore<'t> =
    let afterEnsure, scope = ensureScope store scopeId
    let updated = set scope token value
    { afterEnsure with scopes = removeScope scopeId afterEnsure.scopes @ [(scopeId, updated)] }

let scopedPeek (store: ScopedLruStore<'t>) (scopeId: string) (token: string) : 't option =
    store.scopes
    |> List.tryFind (fun (s, _) -> s = scopeId)
    |> Option.bind (fun (_, scope) -> peek scope token)

let scopedConsume (store: ScopedLruStore<'t>) (scopeId: string) (token: string)
                  : ScopedLruStore<'t> * 't option =
    match List.tryFind (fun (s, _) -> s = scopeId) store.scopes with
    | None -> store, None
    | Some(_, scope) ->
        let updated, value = consume scope token
        { store with scopes = removeScope scopeId store.scopes @ [(scopeId, updated)] }, value

let scopedClearScope (store: ScopedLruStore<'t>) (scopeId: string) : ScopedLruStore<'t> =
    { store with scopes = removeScope scopeId store.scopes }

let scopedClear (store: ScopedLruStore<'t>) : ScopedLruStore<'t> = { store with scopes = [] }
