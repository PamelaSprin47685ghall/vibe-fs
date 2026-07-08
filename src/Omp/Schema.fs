module Wanxiangshu.Omp.Schema

open Fable.Core.JsInterop

let str (description: string) (typebox: obj) =
    typebox?Type?String (createObj [ "description", box description ])

let num (description: string) (typebox: obj) =
    typebox?Type?Number (createObj [ "description", box description ])

let bool_ (description: string) (typebox: obj) =
    typebox?Type?Boolean (createObj [ "description", box description ])

let strArray (description: string) (typebox: obj) =
    typebox?Type?Array (str description typebox)

let optional (schema: obj) (typebox: obj) = typebox?Type?Optional (schema)

let opt (desc: string) (typebox: obj) (build: string -> obj -> obj) = optional (build desc typebox) typebox

let optWithDefault (desc: string) (typebox: obj) (defaultValue: string) (build: string -> obj -> obj) : obj =
    let inner = build desc typebox
    inner?("default") <- box defaultValue
    optional inner typebox

let union (schemas: obj array) (typebox: obj) = typebox?Type?Union (schemas)

let null_ (description: string) (typebox: obj) =
    typebox?Type?Null (createObj [ "description", box description ])

let enumOf (values: string array) (description: string) (typebox: obj) =
    typebox?Type?Enum (values, createObj [ "description", box description ])

let objectOf (fields: (string * obj) array) (typebox: obj) =
    typebox?Type?Object (createObj (fields |> Array.map (fun (k, v) -> k, v)))

let arrayOf (item: obj) (description: string) (typebox: obj) =
    typebox?Type?Array (item, createObj [ "description", box description ])

let arrayMin (item: obj) (minCount: int) (description: string) (typebox: obj) =
    typebox?Type?Array (item, createObj [ "minItems", box minCount; "description", box description ])
