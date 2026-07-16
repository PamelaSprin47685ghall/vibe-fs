module Wanxiangshu.Tests.JsonSchemaBuildersTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.JsonSchemaBuilders
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn

let jsonStrPropTypeAndDescription () =
    let s = jsonStrProp "a string"
    equal "type is string" "string" (string (s?("type")))
    check "description present" (has s "description")

let jsonStrReqFields () =
    let s = jsonStrReq "required string"
    equal "type is string" "string" (string (s?("type")))
    equal "minLength is 1" 1 (unbox<int> (s?("minLength")))
    check "description present" (has s "description")

let jsonNumPropType () =
    let s = jsonNumProp "a number"
    equal "type is number" "number" (string (s?("type")))

let jsonBoolPropType () =
    let s = jsonBoolProp "a bool"
    equal "type is boolean" "boolean" (string (s?("type")))

let jsonStrEnumPropFields () =
    let vals = [| "a"; "b"; "c" |]
    let s = jsonStrEnumProp "pick one" vals
    equal "type is string" "string" (string (s?("type")))
    let ev = unbox<obj[]> (s?("enum")) |> Array.map unbox<string>
    equal "enum matches" vals ev
    check "description present" (has s "description")

let jsonStrArrayPropShape () =
    let s = jsonStrArrayProp "strings"
    equal "type is array" "array" (string (s?("type")))
    let items = s?("items")
    equal "items.type is string" "string" (string (items?("type")))

let jsonStrArrayReqShape () =
    let s = jsonStrArrayReq "required strings"
    equal "type is array" "array" (string (s?("type")))
    equal "minItems is 1" 1 (unbox<int> (s?("minItems")))
    let items = s?("items")
    equal "items.type is string" "string" (string (items?("type")))
    equal "items.minLength is 1" 1 (unbox<int> (items?("minLength")))

let jsonStrArrayOptNoMinItems () =
    let s = jsonStrArrayOpt "optional strings"
    equal "type is array" "array" (string (s?("type")))
    check "no minItems key" (not (has s "minItems"))
    let items = s?("items")
    equal "items.type is string" "string" (string (items?("type")))
    equal "items.minLength is 1" 1 (unbox<int> (items?("minLength")))

let jsonObjectSchemaShape () =
    let props = createObj [ "name", box (jsonStrProp "name") ]
    let req = [| "name" |]
    let s = jsonObjectSchema props req
    equal "type is object" "object" (string (s?("type")))
    let required = unbox<obj[]> (s?("required")) |> Array.map unbox<string>
    equal "required matches" req required
    equal "additionalProperties is false" false (unbox<bool> (s?("additionalProperties")))
    check "properties present" (has s "properties")

let run () =
    jsonStrPropTypeAndDescription ()
    jsonStrReqFields ()
    jsonNumPropType ()
    jsonBoolPropType ()
    jsonStrEnumPropFields ()
    jsonStrArrayPropShape ()
    jsonStrArrayReqShape ()
    jsonStrArrayOptNoMinItems ()
    jsonObjectSchemaShape ()
