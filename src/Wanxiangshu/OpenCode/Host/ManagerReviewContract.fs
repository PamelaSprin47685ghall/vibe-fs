namespace Wanxiangshu.OpenCode.Host

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode

module ManagerReviewContract =

    let private savedContractKey: obj = emitJsExpr () "Symbol('manager-review-contract')"

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isPlainObject (value: obj) : bool = jsNative

    [<Emit("Object.prototype.hasOwnProperty.call($0, $1)")>]
    let private hasOwn (target: obj) (key: obj) : bool = jsNative

    [<Emit("Object.isExtensible($0)")>]
    let private isExtensible (target: obj) : bool = jsNative

    [<Emit("Object.getOwnPropertyDescriptor($0, $1)")>]
    let private getOwnPropertyDescriptor (target: obj) (key: obj) : obj = jsNative

    [<Emit("Object.defineProperty($0, $1, $2)")>]
    let private defineProperty (target: obj) (key: obj) (descriptor: obj) : unit = jsNative

    [<Emit("Reflect.deleteProperty($0, $1)")>]
    let private deleteProperty (target: obj) (key: obj) : bool = jsNative

    [<Emit("throw new TypeError($0)")>]
    let private throwTypeError (message: string) : unit = jsNative

    let hide (args: obj) : unit =
        if isNull args || not (isPlainObject args) then
            throwTypeError "Tool arguments must be an object"

        if not (hasOwn args savedContractKey) then
            let descriptor = getOwnPropertyDescriptor args "contract"

            let isConfigurable =
                if isNull descriptor then
                    true
                else
                    let conf = descriptor?configurable
                    not (isNull conf) && unbox<bool> conf

            if not (isExtensible args) || not isConfigurable then
                throwTypeError "Tool arguments cannot hold or modify the review contract"

            let saved = createObj [ "descriptor", descriptor ]
            let symbolDescriptor =
                createObj [ "value", saved; "enumerable", box false; "configurable", box true ]

            defineProperty args savedContractKey symbolDescriptor

            try
                if not (isNull descriptor) then
                    let deleted = deleteProperty args "contract"
                    if not deleted then
                        throwTypeError "Review contract could not be hidden"
            with ex ->
                deleteProperty args savedContractKey |> ignore
                raise ex

    let restore (args: obj) : unit =
        if not (isNull args) && isPlainObject args && hasOwn args savedContractKey then
            let saved = args?(savedContractKey)
            if not (isExtensible args) then
                throwTypeError "Tool arguments are frozen or not extensible during contract restore"
            let deletedKey = deleteProperty args savedContractKey
            if not deletedKey then
                throwTypeError "Failed to delete saved review contract key"

            if not (isNull saved) then
                let descriptor = saved?descriptor
                if isNull descriptor then
                    let deleted = deleteProperty args "contract"
                    if not deleted then
                        throwTypeError "Failed to delete contract property during restore"
                else
                    defineProperty args "contract" descriptor

    [<Literal>]
    let private reviewContractDescription =
        "仅用于当前尚未被接纳的独立评审。评审一旦被系统接纳，不得再次调用本工具。固定填写 do-not-use-except-for-review。"

    let private decorateSchemaObject (schemaObj: obj) (toolId: string) : unit =
        let properties = schemaObj?properties
        if isNull properties || not (isPlainObject properties) then
            raise (InvalidOperationException(sprintf "Tool %s parameters schema missing object properties" toolId))

        if isNull properties?contract then
            let contractProperty =
                createObj
                    [ "type", box "string"
                      "enum", box [| ManagerReviewTools.contractValue |]
                      "description", box reviewContractDescription ]
            properties?contract <- contractProperty

        let required = schemaObj?required
        if isNull required then
            schemaObj?required <- box [| "contract" |]
        elif isArray required then
            let reqArr = unbox<obj array> required
            let exists = reqArr |> Array.exists (fun x -> string x = "contract")
            if not exists then
                schemaObj?required <- box (Array.append reqArr [| box "contract" |])
        else
            raise (InvalidOperationException(sprintf "Tool %s parameters schema required field is not an array" toolId))

    let decorateDefinition (toolInput: obj) (toolOutput: obj) : unit =
        if not (isNull toolInput) && not (isNull toolOutput) then
            let toolId = if isNull toolInput?toolID then "" else string toolInput?toolID
            if ManagerReviewTools.isReviewTool toolId then
                let hasJsonSchema =
                    not (isNull toolOutput?jsonSchema) && isPlainObject toolOutput?jsonSchema
                let hasParameters =
                    not (isNull toolOutput?parameters) && isPlainObject toolOutput?parameters

                if not hasJsonSchema && not hasParameters then
                    raise (InvalidOperationException(sprintf "Tool %s parameters schema is not a valid object schema" toolId))

                if hasJsonSchema then
                    decorateSchemaObject toolOutput?jsonSchema toolId

                if hasParameters then
                    let parameters = toolOutput?parameters
                    let parametersProperties = parameters?properties
                    if not (isNull parametersProperties) && isPlainObject parametersProperties then
                        decorateSchemaObject parameters toolId
                    elif hasJsonSchema then
                        parameters?properties <- toolOutput?jsonSchema?properties
                        parameters?required <- toolOutput?jsonSchema?required
                    else
                        raise (InvalidOperationException(sprintf "Tool %s parameters schema missing object properties" toolId))
                else
                    toolOutput?parameters <- toolOutput?jsonSchema
