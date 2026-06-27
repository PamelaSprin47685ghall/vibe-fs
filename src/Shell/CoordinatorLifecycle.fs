module Wanxiangshu.Shell.CoordinatorLifecycle

open Fable.Core
open Wanxiangshu.Shell.PromiseQueue

// ── types ────────────────────────────────────────────────────────────────────

/// Result of coordinator runtime creation — the listen port and bearer token.
type CreateResult =
    { port     : int
      token    : string }

// ── CoordinatorRuntime ────────────────────────────────────────────────────────
// InjectError is set to None here — injection errors are transient and handled
// by the retry loop in the inject queue; they are not a startup-fatal condition.

type CoordinatorRuntime =
    { gitQueue      : SerialQueue
      injectQueue   : SerialQueue
      InjectError   : exn option }   // ← required field, initialised to None

let create
    (gitQueue    : SerialQueue)
    (injectQueue : SerialQueue) : CoordinatorRuntime =

    { gitQueue    = gitQueue
      injectQueue = injectQueue
      InjectError = None }   // no injection error at creation time
