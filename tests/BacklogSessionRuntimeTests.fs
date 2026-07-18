module Wanxiangshu.Tests.BacklogSessionRuntimeTests

open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Tests.Assert

let run () : unit =
    let firstScope = RuntimeScope()
    let secondScope = RuntimeScope()
    let first = BacklogSession(omp, firstScope)
    let sameScope = BacklogSession(omp, firstScope)
    let otherScope = BacklogSession(omp, secondScope)

    first.CaptureReport("scope-call", "scope report")
    check "same scope shares report projection" (sameScope.TakeReport("scope-call") = "scope report")
    check "different scope does not share report projection" (otherScope.TakeReport("scope-call") = "")
