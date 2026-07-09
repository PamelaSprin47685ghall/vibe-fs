module Wanxiangshu.Kernel.FallbackKernel.Decision

open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.FallbackKernel.Types

let errorInputIsAbort (err: ErrorInput) : bool =
    match err.DomainError with
    | Some MessageAborted -> true
    | Some(ClientCancellation _) -> true
    | _ when err.ErrorName = "AbortError" || err.ErrorName = "MessageAbortedError" -> true
    | _ -> false

let private isImmediateStatusCode (sc: int option) : bool =
    match sc with
    | Some 401
    | Some 402
    | Some 403 -> true
    | _ -> false

let private isRetryableStatusCode (sc: int option) : bool =
    match sc with
    | Some 429
    | Some 500
    | Some 502
    | Some 503
    | Some 504 -> true
    | _ -> false

/// Classify an error into the action class the state machine should take.
///
/// Priority (highest → lowest):
///   1. Cancelled / TaskComplete → Ignore
///   2. Abort error name           → Ignore
///   3. Auth / quota status code   → ImmediateFallback
///   4. Explicit non-retryable flag → ImmediateFallback
///   5. Retries exhausted          → Exhausted
///   6. isRetryable=true / retryable status code → RetrySame
///   7. Everything else (safety net) → RetrySame
let classifyError (err: ErrorInput) (state: SessionFallbackState) (cfg: FallbackConfig) : ErrorClass =
    // retryCount comes from Phase, not FailureCount — FailureCount tracks heuristic k,
    // while Phase.Retrying count tracks actual in-flight retries for MaxRetries comparison.
    let retryCount =
        match state.Phase with
        | FallbackPhase.Retrying count -> count
        | _ -> 0

    if state.Cancelled || state.TaskComplete then
        ErrorClass.Ignore
    elif errorInputIsAbort err then
        ErrorClass.Ignore
    elif isImmediateStatusCode err.StatusCode then
        ErrorClass.ImmediateFallback
    elif err.IsRetryable = Some false then
        ErrorClass.ImmediateFallback
    elif retryCount >= cfg.MaxRetries then
        ErrorClass.Exhausted
    elif err.IsRetryable = Some true || isRetryableStatusCode err.StatusCode then
        ErrorClass.RetrySame
    else
        ErrorClass.RetrySame // default safety net
