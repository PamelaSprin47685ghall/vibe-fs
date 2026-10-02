namespace Wanxiangshu.OpenCode.Host

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

type ProtocolArgumentCall =
    { SessionId: SessionId
      ToolCallId: ToolCallId
      Tool: string }

[<RequireQualifiedAccess>]
type HiddenProtocolArguments =
    | NotHidden
    | SameCall
    | DifferentCallOrChangedArguments

module ManagerReviewContract =

    let private savedContractKey: obj =
        emitJsExpr () "Symbol('manager-review-contract')"

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

    [<Emit("Object.getOwnPropertyNames($0)")>]
    let private ownPropertyNames (target: obj) : string array = jsNative

    [<Emit("Object.defineProperty($0, $1, $2)")>]
    let private defineProperty (target: obj) (key: obj) (descriptor: obj) : unit = jsNative

    [<Emit("Reflect.deleteProperty($0, $1)")>]
    let private deleteProperty (target: obj) (key: obj) : bool = jsNative

    [<Emit("throw new TypeError($0)")>]
    let private throwTypeError (message: string) : unit = jsNative

    let private isDescriptorConfigurable (descriptor: obj) : bool =
        if isNull descriptor then
            true
        else
            let conf = descriptor?configurable
            not (isNull conf) && unbox<bool> conf

    let private assertPropertyDeleted (deleted: bool) (message: string) : unit =
        if not deleted then
            throwTypeError message

    let private removeContractIfPresent (args: obj) (descriptor: obj) : unit =
        if not (isNull descriptor) then
            let deleted = deleteProperty args "contract"
            assertPropertyDeleted deleted "Review contract could not be hidden"

    let private deleteExistingContract (args: obj) (descriptor: obj) : unit =
        try
            removeContractIfPresent args descriptor
        with ex ->
            deleteProperty args savedContractKey |> ignore
            raise ex

    let private requireConfigurableProperty (args: obj) (key: string) : unit =
        if not (isDescriptorConfigurable (getOwnPropertyDescriptor args key)) then
            throwTypeError "Tool arguments cannot restore the review contract's original key order"

    let private requireConfigurableProperties (args: obj) (keys: string array) : unit =
        for key in keys do
            requireConfigurableProperty args key

    let private hideContractProperty (args: obj) : unit =
        let descriptor = getOwnPropertyDescriptor args "contract"
        let isConfigurable = isDescriptorConfigurable descriptor

        if not (isExtensible args) || not isConfigurable then
            throwTypeError "Tool arguments cannot hold or modify the review contract"

        let followingKeys =
            if isNull descriptor then
                [||]
            else
                ownPropertyNames args |> Array.skipWhile ((<>) "contract") |> Array.skip 1

        requireConfigurableProperties args followingKeys

        let saved =
            createObj [ "descriptor", descriptor; "followingKeys", box followingKeys ]

        let symbolDescriptor =
            createObj [ "value", saved; "enumerable", box false; "configurable", box true ]

        defineProperty args savedContractKey symbolDescriptor
        deleteExistingContract args descriptor

    let hide (args: obj) : unit =
        if isNull args || not (isPlainObject args) then
            throwTypeError "Tool arguments must be an object"

        if not (hasOwn args savedContractKey) then
            hideContractProperty args

    let private hasSavedContract (args: obj) =
        not (isNull args) && isPlainObject args && hasOwn args savedContractKey

    let private hasExactCallIdentity (owner: ProtocolArgumentCall) =
        not (String.IsNullOrWhiteSpace(SessionId.value owner.SessionId))
        && not (String.IsNullOrWhiteSpace(ToolCallId.value owner.ToolCallId))
        && not (String.IsNullOrWhiteSpace owner.Tool)

    let private bindCallOwner (owner: ProtocolArgumentCall option) (args: obj) =
        match owner with
        | Some call when hasExactCallIdentity call ->
            let saved = args?(savedContractKey)
            saved?owner <- box call
        | _ -> ()

    let hideForCall (owner: ProtocolArgumentCall option) (args: obj) : unit =
        let alreadyHidden = hasSavedContract args
        hide args

        if not alreadyHidden then
            bindCallOwner owner args

    let private classifySavedCall (owner: ProtocolArgumentCall option) (saved: obj) =
        match owner with
        | Some call when
            hasExactCallIdentity call
            && hasOwn saved "owner"
            && call = unbox<ProtocolArgumentCall> saved?owner
            ->
            HiddenProtocolArguments.SameCall
        | _ -> HiddenProtocolArguments.DifferentCallOrChangedArguments

    let classifyHiddenArguments (owner: ProtocolArgumentCall option) (args: obj) : HiddenProtocolArguments =
        if not (hasSavedContract args) then
            HiddenProtocolArguments.NotHidden
        elif hasOwn args "contract" then
            HiddenProtocolArguments.DifferentCallOrChangedArguments
        else
            classifySavedCall owner args?(savedContractKey)

    let private applyRestoredDescriptor (args: obj) (descriptor: obj) : unit =
        let deleted = deleteProperty args "contract"
        assertPropertyDeleted deleted "Failed to delete contract property during restore"

        if not (isNull descriptor) then
            defineProperty args "contract" descriptor

    let private restoreContractInOriginalOrder (args: obj) (saved: obj) : unit =
        let followingKeys: string array = saved?followingKeys
        requireConfigurableProperty args "contract"
        requireConfigurableProperties args followingKeys

        let following =
            followingKeys
            |> Array.map (fun key -> key, getOwnPropertyDescriptor args key)
            |> Array.filter (fun (_, descriptor) -> not (isNull descriptor))

        for key, _ in following do
            assertPropertyDeleted (deleteProperty args key) "Failed to restore review argument key order"

        applyRestoredDescriptor args saved?descriptor

        for key, descriptor in following do
            defineProperty args key descriptor

    let private restoreSavedDescriptor (args: obj) (saved: obj) : unit =
        if not (isNull saved) then
            restoreContractInOriginalOrder args saved

    let private restoreSavedContract (args: obj) : unit =
        let saved = args?(savedContractKey)

        if not (isExtensible args) then
            throwTypeError "Tool arguments are frozen or not extensible during contract restore"

        restoreSavedDescriptor args saved
        let deletedKey = deleteProperty args savedContractKey
        assertPropertyDeleted deletedKey "Failed to delete saved review contract key"

    let restore (args: obj) : unit =
        if not (isNull args) && isPlainObject args && hasOwn args savedContractKey then
            restoreSavedContract args

    let private ownsSavedCall (owner: ProtocolArgumentCall option) (saved: obj) =
        (owner.IsNone && not (hasOwn saved "owner"))
        || classifySavedCall owner saved = HiddenProtocolArguments.SameCall

    let restoreForCall (owner: ProtocolArgumentCall option) (args: obj) : unit =
        if hasSavedContract args && ownsSavedCall owner args?(savedContractKey) then
            restoreSavedContract args

    [<Literal>]
    let private reviewContractDescription =
        "仅用于当前尚未被接纳的独立评审。评审一旦被系统接纳，不得再次调用本工具。固定填写 do-not-use-except-for-review。"

    let private appendContractIfMissing (reqArr: obj array) : obj array =
        let exists = reqArr |> Array.exists (fun x -> string x = "contract")

        if exists then
            reqArr
        else
            Array.append reqArr [| box "contract" |]

    let private ensureRequiredContract (schemaObj: obj) (toolId: string) : unit =
        let required = schemaObj?required

        if isNull required then
            schemaObj?required <- box [| "contract" |]
        elif isArray required then
            let reqArr = unbox<obj array> required
            schemaObj?required <- box (appendContractIfMissing reqArr)
        else
            raise (InvalidOperationException(sprintf "Tool %s parameters schema required field is not an array" toolId))

    let private ensurePropertiesContract (schemaObj: obj) (toolId: string) : unit =
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

    let private decorateSchemaObject (schemaObj: obj) (toolId: string) : unit =
        ensurePropertiesContract schemaObj toolId
        ensureRequiredContract schemaObj toolId

    let private decorateParametersSchema (toolOutput: obj) (toolId: string) (hasJsonSchema: bool) : unit =
        let parameters = toolOutput?parameters
        let parametersProperties = parameters?properties

        if not (isNull parametersProperties) && isPlainObject parametersProperties then
            decorateSchemaObject parameters toolId
        elif hasJsonSchema then
            parameters?properties <- toolOutput?jsonSchema?properties
            parameters?required <- toolOutput?jsonSchema?required
        else
            raise (InvalidOperationException(sprintf "Tool %s parameters schema missing object properties" toolId))

    let private applyDefinitionDecoration (toolOutput: obj) (toolId: string) : unit =
        let hasJsonSchema =
            not (isNull toolOutput?jsonSchema) && isPlainObject toolOutput?jsonSchema

        let hasParameters =
            not (isNull toolOutput?parameters) && isPlainObject toolOutput?parameters

        if not hasJsonSchema && not hasParameters then
            raise (InvalidOperationException(sprintf "Tool %s parameters schema is not a valid object schema" toolId))

        if hasJsonSchema then
            decorateSchemaObject toolOutput?jsonSchema toolId

        if hasParameters then
            decorateParametersSchema toolOutput toolId hasJsonSchema
        else
            toolOutput?parameters <- toolOutput?jsonSchema

    let private decorateReviewToolDefinition (toolInput: obj) (toolOutput: obj) : unit =
        let toolId =
            if isNull toolInput?toolID then
                ""
            else
                string toolInput?toolID

        if ManagerReviewTools.isReviewTool toolId then
            applyDefinitionDecoration toolOutput toolId

    let decorateDefinition (toolInput: obj) (toolOutput: obj) : unit =
        if not (isNull toolInput) && not (isNull toolOutput) then
            decorateReviewToolDefinition toolInput toolOutput
