namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations write as-is: each edit/write is atomically persisted
/// through MutationFs at call time; the staging buffer records what was
/// actually written (JS-012).
module JsToolsBindings =
    /// The host loop behind `call`, injected by the workflow.
    type JsCallHost =
        { Loop: Wanxiangshu.Process.JsSandbox.ParkState option -> obj -> string -> Task<obj> }

    val createLedger: persistence: IJsTransactionPersistence option -> JsAsIsLedger

    val makeWhereAllMember: unit -> (string * obj)
    val makeWhereMember: parkState: Wanxiangshu.Process.JsSandbox.ParkState option -> (string * obj)
    val makeParkMember: parkState: Wanxiangshu.Process.JsSandbox.ParkState -> (string * obj)

    /// `horizon()`: the `(memberName, member)` pair for the horizon roster
    /// envelope; refuses with DELEGATION_UNAVAILABLE when no host is installed.
    val makeHorizonMember: host: JsDelegationHost option -> (string * obj)

    /// `advisor(intent, reportMapping)`: the `(memberName, member)` pair for
    /// the advisor consult; the report mapping is mandatory and a missing or
    /// malformed one refuses with VIEW_DECLARATION_INVALID. It refuses with
    /// DELEGATION_UNAVAILABLE when no host is installed and returns the host
    /// closure's envelope verbatim otherwise.
    val makeAdvisorMember: host: JsDelegationHost option -> (string * obj)

    /// `ask(question)`: the `(memberName, member)` pair for the user
    /// question; it is a park that carries the question, so the program
    /// suspends and resumes with the injected answer.
    val makeAskMember:
        parkState: Wanxiangshu.Process.JsSandbox.ParkState option ->
        ask: (string -> Task<Result<unit, string>>) option ->
            (string * obj)

    val createApi:
        capabilities: Set<JsCapability> ->
        root: string ->
        staging: ResizeArray<JsStagedMutation> ->
        readSnapshots: ResizeArray<JsReadSnapshot> ->
        explicitReads: ResizeArray<JsExplicitFileRead> ->
        ledger: JsAsIsLedger ->
        callHost: JsCallHost option ->
        delegationHost: JsDelegationHost option ->
        askInvoke: (string -> Task<Result<unit, string>>) option ->
            obj

    /// Build the api object for one sandbox run that carries a park lifecycle
    /// state. `parkState` is the mutable object shared with the sandbox wrapper;
    /// the generated `this.park` member suspends through it (PLAN S2 step 4).
    val createApiWithPark:
        capabilities: Set<JsCapability> ->
        root: string ->
        staging: ResizeArray<JsStagedMutation> ->
        readSnapshots: ResizeArray<JsReadSnapshot> ->
        explicitReads: ResizeArray<JsExplicitFileRead> ->
        ledger: JsAsIsLedger ->
        parkState: Wanxiangshu.Process.JsSandbox.ParkState ->
        callHost: JsCallHost option ->
        delegationHost: JsDelegationHost option ->
        askInvoke: (string -> Task<Result<unit, string>>) option ->
            obj

module JsReviewOnly =
    val scopeReviewOnly: check: (unit -> Task<bool>) -> api: obj -> obj
