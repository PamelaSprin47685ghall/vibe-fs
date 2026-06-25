module VibeFs.Shell.SessionExecutor

open Fable.Core
open VibeFs.Shell.RuntimeScope

/// Per-session serial executor bound to a registration [RuntimeScope].
/// Production and tests must use [createForScope] with an explicit scope (e.g. [RuntimeScope.create]).
type SessionExecutor(scope: RuntimeScope) =
    member _.EnqueuePerSession(sessionId: string, work: unit -> JS.Promise<'T>) : JS.Promise<'T> =
        scope.EnqueuePerSession(sessionId, work)

let createForScope (scope: RuntimeScope) : SessionExecutor = SessionExecutor(scope)