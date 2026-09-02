namespace Wanxiangshu.Repository.Knowledge.Casebook

open System.Threading.Tasks

/// JS-native semantic surface for Casebook laws (PR 7 exemplar).
///
/// A JS test expresses observations / events / cases in plain JS:
///
/// ```js
/// const normalized = casebook.normalize([
///   { kind: 'file-read', path: 'a.txt', contentHash: 'h1' },
///   { kind: 'file-read', path: 'a.txt', contentHash: 'h1' },
/// ])
/// // [{ kind: 'file-read', path: 'a.txt', contentHash: 'h1' }]
///
/// const world = casebook.emptyWorld()
/// const next = casebook.applyEvent(world,
///   { kind: 'case-captured', case: { sessionId: 's1', q: 'Q', a: 'A', observations: [] } })
/// // { ok: true, world: { accessCounter: 1, cases: [{ sessionId: 's1', ... }] } }
/// ```
///
/// The F# `Observation` / `CasebookEvent` / `Case` unions stay inside the
/// surface; translation happens at the owner boundary
/// (JS-SEMANTIC-SURFACE-002/003/005). `store` is the opaque EventStoreHandle
/// created by EventStoreSurface — passed back, never inspected by JS.
module CasebookSurface =

    /// Stable SHA-256 fingerprint for a JS-native FileRead observation.
    val contentHash: text: string -> string

    /// Capture a typed observation from a tool execution. An unrecognized or
    /// incomplete execution returns `null`, not an F# option.
    val capture: toolName: string -> args: obj -> output: string -> obj

    /// Parse a typed executor read command. Unsupported shell forms return
    /// `null`; the parser never exposes its F# option representation.
    val ofExecCommand: command: string -> obj

    /// Normalize a JS observation array: same identity → one entry; glob
    /// paths order-insensitive. Returns the normalized JS array.
    val normalize: observations: obj array -> obj array

    /// CASE-003 replay classification:
    /// `'fresh'` only on exact normalized equality, `'stale'` otherwise.
    val classifyReplay: stored: obj array -> replayed: obj array -> string

    /// Return the empty JS-native projection passed to `applyEvent`.
    val emptyWorld: unit -> obj

    /// Apply exactly one JS casebook event through the owner projection oracle.
    /// Returns `{ ok: true, world }` or `{ ok: false, error }`.
    val applyEvent: world: obj -> event: obj -> obj

    /// CASE-008 LRU eviction. JS Cases in, `{ kept, victims }` out.
    val evict: capacity: int -> cases: obj array -> obj

    /// Read one Case from the durable Current projection. The store is an
    /// opaque capability; both the result envelope and Case are JS-native.
    val fetchCase: store: obj -> capacity: int -> sessionId: string -> Task<obj>

    /// Append a Refreshed event after translating observations at the owner
    /// boundary.
    val refresh: store: obj -> sessionId: string -> q: string -> a: string -> observations: obj array -> Task<obj>

    /// Report whether replay against the current worktree requires refresh.
    val needsRefresh: store: obj -> capacity: int -> sessionId: string -> root: string -> Task<obj>

    /// Append a CaseAccessed event through the Casebook workflow owner.
    val touchAccess: store: obj -> sessionId: string -> Task<obj>

    /// Append an eviction tombstone through the durable Casebook store owner.
    val evictCase: store: obj -> sessionId: string -> Task<obj>

    /// Feature marker gate exposed without leaking the workflow module.
    val featureEnabled: workspaceRoot: string -> bool

    /// CASE-010 exactly-once finalize. `{ ok: true } | { ok: false, error }`.
    val finalize: store: obj -> case: obj -> Task<obj>

    /// Archive one Inspector result. `{ ok: true } | { ok: false, error }`.
    val archive: store: obj -> case: obj -> Task<obj>
