namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations write as-is: each edit/write is atomically persisted
/// through MutationFs at call time; the staging buffer records what was
/// actually written (JS-012).
module JsToolsBindings =
    /// The host loop behind `js.call`, injected by the workflow.
    type JsCallHost =
        { Loop: Wanxiangshu.Process.JsSandbox.ParkState option -> obj -> string -> Task<obj> }

    type JsSearchHost =
        { Search: string array -> bool -> Task<obj> }

    val createLedger: persistence: IJsTransactionPersistence option -> JsAsIsLedger

    val makeAskMember:
        state: Wanxiangshu.Process.JsSandbox.ParkState ->
        invoke: (string -> Task<Result<unit, string>>) ->
            (string * obj)

    val scopePlanFile: root: string -> canWrite: (unit -> bool) -> api: obj -> obj

    /// `js.horizon()`: the `(memberName, member)` pair for the horizon roster
    /// envelope; refuses with DELEGATION_UNAVAILABLE when no host is installed.
    val makeHorizonMember: host: JsDelegationHost option -> (string * obj)

    /// `js.advisor(intent, view)`: the `(memberName, member)` pair for the
    /// advisor consult; refuses with DELEGATION_UNAVAILABLE when no host is
    /// installed and returns the host closure's envelope verbatim otherwise.
    val makeAdvisorMember: host: JsDelegationHost option -> (string * obj)

    val createApi:
        capabilities: Set<JsCapability> ->
        root: string ->
        staging: ResizeArray<JsStagedMutation> ->
        readSnapshots: ResizeArray<JsReadSnapshot> ->
        explicitReads: ResizeArray<JsExplicitFileRead> ->
        ledger: JsAsIsLedger ->
        runHost: JsCallHost option ->
        searchHost: JsSearchHost option ->
        delegationHost: JsDelegationHost option ->
        askInvoke: (string -> Task<Result<unit, string>>) option ->
            obj

    /// Build the api object for one sandbox run that carries a park lifecycle
    /// state. `parkState` is the mutable object shared with the sandbox wrapper;
    /// the generated `js.park` member suspends through it (PLAN S2 step 4).
    val createApiWithPark:
        capabilities: Set<JsCapability> ->
        root: string ->
        staging: ResizeArray<JsStagedMutation> ->
        readSnapshots: ResizeArray<JsReadSnapshot> ->
        explicitReads: ResizeArray<JsExplicitFileRead> ->
        ledger: JsAsIsLedger ->
        parkState: Wanxiangshu.Process.JsSandbox.ParkState ->
        runHost: JsCallHost option ->
        searchHost: JsSearchHost option ->
        delegationHost: JsDelegationHost option ->
        askInvoke: (string -> Task<Result<unit, string>>) option ->
            obj

module JsReviewOnly =
    val scopeReviewOnly: check: (unit -> Task<bool>) -> api: obj -> obj
