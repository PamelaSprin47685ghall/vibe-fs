namespace Wanxiangshu.Execution.Delegation

/// JS-native projection boundary for durable handle lifecycle. The projection
/// remains the delegation owner's typed implementation; JS receives snapshots,
/// never the union or map representation.
///
/// This surface owns the direct-projection command API (link / complete /
/// abandon / retire / read / views). Fact-based fold replay lives in
/// `Handle/FoldSurface.fs`, which calls the production `ExecutionFactFold`
/// directly — no second interpreter here.
module HandleSurface =

    val scenario: action: string -> obj

    /// Crash-reconciliation matrix at the handle owner. Duplicate completion
    /// and retirement are replayed through the same projection transitions.
    val crashScenario: action: string -> obj

    /// JS-native proof surface for the optional string adapter used by record
    /// snapshots. `hasValue` carries the option case without exposing Fable's
    /// option representation to JS.
    val optionalStringTraversal: hasValue: bool -> value: obj -> extract: (obj -> string) -> obj

    /// A projection state that JS holds as an opaque token. JS never reads its
    /// fields; it passes it back to `apply` / `view` / `read`.
    type HandleProjectionState =
        internal new: projection: AgentLinkageProjection -> HandleProjectionState
        member internal Internal: AgentLinkageProjection

    /// The empty projection. JS starts every scenario from this.
    val empty: unit -> HandleProjectionState

    /// Apply one lifecycle command to a projection state.
    ///
    /// Commands:
    ///   { op: "link", handle, child, agent, role, ownership? }
    ///   { op: "complete", handle, kind?, ref?, digest? }
    ///   { op: "abandon", handle, reason? }
    ///   { op: "retire", handle }
    ///
    /// Unrecognized inputs return `{ ok: false, error: { kind, value } }`.
    val apply: state: HandleProjectionState -> command: obj -> obj

    /// Read one handle record from a projection state. Returns `null` if the
    /// handle is not in the map (never linked, or — impossible for retired —
    /// removed).
    val read: state: HandleProjectionState -> handle: obj -> obj

    /// `isRetired(handle, projection)` — the tombstone question.
    val isRetired: state: HandleProjectionState -> handle: obj -> bool

    /// `isAbandoned(handle, projection)`.
    val isAbandoned: state: HandleProjectionState -> handle: obj -> bool

    /// `tryFind(handle, projection)` — returns the record or `null`. JS uses
    /// `isSome(tryFind(...))` to distinguish "retired" from "never existed".
    val tryFind: state: HandleProjectionState -> handle: obj -> obj

    /// `tryFindByChildSession(child, projection)`.
    val tryFindByChildSession: state: HandleProjectionState -> child: obj -> obj

    /// The three derived views, as sorted `describe()` strings.
    val views: state: HandleProjectionState -> obj

    /// `linkedChildren(projection)` — every child session ever linked, as
    /// record snapshots sorted by creation order.
    val linkedChildren: state: HandleProjectionState -> obj array

    /// `reportableAbandoned(projection)` — count of Abandoned handles join
    /// must include in the next batch.
    val reportableAbandonedCount: state: HandleProjectionState -> int

    /// `handleId.agent('h1')` → `"agent:h1"`. The JS-visible handle identity
    /// is the `describe()` string; the surface parses it back into the typed
    /// union on every command.
    val handleIdAgent: value: string -> string
    val handleIdPty: value: string -> string
    val handleIdManagerJob: value: string -> string
    val handleIdDescribe: value: string -> string
    val handleIdTryAgent: value: string -> obj

    val serializeFact: value: obj -> string
    val deserializeFact: line: string -> obj
