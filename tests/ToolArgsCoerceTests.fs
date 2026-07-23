module Wanxiangshu.Tests.ToolArgsCoerceTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.ToolArgs
open Wanxiangshu.Runtime.ToolArgsDecode
open Wanxiangshu.Runtime.ToolHookRuntime

module Dyn = Wanxiangshu.Runtime.Dyn

let testCoerceUnknownToolArgsOk () =
    registerToolParameterTypes
        [ ("my_custom_tool", "count", SchemaType.SNumber)
          ("my_custom_tool", "verbose", SchemaType.SBoolean)
          ("my_custom_tool", "items", SchemaType.SArray)
          ("my_custom_tool", "config", SchemaType.SObject)
          ("my_custom_tool", "name", SchemaType.SString) ]

    let args =
        createObj
            [ "count", box "42"
              "verbose", box "true"
              "items", box """["a","b"]"""
              "config", box """{"key":"val"}"""
              "name", box "hello" ]

    coerceArgsTypes "my_custom_tool" args

    let count = Dyn.get args "count"
    check "custom number coerced" (not (Dyn.typeIs count "string"))
    check "custom number value" (unbox<int> count = 42)

    let verbose = Dyn.get args "verbose"
    check "custom boolean coerced" (not (Dyn.typeIs verbose "string"))
    check "custom boolean value" (unbox<bool> verbose = true)

    let items = Dyn.get args "items"
    check "custom array coerced" (not (Dyn.typeIs items "string"))
    check "custom array is array" (Dyn.isArray items)

    let config = Dyn.get args "config"
    check "custom object coerced" (not (Dyn.typeIs config "string"))
    check "custom object is object" (Dyn.typeIs config "object")

    let name = Dyn.get args "name"
    check "string field not coerced" (Dyn.typeIs name "string")
    check "string field preserved" (unbox<string> name = "hello")

let testCoerceQuotedStringArgs () =
    registerToolParameterTypes
        [ ("my_custom_tool", "quoted_string", SchemaType.SString)
          ("my_custom_tool", "quoted_object", SchemaType.SObject)
          ("my_custom_tool", "quoted_array", SchemaType.SArray)
          ("my_custom_tool", "quoted_number", SchemaType.SNumber) ]

    let args =
        createObj
            [ "quoted_string", box "\"hello\""
              "quoted_object", box "\"{\\\"a\\\":1}\""
              "quoted_array", box "\"[1,2]\""
              "quoted_number", box "\"42\"" ]

    coerceArgsTypes "my_custom_tool" args

    let nv = Dyn.get args "quoted_string"
    check "quoted_string preserved" (unbox<string> nv = "\"hello\"")
    let qo = Dyn.get args "quoted_object"
    check "quoted_object preserved" (unbox<string> qo = "\"{\\\"a\\\":1}\"")
    let qa = Dyn.get args "quoted_array"
    check "quoted_array preserved" (unbox<string> qa = "\"[1,2]\"")
    let qn = Dyn.get args "quoted_number"
    check "quoted_number preserved" (unbox<string> qn = "\"42\"")

let testCoerceFloatAndExpNumber () =
    registerToolParameterTypes
        [ ("my_custom_tool", "float_val", SchemaType.SNumber)
          ("my_custom_tool", "exp_val", SchemaType.SNumber) ]

    let args = createObj [ "float_val", box "3.5"; "exp_val", box "1e2" ]

    coerceArgsTypes "my_custom_tool" args

    let fv = Dyn.get args "float_val"
    let ev = Dyn.get args "exp_val"
    check "float_val is number" (not (Dyn.typeIs fv "string"))
    equal "float_val value" 3.5 (unbox<float> fv)
    check "exp_val is number" (not (Dyn.typeIs ev "string"))
    equal "exp_val value" 100.0 (unbox<float> ev)

let testTrimWhitespaceInNumberAndJson () =
    registerToolParameterTypes
        [ ("my_custom_tool", "num_val", SchemaType.SNumber)
          ("my_custom_tool", "json_val", SchemaType.SObject) ]

    let args = createObj [ "num_val", box "  42  "; "json_val", box "  {\"x\":1}  " ]

    coerceArgsTypes "my_custom_tool" args

    let nv = Dyn.get args "num_val"
    let jv = Dyn.get args "json_val"
    check "num_val is number" (not (Dyn.typeIs nv "string"))
    equal "num_val value" 42 (unbox<int> nv)
    check "json_val is object" (Dyn.typeIs jv "object")

let testCoerceArgsTypesOk () =
    let readArgs =
        createObj [ "path", box "a.txt"; "offset", box "123.0"; "limit", box "456.5" ]

    coerceArgsTypes "read" readArgs

    let intentsJson =
        """[{"objective":"fix bug","background":"test","targets":[{"file":"a.ts","guide":"fix it"}]}]"""

    let coderArgs =
        createObj
            [ "intents", box intentsJson
              "tdd", box "red"
              "follow-tdd-and-kolmogorov-principles", box "1.0" ]

    coerceArgsTypes "coder" coderArgs
    check "coder follow-tdd coerced to number" (Dyn.get coderArgs "follow-tdd-and-kolmogorov-principles" = box 1.0)

    match decodeToolInvocation "read" readArgs with
    | Ok(Typed(ToolArgs.Read r)) ->
        check "read offset coerced" (r.Offset = Some 123)
        check "read limit coerced" (r.Limit = Some 456)
    | _ -> check "read coerced failed" false

    match decodeToolInvocation "coder" coderArgs with
    | Ok(CoderBatch [ intent ]) ->
        check "coder intents coerced objective" (intent.objective = "fix bug")
        check "coder intents coerced file" (intent.targets.Head.file = "a.ts")
    | _ -> check "coder coerced failed" false

let run () =
    testCoerceArgsTypesOk ()
    testCoerceUnknownToolArgsOk ()
    testCoerceQuotedStringArgs ()
    testCoerceFloatAndExpNumber ()
    testTrimWhitespaceInNumberAndJson ()
