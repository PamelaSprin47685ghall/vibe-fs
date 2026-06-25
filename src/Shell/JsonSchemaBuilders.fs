module VibeFs.Shell.JsonSchemaBuilders

open Fable.Core.JsInterop

let jsonStrProp (desc: string) : obj =
    createObj [ "type", box "string"; "description", box desc ]

let jsonStrReq (desc: string) : obj =
    createObj [ "type", box "string"; "minLength", box 1; "description", box desc ]

let jsonNumProp (desc: string) : obj =
    createObj [ "type", box "number"; "description", box desc ]

let jsonBoolProp (desc: string) : obj =
    createObj [ "type", box "boolean"; "description", box desc ]

let jsonStrEnumProp (desc: string) (values: string array) : obj =
    createObj [ "type", box "string"; "enum", box values; "description", box desc ]

let private jsonStrItem : obj = createObj [ "type", box "string" ]

let private jsonStrItemReq : obj = createObj [ "type", box "string"; "minLength", box 1 ]

let jsonStrArrayProp (desc: string) : obj =
    createObj [ "type", box "array"; "items", box jsonStrItem; "description", box desc ]

let jsonStrArrayReq (desc: string) : obj =
    createObj [ "type", box "array"; "minItems", box 1; "items", box jsonStrItemReq; "description", box desc ]

let jsonStrArrayOpt (desc: string) : obj =
    createObj [ "type", box "array"; "items", box jsonStrItemReq; "description", box desc ]

let jsonObjectSchema (properties: obj) (required: string array) : obj =
    createObj [ "type", box "object"; "properties", properties; "required", box required; "additionalProperties", box false ]