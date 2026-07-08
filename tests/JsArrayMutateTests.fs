module Wanxiangshu.Tests.JsArrayMutateTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.JsArrayMutate

let replaceDifferentArray () =
    let target: obj array = [| box 1; box 2; box 3 |]
    let source: obj array = [| box 4; box 5 |]
    replaceArrayInPlace target source
    equal "length after replace" 2 target.Length
    equal "elem 0" (box 4) target.[0]
    equal "elem 1" (box 5) target.[1]

let replaceSameReference () =
    let arr: obj array = [| box 1; box 2 |]
    // should not throw; array unchanged
    replaceArrayInPlace arr arr
    equal "same ref length" 2 arr.Length
    equal "same ref elem 0" (box 1) arr.[0]
    equal "same ref elem 1" (box 2) arr.[1]

let replaceEmptyArray () =
    let target: obj array = [| box 1; box 2; box 3 |]
    let source: obj array = [||]
    replaceArrayInPlace target source
    equal "empty replace length" 0 target.Length

let targetIdentityPreserved () =
    let target: obj array = [| box 1 |]
    let source: obj array = [| box 9 |]
    let before = System.Object.ReferenceEquals(target, target)
    replaceArrayInPlace target source
    let after = System.Object.ReferenceEquals(target, target)
    check "same array reference before" before
    check "same array reference after" after

let run () : unit =
    replaceDifferentArray ()
    replaceSameReference ()
    replaceEmptyArray ()
    targetIdentityPreserved ()
