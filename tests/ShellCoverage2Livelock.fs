module Wanxiangshu.Tests.ShellCoverage2Livelock

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.RuntimeScope

module Livelock = Wanxiangshu.Runtime.LivelockGuard

// ── LivelockGuard ─────────────────────────────────────────────────────────────
let livelockGuardFirstCall () =
    let testScope = RuntimeScope()
    check "first call not blocked" (not (Livelock.check testScope "s1" "c" "a" "o"))

let livelockGuardSameIncrement () =
    let testScope = RuntimeScope()
    check "same tool" (not (Livelock.check testScope "s9" "c" "a" "o"))
    check "repeat counts" (not (Livelock.check testScope "s9" "c" "a" "o"))

let livelockGuardBreach () =
    let testScope = RuntimeScope()
    check "1st" (not (Livelock.check testScope "s2" "c" "a" "o"))
    check "2nd" (not (Livelock.check testScope "s2" "c" "a" "o"))
    check "3rd breach" (Livelock.check testScope "s2" "c" "a" "o")

let livelockGuardDifferentResets () =
    let testScope = RuntimeScope()
    check "s3 baseline" (not (Livelock.check testScope "s3" "c" "a" "o"))
    check "s3 repeat" (not (Livelock.check testScope "s3" "c" "a" "o"))
    check "s3 different output breaks" (not (Livelock.check testScope "s3" "c" "a" "x"))

let run () =
    livelockGuardFirstCall ()
    livelockGuardSameIncrement ()
    livelockGuardBreach ()
    livelockGuardDifferentResets ()
