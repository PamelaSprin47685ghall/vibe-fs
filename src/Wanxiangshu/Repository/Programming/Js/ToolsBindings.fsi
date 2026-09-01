namespace Wanxiangshu.Repository.Programming.Js

/// JS-010/JS-016: the api object injected into the sandbox — the only
/// authority a model program sees. Every member returns a JSON-compatible
/// object; failures carry `{ ok: false, code, reason }` with stable codes
/// (JS-019). Mutations only stage (JS-012); the transaction engine commits.
module JsToolsBindings =
    val createApi:
        root: string -> staging: ResizeArray<JsStagedMutation> -> readSnapshots: ResizeArray<JsReadSnapshot> -> obj
