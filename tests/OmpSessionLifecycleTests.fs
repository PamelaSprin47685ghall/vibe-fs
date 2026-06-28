module Wanxiangshu.Tests.OmpSessionLifecycleTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Omp.ChildSession
open Wanxiangshu.Omp.SessionLifecycle
open Wanxiangshu.Omp.SessionLifecycleHooks


/// The child-session registry is the seam between `createChildSession` and
/// the `tool_result` filter. Marking and unmarking must be exact, with empty
/// ids treated as no-ops so a misconfigured host cannot poison the set.
let isChildSessionGuard () =
    clearChildSessionsForTest ()
    let parentId = "parent-session-1"
    let childId = "child-coder-session"
    check "parent initially not a child" (not (isChildSession parentId))
    check "child initially not a child" (not (isChildSession childId))
    markChildSession childId
    check "child now registered" (isChildSession childId)
    check "parent still not a child" (not (isChildSession parentId))
    check "empty id never registers" (not (isChildSession ""))
    unmarkChildSession childId
    check "child cleared after unmark" (not (isChildSession childId))
    markChildSession ""
    check "marking empty id does not register random id" (not (isChildSession "random-after-empty-mark"))
    markChildSession "probe"
    check "non-empty mark still works after empty mark" (isChildSession "probe")
    unmarkChildSession "probe"
