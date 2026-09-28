namespace Wanxiangshu.OpenCode.Host

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Strength

/// DELEGATE.md 4.2: explicit read-only delegation schema contract and
/// parameter boundary. Schema decoration, budget/note validation, provider
/// argument evidence preservation and bilingual collaboration prose live
/// here. This module never creates a child session, sends a provider
/// request, computes the batch max, or writes business events.
module ReadonlyDelegationContract =

    let private savedArgsKey: obj = emitJsExpr () "Symbol('readonly-delegation-args')"

    [<Literal>]
    let private roundsField = "delegate_readonly_rounds"

    [<Literal>]
    let private noteField = "self_note"

    [<Literal>]
    let private maximumRounds = 2147483647

    [<Emit("typeof $0 === 'object' && $0 !== null && !Array.isArray($0)")>]
    let private isPlainObject (value: obj) : bool = jsNative

    [<Emit("Array.isArray($0)")>]
    let private isArray (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'number'")>]
    let private isNumberValue (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'string'")>]
    let private isStringValue (value: obj) : bool = jsNative

    [<Emit("typeof $0 === 'undefined'")>]
    let private isUndefinedValue (value: obj) : bool = jsNative

    [<Emit("Number.isFinite($0)")>]
    let private isFiniteNumber (value: obj) : bool = jsNative

    [<Emit("Number.isInteger($0)")>]
    let private isIntegerNumber (value: obj) : bool = jsNative

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

    [<Emit("Object.keys($0).sort().join(',')")>]
    let private ownKeysSignature (value: obj) : string = jsNative

    [<Emit("throw new TypeError($0)")>]
    let private throwTypeError (message: string) : unit = jsNative

    // DELEGATE.md 3.2 schema fragments, verbatim.
    [<Literal>]
    let private readonlyRoundsDescription =
        "After every tool call in this assistant response has completed, delegate up to this many subsequent read-only model requests to your companion. One request may issue multiple parallel tool calls. The budget is the MAXIMUM value across all tool calls in this response; 0 does not veto another call's positive value. Your companion receives the current conversation and read-only tools, may finish early, and returns actual tool calls and results to you. Use 0 when you should retain control. This does not delay or replace the current tool calls and does not promise that an edit follows. During delegated read-only execution, set this field to 0; further delegation is disabled."

    [<Literal>]
    let private selfNoteDescription =
        "Optional brief first-person note to myself about my current intent, uncertainty, or what I want to check next. For example: 'I suspect the caller and entry point disagree about empty values; I want to check the callers next.' My companion can see this note in the conversation. It is a hint, not an established fact or a command. Omit it when it adds nothing."

    // DELEGATE.md 3.3 collaboration prose, verbatim.
    [<Literal>]
    let private englishCollaboration =
        "Fill in delegate_readonly_rounds on every tool call. After all calls in this response complete, the maximum value grants your companion up to that many read-only model requests. A 0 does not veto a positive value elsewhere in the batch. One request may call several tools in parallel. Your companion sees the current conversation, may stop early, and returns actual tool calls and results. Use 0 when you need to retain control. Do not invent work to fill the budget; using it up does not require an edit.\n\nWorking together is a way to learn about each other and build trust. Judge by the investigation you can actually see in the conversation. When your companion does well, trust him more and let him do as much of the work as your judgment supports. When he struggles in particular situations, delegate less or not at all in similar situations next time. When in doubt, estimating how many steps remain before the first edit can give you a starting point. This is only a heuristic baseline, not an optimal policy or a substitute for judgment, and it does not require an eventual edit.\n\nOptionally leave a brief self_note in the first person, such as \u201cI suspect the caller and entry point disagree about empty values; I want to check the callers next.\u201d Your companion will naturally see it in the conversation. Omit it when it adds nothing. A short intention or uncertainty is enough; do not write a full analysis or present a guess as a fact."

    [<Literal>]
    let private chineseCollaboration =
        "每个工具调用都要填写 delegate_readonly_rounds。当前响应的全部工具完成后，系统取这些值的最大值，把接下来的至多这么多轮只读调查交给你的同伴。0 不会否决同批其他调用的正数。一轮是一次模型请求，可以并行调用多个工具。同伴看到当前对话，只能只读，可随时提前结束；真实工具调用与结果会回到你的上下文。需要亲自判断时填 0；不要为了用满预算增加调查，预算到期也不要求立即修改代码。\n\n和同伴合作，也是逐渐了解彼此、建立信任的过程。根据你在对话中实际看见的调查表现判断：他做得好，就尽可能多信任他、多让他工作；他不善于某些状况，下次遇到类似状况就适当少用或不用。当拿不准时，可以先估计距离第一次 edit 还有几步，作为抛砖引玉的基线。这只是启发式方法，不代表最优，不能代替实际判断，也不要求你最终一定 edit。\n\n需要留下当前意图或线索时，可选填 self_note，用第一人称给自己留一句短记，例如“我怀疑入口与调用方对空值的约定不同，接下来先核对调用点”。同伴会在对话中自然看见它。没有有用的话就省略；不必写完整分析，也不要把猜测写成事实。"

    /// JS 边界先检查原生 number、有限值、整数和范围，再构造 F# 类型。
    /// 绝不使用 parseInt、字符串强转或 truthy 判断代替验证。
    let tryReadonlyRoundBudget (value: obj) : Result<ReadonlyRoundBudget, string> =
        if isUndefinedValue value then
            Error "delegate_readonly_rounds-missing"
        elif isNull value then
            Error "delegate_readonly_rounds-null"
        elif not (isNumberValue value) then
            Error "delegate_readonly_rounds-not-number"
        elif not (isFiniteNumber value) then
            Error "delegate_readonly_rounds-not-finite"
        elif not (isIntegerNumber value) then
            Error "delegate_readonly_rounds-not-integer"
        else
            let numeric = unbox<float> value

            if numeric < 0.0 then
                Error "delegate_readonly_rounds-negative"
            elif numeric > 2147483647.0 then
                Error "delegate_readonly_rounds-out-of-range"
            else
                match ReadonlyRoundBudget.tryCreate (int numeric) with
                | Ok budget -> Ok budget
                | Error message -> Error message

    /// self_note 缺失（JS undefined，即属性不存在）合法；出现时只接受
    /// 字符串（含空串）；null、数字、布尔、对象、数组一律拒绝，不强转；
    /// 不自动填充；不做“必须以我/I 开头”的正则门禁。
    let trySelfNote (value: obj) : Result<string option, string> =
        if isUndefinedValue value then Ok None
        elif isStringValue value then Ok(Some(string value))
        else Error "self_note-not-string"

    let private descriptorConfigurableOrAbsent (descriptor: obj) : bool =
        if isNull descriptor then
            true
        else
            let conf = descriptor?configurable
            not (isNull conf) && unbox<bool> conf

    let private deleteProtocolField (args: obj) (field: string) : unit =
        if not (isNull (getOwnPropertyDescriptor args field)) then
            let deleted = deleteProperty args field

            if not deleted then
                throwTypeError (sprintf "Tool arguments cannot hide the readonly delegation field %s" field)

    let private deleteProtocolFields (args: obj) : unit =
        try
            deleteProtocolField args roundsField
            deleteProtocolField args noteField
        with ex ->
            deleteProperty args savedArgsKey |> ignore
            raise ex

    let private hideProtocolFields (args: obj) : unit =
        let roundsDescriptor = getOwnPropertyDescriptor args roundsField
        let noteDescriptor = getOwnPropertyDescriptor args noteField

        if
            not (isExtensible args)
            || not (descriptorConfigurableOrAbsent roundsDescriptor)
            || not (descriptorConfigurableOrAbsent noteDescriptor)
        then
            throwTypeError "Tool arguments cannot hold or modify the readonly delegation fields"

        let saved = createObj [ "rounds", roundsDescriptor; "note", noteDescriptor ]

        let symbolDescriptor =
            createObj [ "value", saved; "enumerable", box false; "configurable", box true ]

        defineProperty args savedArgsKey symbolDescriptor
        deleteProtocolFields args

    /// Hide uses its own Symbol key; the manager review contract's saved
    /// record under Symbol('manager-review-contract') is never read, written
    /// or deleted here, so the two contracts coexist on the same args object.
    let hide (args: obj) : unit =
        if isNull args || not (isPlainObject args) then
            throwTypeError "Tool arguments must be an object"

        if not (hasOwn args savedArgsKey) then
            hideProtocolFields args

    let private restoreField (args: obj) (field: string) (descriptor: obj) : unit =
        let restored =
            if isNull descriptor then
                deleteProperty args field
            else
                defineProperty args field descriptor
                true

        if not restored then
            throwTypeError (sprintf "Failed to restore the readonly delegation field %s" field)

    let private restoreSavedFields (args: obj) (saved: obj) : unit =
        restoreField args roundsField saved?rounds
        restoreField args noteField saved?note

    let private restoreSavedArgs (args: obj) : unit =
        let saved = args?(savedArgsKey)

        if not (isExtensible args) then
            throwTypeError "Tool arguments are frozen or not extensible during readonly delegation restore"

        restoreSavedFields args saved

        let deletedKey = deleteProperty args savedArgsKey

        if not deletedKey then
            throwTypeError "Failed to delete the saved readonly delegation key"

    let restore (args: obj) : unit =
        if not (isNull args) && isPlainObject args && hasOwn args savedArgsKey then
            restoreSavedArgs args

    let private budgetProperty () : obj =
        createObj
            [ "type", box "integer"
              "minimum", box 0
              "maximum", box maximumRounds
              "description", box readonlyRoundsDescription ]

    let private noteProperty () : obj =
        createObj [ "type", box "string"; "description", box selfNoteDescription ]

    let private isSameBudgetProperty (value: obj) : bool =
        not (isNull value)
        && isPlainObject value
        && ownKeysSignature value = "description,maximum,minimum,type"
        && string value?``type`` = "integer"
        && unbox<float> value?minimum = 0.0
        && unbox<float> value?maximum = 2147483647.0
        && string value?description = readonlyRoundsDescription

    let private isSameNoteProperty (value: obj) : bool =
        not (isNull value)
        && isPlainObject value
        && ownKeysSignature value = "description,type"
        && string value?``type`` = "string"
        && string value?description = selfNoteDescription

    let private ensureBudgetProperty (properties: obj) (toolId: string) : unit =
        let existing = properties?delegate_readonly_rounds

        if isNull existing then
            properties?delegate_readonly_rounds <- budgetProperty ()
        elif not (isSameBudgetProperty existing) then
            raise (
                InvalidOperationException(
                    sprintf
                        "Tool %s defines a conflicting delegate_readonly_rounds property that differs from the readonly delegation protocol"
                        toolId
                )
            )

    let private ensureNoteProperty (properties: obj) (toolId: string) : unit =
        let existing = properties?self_note

        if isNull existing then
            properties?self_note <- noteProperty ()
        elif not (isSameNoteProperty existing) then
            raise (
                InvalidOperationException(
                    sprintf
                        "Tool %s defines a conflicting self_note property that differs from the readonly delegation protocol"
                        toolId
                )
            )

    let private appendBudgetIfMissing (reqArr: obj array) : obj array =
        let exists = reqArr |> Array.exists (fun x -> string x = roundsField)

        if exists then
            reqArr
        else
            Array.append reqArr [| box roundsField |]

    /// Only the budget joins required; the note never does. Original required
    /// entries are preserved. A non-array required fails loudly.
    let private ensureRequiredBudget (schemaObj: obj) (toolId: string) : unit =
        let required = schemaObj?required

        if isNull required then
            schemaObj?required <- box [| roundsField |]
        elif isArray required then
            schemaObj?required <- box (appendBudgetIfMissing (unbox<obj array> required))
        else
            raise (InvalidOperationException(sprintf "Tool %s parameters schema required field is not an array" toolId))

    /// Decorate one root schema view. The original composition structure
    /// ($ref/oneOf/nullable/strict) is preserved; only properties and
    /// required are extended.
    let private decorateRootSchema (schemaObj: obj) (toolId: string) : unit =
        if isNull schemaObj || not (isPlainObject schemaObj) then
            raise (InvalidOperationException(sprintf "Tool %s schema is not a plain object schema" toolId))

        let properties = schemaObj?properties

        if isNull properties || not (isPlainObject properties) then
            raise (InvalidOperationException(sprintf "Tool %s parameters schema missing object properties" toolId))

        ensureBudgetProperty properties toolId
        ensureNoteProperty properties toolId
        ensureRequiredBudget schemaObj toolId

    let private stripCollaborationBlocks (text: string) : string =
        text
            .Replace(englishCollaboration, "")
            .Replace(chineseCollaboration, "")
            .TrimEnd('\n')

    let private appendCollaborationDescription (toolOutput: obj) : unit =
        if not (isPlainObject toolOutput) then
            raise (InvalidOperationException "Tool definition output must be an object")

        let current =
            if isNull toolOutput?description then
                ""
            else
                string toolOutput?description

        let language = ProviderLanguageBinding.forSessionText ""

        let block =
            match language with
            | ProviderLanguage.SimplifiedChinese -> chineseCollaboration
            | _ -> englishCollaboration

        toolOutput?description <- stripCollaborationBlocks current + "\n\n" + block

    /// A definition that already states its provider view as `properties` on
    /// `parameters` needs no Effect rendering; it is decorated where it stands
    /// and published as the JSON schema so the Host sends exactly these bytes.
    let private parametersHoldSchemaView (parameters: obj) : bool =
        if not (isPlainObject parameters) then
            false
        else
            let properties = parameters?properties
            not (isNull properties) && isPlainObject properties

    /// The Host renders the provider-visible schema itself for any tool whose
    /// definition carries an Effect argument schema (`ToolJsonSchema.fromTool`:
    /// a stated JSON schema, otherwise `Schema.toJsonSchemaDocument` of
    /// `parameters`, opencode 1.18.32 `src/tool/json-schema.ts`). Decoration
    /// therefore publishes through the JSON schema view, whichever shape the
    /// definition arrives in:
    ///
    ///  - a stated `jsonSchema` (plugin tools such as the engineering surfaces)
    ///    is decorated where it stands;
    ///  - `parameters` that already carries `properties` is that view itself;
    ///    it is decorated in place and published as the JSON schema;
    ///  - otherwise `parameters` is the Effect argument schema of a Host
    ///    built-in (read, edit, question, …) and the JSON schema is rendered
    ///    with the same conversion the Host applies.
    ///
    /// The Effect schema is never modified — the Host decodes the real
    /// arguments with it, and `tool.execute.before` hides the protocol fields
    /// before that decode.
    let private decorateToolDefinition (toolInput: obj) (toolOutput: obj) : unit =
        let toolId =
            if isNull toolInput?toolID then
                ""
            else
                string toolInput?toolID

        if toolId <> "invalid" then
            let jsonSchema = toolOutput?jsonSchema
            let parameters = toolOutput?parameters
            let hasJsonSchema = not (isNull jsonSchema) && isPlainObject jsonSchema

            if not hasJsonSchema && not (isPlainObject parameters) then
                raise (
                    InvalidOperationException(sprintf "Tool %s parameters schema is not a valid object schema" toolId)
                )

            if hasJsonSchema then
                decorateRootSchema jsonSchema toolId
            elif parametersHoldSchemaView parameters then
                decorateRootSchema parameters toolId
                toolOutput?jsonSchema <- parameters
            else
                let rendered = ToolSchemaJson.providerSchema parameters
                decorateRootSchema rendered toolId
                toolOutput?jsonSchema <- rendered

            appendCollaborationDescription toolOutput

    let decorateDefinition (toolInput: obj) (toolOutput: obj) : unit =
        if not (isNull toolInput) && not (isNull toolOutput) then
            decorateToolDefinition toolInput toolOutput
