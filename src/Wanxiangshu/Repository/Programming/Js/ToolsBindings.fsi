namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations write as-is: each edit/write is atomically persisted
/// through MutationFs at call time; the staging buffer records what was
/// actually written (JS-012).
module JsToolsBindings =
    /// The host loop behind `js.run`, injected by the workflow.
    type JsRunHost =
        { Loop: Wanxiangshu.Process.JsSandbox.ParkState option -> obj -> string -> Task<obj> }

    val createLedger: persistence: IJsTransactionPersistence option -> JsAsIsLedger

    val createApi:
        capabilities: Set<JsCapability> ->
        root: string ->
        staging: ResizeArray<JsStagedMutation> ->
        readSnapshots: ResizeArray<JsReadSnapshot> ->
        explicitReads: ResizeArray<JsExplicitFileRead> ->
        ledger: JsAsIsLedger ->
        runHost: JsRunHost option ->
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
        runHost: JsRunHost option ->
            obj
