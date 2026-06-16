module VibeFs.Kernel.Lru

/// An LRU cache as immutable data.  The item map is keyed by token; a separate
/// order list tracks least- to most-recently-used so semantics survive compilation.
type LruStore<'t> = { cache: Map<string, 't>; order: string list; maxSize: int }

let create (maxSize: int) : LruStore<'t> = { cache = Map.empty; order = []; maxSize = maxSize }

let private removeKey key order = List.except [ key ] order
let private trimHead order maxSize =
    if List.length order > maxSize then List.tail order else order

let set (store: LruStore<'t>) (key: string) (value: 't) : LruStore<'t> =
    let newOrder = trimHead (removeKey key store.order @ [ key ]) store.maxSize
    let evicted = store.order |> List.tryHead
    let newCache =
        match evicted with
        | Some k when List.length store.order >= store.maxSize && not (Map.containsKey key store.cache) -> Map.remove k store.cache
        | _ -> store.cache
    { store with cache = Map.add key value newCache; order = newOrder }

/// Read and promote to most-recent; returns the new store and the value.
let get (store: LruStore<'t>) (key: string) : LruStore<'t> * 't option =
    match Map.tryFind key store.cache with
    | None -> store, None
    | Some value -> set store key value, Some value

let lruGet = get

let peek (store: LruStore<'t>) (key: string) : 't option =
    Map.tryFind key store.cache

/// Read and remove (single-use tokens).
let consume (store: LruStore<'t>) (key: string) : LruStore<'t> * 't option =
    match Map.tryFind key store.cache with
    | None -> store, None
    | Some value -> { store with cache = Map.remove key store.cache; order = removeKey key store.order }, Some value

let delete (store: LruStore<'t>) (key: string) : LruStore<'t> =
    { store with cache = Map.remove key store.cache; order = removeKey key store.order }

let clear (store: LruStore<'t>) : LruStore<'t> = { store with cache = Map.empty; order = [] }
let size (store: LruStore<'t>) : int = Map.count store.cache

/// A two-level LRU: per-scope stores, themselves LRU-evicted globally.
type ScopedLruStore<'t> =
    { scopes: Map<string, LruStore<'t>>
      scopeOrder: string list
      perScopeLimit: int
      globalScopeLimit: int }

let createScoped (perScopeLimit: int) (globalScopeLimit: int) : ScopedLruStore<'t> =
    { scopes = Map.empty; scopeOrder = []; perScopeLimit = perScopeLimit; globalScopeLimit = globalScopeLimit }

let private ensureScope (store: ScopedLruStore<'t>) (scopeId: string)
                        : ScopedLruStore<'t> * LruStore<'t> =
    match Map.tryFind scopeId store.scopes with
    | Some scope ->
        let promotedOrder = removeKey scopeId store.scopeOrder @ [ scopeId ]
        let trimmedOrder = trimHead promotedOrder store.globalScopeLimit
        { store with scopeOrder = trimmedOrder }, scope
    | None ->
        let fresh = create store.perScopeLimit : LruStore<'t>
        let newOrder = removeKey scopeId store.scopeOrder @ [ scopeId ]
        let trimmedOrder = trimHead newOrder store.globalScopeLimit
        let evicted = store.scopeOrder |> List.tryHead
        let newScopes =
            match evicted with
            | Some k when List.length store.scopeOrder >= store.globalScopeLimit -> Map.remove k store.scopes
            | _ -> store.scopes
        { store with scopes = Map.add scopeId fresh newScopes; scopeOrder = trimmedOrder }, fresh

let scopedSet (store: ScopedLruStore<'t>) (scopeId: string) (token: string) (value: 't)
              : ScopedLruStore<'t> =
    let afterEnsure, scope = ensureScope store scopeId
    let updated = set scope token value
    { afterEnsure with scopes = Map.add scopeId updated afterEnsure.scopes }

let scopedPeek (store: ScopedLruStore<'t>) (scopeId: string) (token: string) : 't option =
    Map.tryFind scopeId store.scopes |> Option.bind (fun scope -> peek scope token)

let scopedConsume (store: ScopedLruStore<'t>) (scopeId: string) (token: string)
                  : ScopedLruStore<'t> * 't option =
    match Map.tryFind scopeId store.scopes with
    | None -> store, None
    | Some scope ->
        let updated, value = consume scope token
        { store with scopes = Map.add scopeId updated store.scopes }, value

let scopedClearScope (store: ScopedLruStore<'t>) (scopeId: string) : ScopedLruStore<'t> =
    { store with scopes = Map.remove scopeId store.scopes; scopeOrder = removeKey scopeId store.scopeOrder }

let scopedClear (store: ScopedLruStore<'t>) : ScopedLruStore<'t> =
    { store with scopes = Map.empty; scopeOrder = [] }
