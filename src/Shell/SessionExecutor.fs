module VibeFs.Shell.SessionExecutor

open Fable.Core
open VibeFs.Shell.RuntimeScope

/// Per-session serial executor bound to a registration [RuntimeScope].
/// Production paths (Opencode/Mux executor tools) must use [createForScope] with the plugin scope, not [enqueuePerSession].
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)

/// Test and legacy fallback only; delegates to process [getDefault] scope, not per-registration scope.
let enqueuePerSession (sessionId: string) (work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
    getDefault().EnqueuePerSession(sessionId, work)