module Wanxiangshu.Tests.OmpSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Shell.RuntimeScope
open Wanxiangshu.Omp.SessionLifecycle
open Wanxiangshu.Omp.SessionLifecycleHooks

let private testScope = RuntimeScope()

/// The child-session registry is the seam between `createChildSession` and
/// the `tool_result` filter. Marking and unmarking must be exact, with empty
/// ids treated as no-ops so a misconfigured host cannot poison the set.
let isChildSessionGuard () =
    clearChildSessionsForTest testScope ()
    let parentId = "parent-session-1"
    let childId = "child-coder-session"
    check "parent initially not a child" (not (isChildSession testScope parentId))
    check "child initially not a child" (not (isChildSession testScope childId))
    markChildSession testScope childId
    check "child now registered" (isChildSession testScope childId)
    check "parent still not a child" (not (isChildSession testScope parentId))
    check "empty id never registers" (not (isChildSession testScope ""))
    unmarkChildSession testScope childId
    check "child cleared after unmark" (not (isChildSession testScope childId))
    markChildSession testScope ""
    check "marking empty id does not register random id" (not (isChildSession testScope "random-after-empty-mark"))
    markChildSession testScope "probe"
    check "non-empty mark still works after empty mark" (isChildSession testScope "probe")
    unmarkChildSession testScope "probe"
