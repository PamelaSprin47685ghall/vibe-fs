module Wanxiangshu.Tests.Wanxiangzhen.AssertCompat

module A = Wanxiangshu.Tests.Assert

let check = A.check
let chk = A.chk
let checkBare = A.checkBare
let isSome = A.isSome
let isNone = A.isNone
let recordException = A.recordException
let timed = A.timed
let timedAsync = A.timedAsync
let timedAsyncSuite = A.timedAsyncSuite

let equal (expected: 'a) (actual: 'a) : unit = A.equal "equal" expected actual

let equalLabeled (label: string) (expected: 'a) (actual: 'a) : unit = A.equal label expected actual
