module Wanxiangshu.Tests.DynFieldTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.DynField

let hasFieldTrue () =
    let obj = unbox (createObj [ "key", box 42 ])
    check "true" (hasField obj "key")

let hasFieldFalse () =
    let obj = unbox (createObj [])
    check "false" (not (hasField obj "key"))

let strFieldPresent () =
    let obj = unbox (createObj [ "name", box "hello" ])
    equal "hello" (Some "hello") (strField obj "name")

let strFieldMissing () =
    let obj = unbox (createObj [])
    equal "none" None (strField obj "name")

let optIntPresent () =
    let obj = unbox (createObj [ "count", box 5 ])
    equal "5" (Some 5) (optInt obj "count")

let optIntMissing () =
    let obj = unbox (createObj [])
    equal "none" None (optInt obj "count")

let optBoolTrue () =
    let obj = unbox (createObj [ "flag", box true ])
    equal "true" (Some true) (optBool obj "flag")

let optBoolMissing () =
    let obj = unbox (createObj [])
    equal "none" None (optBool obj "flag")

let requiredStrFieldPresent () =
    let obj = unbox (createObj [ "key", box "value" ])
    equal "value" "value" (requiredStrField obj "key")

let requiredStrFieldMissing () =
    let obj = unbox (createObj [])
    equal "empty" "" (requiredStrField obj "key")

let strListFieldPresent () =
    let obj = unbox (createObj [ "items", box [| "a"; "b" |] ])

    match strListField obj "items" with
    | Some list -> equal "ab" 2 (list.Length)
    | None -> failwith "expected Some"

let strListFieldMissing () =
    let obj = unbox (createObj [])
    equal "none" None (strListField obj "items")

let objListFieldPresent () =
    let obj = unbox (createObj [ "items", box [| box 1; box 2; box 3 |] ])

    match objListField obj "items" with
    | Some list -> equal "abc" 3 (list.Length)
    | None -> failwith "expected Some"

let objListFieldNotArray () =
    let obj = unbox (createObj [ "items", box "not-an-array" ])
    equal "none" None (objListField obj "items")

let objListFieldMissing () =
    let obj = unbox (createObj [])
    equal "none" None (objListField obj "items")

let run () =
    hasFieldTrue ()
    hasFieldFalse ()
    strFieldPresent ()
    strFieldMissing ()
    optIntPresent ()
    optIntMissing ()
    optBoolTrue ()
    optBoolMissing ()
    requiredStrFieldPresent ()
    requiredStrFieldMissing ()
    strListFieldPresent ()
    strListFieldMissing ()
    objListFieldPresent ()
    objListFieldNotArray ()
    objListFieldMissing ()
