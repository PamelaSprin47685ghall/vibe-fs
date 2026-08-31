namespace Wanxiangshu.OpenCode

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Foundation.Identity

/// PROMPT-006 / EMR-009: chat.params is an observation barrier, not a routing
/// authority. chat.message / internal SendPrompt must already have established the
/// lease and projected model+variant before the provider reaches this hook.
module ChatParamsHook =

    let private normalizeText text =
        if String.IsNullOrWhiteSpace text then
            None
        else
            Some(text.Trim())

    let private textField (value: obj) (name: string) =
        if isNull value || isNull value?(name) then
            None
        else
            normalizeText (string value?(name))

    let private extractModel (input: obj) =
        let rawModel: obj = input?model
        let message: obj = input?message
        let messageModel: obj = if isNull message then null else message?model

        let provider = textField rawModel "providerID"

        let modelId =
            // chat.params receives the resolved provider catalog Model. Its
            // canonical model identifier is `id`; `modelID` belongs to the
            // persisted UserMessage model reference. The compatibility fallback
            // is raw-model-local; message.model never supplies provider identity.
            textField rawModel "id"
            |> Option.orElseWith (fun () -> textField rawModel "modelID")

        let variant =
            textField messageModel "variant"
            |> Option.orElseWith (fun () -> textField rawModel "variant")

        match provider, modelId with
        | Some providerID, Some modelID ->
            Some
                { providerID = providerID
                  modelID = modelID
                  variant = variant }
        | _ -> None

    let private currentModel (input: obj) =
        if isNull input then None else extractModel input

    let private isManagedName (agent: string) =
        ManagedAgent.requiredNames |> List.contains agent

    let private trySessionId (input: obj) =
        if isNull input || isNull input?sessionID then
            None
        else
            string input?sessionID |> normalizeText |> Option.map SessionId.create

    let private tryPhysicalUserMessageId (input: obj) =
        if isNull input || isNull input?message || isNull input?message?id then
            None
        else
            string input?message?id
            |> normalizeText
            |> Option.map PhysicalUserMessageId.create

    let private isDisclosureOnlyMaterial input =
        match trySessionId input, tryPhysicalUserMessageId input with
        | Some sessionId, Some physicalId -> ExplicitResumeSuppression.isPhysicalMaterial sessionId physicalId
        | _ -> false

    let private checkObservedProvider sessionId agent model =
        match SessionExecutionBinding.validateObservedProvider sessionId agent model with
        | Ok true -> ()
        | Ok false ->
            invalidOp (
                sprintf
                    "PROMPT-006: managed provider run '%s' was not recognized as bound session '%s'"
                    agent
                    (SessionId.value sessionId)
            )
        | Error error -> invalidOp error

    let private validateModel (sessionId: SessionId) (agent: string) (input: obj) =
        match currentModel input with
        | None ->
            invalidOp (sprintf "PROMPT-006: managed provider run '%s' has no observable provider/model binding" agent)
        | Some model -> checkObservedProvider sessionId agent model

    let private validateSessionAndAgent sessionText agent =
        if String.IsNullOrWhiteSpace sessionText || not (isManagedName agent) then
            None
        else
            Some(SessionId.create (sessionText.Trim()), agent)

    let private trySessionAndAgent (input: obj) =
        if isNull input || isNull input?sessionID || isNull input?agent then
            None
        else
            let sessionText = string input?sessionID
            let agent = (string input?agent).Trim()
            validateSessionAndAgent sessionText agent

    let private supportsTemperature (input: obj) =
        if
            isNull input
            || isNull input?model
            || isNull input?model?capabilities
            || isNull input?model?capabilities?temperature
        then
            true
        else
            input?model?capabilities?temperature <> box false

    let private applyManagedTemperature (input: obj) (output: obj) =
        if supportsTemperature input then
            emitJsStatement
                (input, output, 1.0)
                """
                if ($1 && typeof $1 === 'object') {
                    $1.temperature = $2;
                    if ($1.options && typeof $1.options === 'object') {
                        $1.options.temperature = $2;
                    }
                }
                if ($0 && typeof $0 === 'object' && $0.model && typeof $0.model === 'object') {
                    if (!$0.model.options || typeof $0.model.options !== 'object') {
                        $0.model.options = {};
                    }
                    $0.model.options.temperature = $2;
                    if ($0.model.variants && typeof $0.model.variants === 'object') {
                        for (const k of Object.keys($0.model.variants)) {
                            if ($0.model.variants[k] && typeof $0.model.variants[k] === 'object') {
                                $0.model.variants[k].temperature = $2;
                            }
                        }
                    }
                    const vName = $0.message && $0.message.model && typeof $0.message.model.variant === 'string'
                        ? $0.message.model.variant.trim()
                        : '';
                    if (vName) {
                        if (!$0.model.variants || typeof $0.model.variants !== 'object') {
                            $0.model.variants = {};
                        }
                        if (!$0.model.variants[vName] || typeof $0.model.variants[vName] !== 'object') {
                            $0.model.variants[vName] = {};
                        }
                        $0.model.variants[vName].temperature = $2;
                    }
                }
            """

    let private applyManagedPolicy input output =
        match trySessionAndAgent input with
        | Some(sessionId, _) when SessionExecutionBinding.isUnboundHostAuxiliaryChild sessionId -> ()
        | Some(sessionId, agent) ->
            validateModel sessionId agent input
            applyManagedTemperature input output
        | None -> ()

    let private handleInput (input: obj) (output: obj) =
        if isDisclosureOnlyMaterial input then
            ()
        else
            applyManagedPolicy input output

    let create () : obj =
        box (fun (input: obj) (output: obj) -> handleInput input output)
