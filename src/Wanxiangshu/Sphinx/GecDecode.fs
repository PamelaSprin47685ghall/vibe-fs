namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Core
open Wanxiangshu.Sphinx.Runtime

module GecDecode =

    let private fail code message : Result<'value, CoreError> =
        Error { Code = code; Message = message }

    let private withCode (code: string) (outcome: Result<'value, string>) : Result<'value, CoreError> =
        outcome
        |> Result.mapError (fun message -> ({ Code = code; Message = message }: CoreError))

    let private fieldError (name: string) (expectation: string) : CoreError =
        { Code = "invalid-" + name
          Message = name + " must be " + expectation }

    let private isNullish (value: obj) : bool = emitJsExpr value "$0 == null"

    let private isArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private getField (raw: obj) (name: string) : obj = emitJsExpr (raw, name) "$0[$1]"

    let private hasKey (raw: obj) (name: string) : bool =
        emitJsExpr (raw, name) "Object.prototype.hasOwnProperty.call($0,$1)"

    let private jsType (value: obj) : string = emitJsExpr value "typeof $0"

    let private isFiniteNumber (value: obj) : bool = emitJsExpr value "Number.isFinite($0)"

    let private asString (value: obj) : Result<string, string> =
        if jsType value = "string" then
            Ok(unbox<string> value)
        else
            Error "expected string"

    let private asFloat (value: obj) : Result<float, string> =
        if jsType value = "number" && isFiniteNumber value then
            Ok(unbox<float> value)
        else
            Error "expected finite number"

    let private fieldString (raw: obj) (name: string) : Result<string, CoreError> =
        let found = getField raw name

        if isNullish found then
            fail ("missing-" + name) (name + " is required")
        else
            asString found |> Result.mapError (fun _ -> fieldError name "a string")

    let private optString (raw: obj) (name: string) : string option =
        let found = getField raw name

        if isNullish found then
            None
        else
            asString found
            |> Result.toOption
            |> Option.filter (fun text -> not (String.IsNullOrWhiteSpace text))

    let private fieldInt (raw: obj) (name: string) : Result<int, CoreError> =
        let found = getField raw name

        if isNullish found then
            fail ("missing-" + name) (name + " is required")
        else
            asFloat found
            |> Result.mapError (fun _ -> fieldError name "a finite number")
            |> Result.map int

    let private optFloat (raw: obj) (name: string) : float option =
        let found = getField raw name

        if isNullish found then
            None
        else
            asFloat found |> Result.toOption

    let private stringListItem (item: obj) : Result<string, CoreError> =
        item
        |> asString
        |> Result.mapError (fun _ ->
            ({ Code = "invalid-list"
               Message = "array items must be strings" }
            : CoreError))

    let private stringArrayValue (value: obj) : Result<string list, CoreError> =
        if not (isArray value) then
            fail "invalid-list" "expected string array"
        else
            let items = unbox<obj array> value |> Array.toList

            items
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun (acc: string list) ->
                        stringListItem item |> Result.map (fun text -> text :: acc)))
                (Ok [])
            |> Result.map List.rev

    let private optStringArray (raw: obj) (name: string) : Result<string list, CoreError> =
        let found = getField raw name

        if isNullish found then Ok [] else stringArrayValue found

    let private budgetAmountOf (key: string) (amount: obj) : Result<float, CoreError> =
        amount
        |> asFloat
        |> Result.mapError (fun _ ->
            ({ Code = "invalid-budget"
               Message = "budget " + key + " must be finite" }
            : CoreError))

    let private budgetValue (value: obj) : Result<ResourceBudget, CoreError> =
        if isNullish value || jsType value <> "object" || isArray value then
            fail "invalid-budget" "budget must be an object"
        else
            keysOf value
            |> Array.toList
            |> List.fold
                (fun state key ->
                    state
                    |> Result.bind (fun (acc: ResourceBudget) ->
                        let amount = getField value key

                        budgetAmountOf key amount |> Result.map (fun number -> Map.add key number acc)))
                (Ok Map.empty)

    let private schemaFieldText (field: obj) : string =
        asString field |> Result.toOption |> Option.defaultValue ""

    let private validateSchemaFields (idField: obj) (hashField: obj) : Result<unit, CoreError> =
        if jsType idField <> "string" && not (isNullish idField) then
            fail "invalid-schema" "schema id must be a string"
        elif jsType hashField <> "string" && not (isNullish hashField) then
            fail "invalid-schema" "schema hash must be a string"
        else
            Ok()

    let private schemaRefValue (value: obj) : Result<SchemaRef, CoreError> =
        if isNullish value || jsType value <> "object" then
            fail "invalid-schema" "schema must be an object"
        else
            let idField = getField value "id"
            let hashField = getField value "hash"
            let idText = schemaFieldText idField
            let hashText = schemaFieldText hashField

            validateSchemaFields idField hashField
            |> Result.map (fun () -> ({ Id = idText; Hash = hashText }: SchemaRef))

    let private manifestSchemas (raw: obj) : Result<Map<string, SchemaRef>, CoreError> =
        let found = getField raw "schemas"

        if isNullish found then
            Ok Map.empty
        elif jsType found <> "object" || isArray found then
            fail "invalid-manifest" "schemas must be an object"
        else
            keysOf found
            |> Array.toList
            |> List.fold
                (fun state name ->
                    state
                    |> Result.bind (fun acc ->
                        schemaRefValue (getField found name)
                        |> Result.map (fun schema -> Map.add name schema acc)))
                (Ok Map.empty)

    let decodeManifest (raw: obj) : Result<PluginManifest, CoreError> =
        if isNullish raw || jsType raw <> "object" then
            fail "invalid-manifest" "manifest must be an object"
        else
            fieldString raw "id"
            |> Result.bind (fun id ->
                fieldString raw "release"
                |> Result.bind (fun release ->
                    fieldString raw "abiHash"
                    |> Result.bind (fun abiHash ->
                        optStringArray raw "capabilities"
                        |> Result.bind (fun capabilities ->
                            optStringArray raw "dependencies"
                            |> Result.bind (fun dependencies ->
                                manifestSchemas raw
                                |> Result.map (fun schemas ->
                                    { Id = id
                                      Release = release
                                      AbiHash = abiHash
                                      Capabilities = Set.ofList capabilities
                                      Dependencies = Set.ofList dependencies
                                      Schemas = schemas }))))))

    let private lockEntryOfManifest (manifest: PluginManifest) : PluginLockEntry = Plugin.toLockEntry manifest

    let private lockEntryOfRaw (raw: obj) : Result<PluginLockEntry, CoreError> =
        let plugin = getField raw "plugin"

        if isNullish plugin then
            decodeManifest raw |> Result.map lockEntryOfManifest
        else
            fieldString plugin "id"
            |> Result.bind (fun id ->
                fieldString plugin "release"
                |> Result.bind (fun release ->
                    fieldString plugin "abiHash"
                    |> Result.bind (fun abiHash ->
                        optStringArray raw "capabilities"
                        |> Result.bind (fun capabilities ->
                            optStringArray raw "dependencies"
                            |> Result.bind (fun dependencies ->
                                manifestSchemas raw
                                |> Result.map (fun schemas ->
                                    { Plugin =
                                        { Id = id
                                          Release = release
                                          AbiHash = abiHash }
                                      Capabilities = Set.ofList capabilities
                                      Dependencies = Set.ofList dependencies
                                      Schemas = schemas }))))))

    let decodeLockEntries (raw: obj) : Result<PluginLockEntry list, CoreError> =
        if isNullish raw then
            fail "missing-plugin-lock" "plugin lock is required"
        elif not (isArray raw) then
            fail "invalid-plugin-lock" "plugin lock must be an array"
        else
            unbox<obj array> raw
            |> Array.toList
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun acc -> lockEntryOfRaw item |> Result.map (fun entry -> entry :: acc)))
                (Ok [])
            |> Result.map List.rev

    let private envelopeWith (schema: SchemaRef) (payload: obj) : Result<JsonEnvelope, CoreError> =
        let body = if isNullish payload then box {| |} else payload

        JsonEnvelope.create schema body
        |> Result.mapError (fun message ->
            ({ Code = "invalid-envelope"
               Message = message }
            : CoreError))

    let private emptySchema (id: string) (hash: string) : SchemaRef = { Id = id; Hash = hash }

    let private emptyEnvelope (id: string) (hash: string) : Result<JsonEnvelope, CoreError> =
        envelopeWith (emptySchema id hash) (box {| |})

    let private decodeInquiryCreated (raw: obj) : Result<CoreEventBody, CoreError> =
        fieldString raw "question"
        |> Result.bind (fun question ->
            let lockRaw = getField raw "pluginLock"

            decodeLockEntries lockRaw
            |> Result.bind (fun lockEntries ->
                let budgetRaw = getField raw "budget"

                (if isNullish budgetRaw then
                     fail "missing-budget" "budget is required"
                 else
                     budgetValue budgetRaw)
                |> Result.bind (fun budget ->
                    let rootRaw = getField raw "root"

                    (if isNullish rootRaw then
                         fail "missing-root" "root is required"
                     else
                         let envelopeRaw = getField rootRaw "envelope"

                         (if isNullish envelopeRaw then
                              fail "missing-root" "root envelope is required"
                          else
                              schemaRefValue (getField envelopeRaw "schema")
                              |> Result.bind (fun schema ->
                                  let payload = getField envelopeRaw "payload"
                                  let body = if isNullish payload then box {| |} else payload
                                  envelopeWith schema body)))
                    |> Result.bind (fun rootEnvelope ->
                        let body = InquiryCreated(rootEnvelope, lockEntries, budget)

                        if String.IsNullOrWhiteSpace question then
                            fail "missing-question" "question must not be blank"
                        else
                            Ok body))))

    let private workIds (raw: obj) (name: string) : Result<WorkId list, CoreError> =
        optStringArray raw name
        |> Result.bind (fun names ->
            names
            |> List.fold
                (fun state name ->
                    state
                    |> Result.bind (fun (acc: WorkId list) ->
                        WorkId.tryCreate name
                        |> withCode "invalid-dependency"
                        |> Result.map (fun id -> id :: acc)))
                (Ok [])
            |> Result.map List.rev)

    let private decodeWorkIds (idText: string) (branchText: string) : Result<WorkId * BranchId, CoreError> =
        match WorkId.tryCreate idText, BranchId.tryCreate branchText with
        | Error message, _ -> fail "invalid-work-id" message
        | _, Error message -> fail "invalid-branch-id" message
        | Ok id, Ok branch -> Ok(id, branch)

    let private buildWorkSpec
        (id: WorkId)
        (branch: BranchId)
        (attempt: int)
        (dependencies: WorkId list)
        : Result<WorkSpec, CoreError> =
        if attempt < 1 then
            fail "invalid-attempt" "work attempt must be positive"
        else
            Ok(
                { Id = id
                  BranchId = branch
                  Attempt = attempt
                  Producer = None
                  Capability = ""
                  Input = None
                  OutputSchema = None
                  Dependencies = Set.ofList dependencies
                  ConflictKeys = Set.empty
                  BlindToken = None
                  RandomSeed = "0"
                  Budget = Map.empty }
                : WorkSpec
            )

    let private decodeWorkSpec (work: obj) : Result<WorkSpec, CoreError> =
        fieldString work "id"
        |> Result.bind (fun idText ->
            fieldString work "branch"
            |> Result.bind (fun branchText ->
                fieldInt work "attempt"
                |> Result.bind (fun attempt ->
                    workIds work "dependencies"
                    |> Result.bind (fun dependencies ->
                        decodeWorkIds idText branchText
                        |> Result.bind (fun (id, branch) -> buildWorkSpec id branch attempt dependencies)))))

    let private decodeWorkPlanned (raw: obj) : Result<CoreEventBody, CoreError> =
        let work = getField raw "work"

        if isNullish work then
            fail "missing-work" "work is required"
        else
            decodeWorkSpec work |> Result.map (fun spec -> WorkPlanned [ spec ])

    let private wallClockFields: string list =
        [ "leaseExpiresAt"; "heartbeatTimeout"; "wallClock"; "expiresAt"; "timeoutMs" ]

    let private hasWallClock (raw: obj) (work: obj) : bool =
        wallClockFields
        |> List.exists (fun name ->
            (not (isNullish raw) && hasKey raw name)
            || (not (isNullish work) && hasKey work name))

    let private leaseProofOf (work: obj) (attempt: int) : LeaseProof =
        let fence = optString work "fence" |> Option.defaultValue ""
        let session = optString work "session"

        ({ Attempt = attempt
           Fence = fence
           Session = session }
        : LeaseProof)

    let private completionProofOf (raw: obj) (attempt: int) : Result<CompletionProof, CoreError> =
        let observation = getField raw "observation"
        let errorValue = getField raw "error"
        let reason = getField raw "reason"

        let chosen, schemaId, schemaHash =
            if not (isNullish observation) then
                observation, "sphinx.completion/observation@1", "sphinx-completion-observation-v1"
            elif not (isNullish errorValue) then
                errorValue, "sphinx.completion/error@1", "sphinx-completion-error-v1"
            elif not (isNullish reason) then
                reason, "sphinx.completion/reason@1", "sphinx-completion-reason-v1"
            else
                null, "", ""

        if isNullish chosen then
            Ok
                { Attempt = attempt
                  EventId = None
                  Detail = None }
        else
            envelopeWith (emptySchema schemaId schemaHash) chosen
            |> Result.map (fun detail ->
                { Attempt = attempt
                  EventId = None
                  Detail = Some detail })

    let private successorIdText (successor: obj) : string =
        if jsType successor = "string" then
            unbox<string> successor
        else
            schemaFieldText (getField successor "id")

    let private decodeSupersededState (raw: obj) : Result<WorkId, CoreError> =
        let successor = getField raw "successor"

        if isNullish successor then
            fail "missing-successor" "superseded work requires a successor"
        else
            WorkId.tryCreate (successorIdText successor) |> withCode "invalid-successor"

    let private decodeWorkState
        (raw: obj)
        (work: obj)
        (spec: WorkSpec)
        (toState: string)
        : Result<WorkState, CoreError> =
        match toState with
        | "Planned" -> Ok Planned
        | "Ready" -> Ok Ready
        | "Leased" -> Ok(Leased(leaseProofOf work spec.Attempt))
        | "Executing" -> Ok(Executing(leaseProofOf work spec.Attempt))
        | "InputRequired" -> Ok(WorkState.InputRequired(leaseProofOf work spec.Attempt))
        | "Succeeded" -> completionProofOf raw spec.Attempt |> Result.map Succeeded
        | "Failed" -> completionProofOf raw spec.Attempt |> Result.map WorkState.Failed
        | "Cancelled" -> completionProofOf raw spec.Attempt |> Result.map WorkState.Cancelled
        | "Superseded" -> decodeSupersededState raw |> Result.map Superseded
        | _ -> fail "illegal-transition" ("unknown work state " + toState)

    let private decodeWorkTransitioned (raw: obj) : Result<CoreEventBody, CoreError> =
        let work = getField raw "work"

        if isNullish work then
            fail "missing-work" "work is required"
        elif hasWallClock raw work then
            fail "wall-clock-field" "wall clock fields must never drive the lifecycle"
        else
            decodeWorkSpec work
            |> Result.bind (fun (spec: WorkSpec) ->
                fieldString raw "from"
                |> Result.bind (fun fromState ->
                    fieldString raw "to"
                    |> Result.bind (fun toState ->
                        decodeWorkState raw work spec toState
                        |> Result.map (fun (next: WorkState) -> WorkTransitioned(spec, fromState, next)))))

    let private validateGraphPatch (kind: string) (relation: string) : Result<unit, CoreError> =
        if String.IsNullOrWhiteSpace kind then
            fail "invalid-patch" "node kind must not be blank"
        elif String.IsNullOrWhiteSpace relation then
            fail "invalid-patch" "edge relation must not be blank"
        else
            Ok()

    let private decodeGraphPayload (raw: obj) : Result<JsonEnvelope, CoreError> =
        let envelopeRaw = getField raw "envelope"

        if isNullish envelopeRaw then
            emptyEnvelope "sphinx.graph/empty@1" "sphinx-graph-empty-v1"
        else
            schemaRefValue (getField envelopeRaw "schema")
            |> Result.bind (fun (schema: SchemaRef) ->
                let payload = getField envelopeRaw "payload"
                let body = if isNullish payload then box {| |} else payload
                envelopeWith schema body)

    let private graphPatchedOf
        (target: NodeId)
        (kind: string)
        (relation: string)
        (payload: JsonEnvelope)
        (revision: int)
        : CoreEventBody =
        let node: GraphNode =
            { Id = target
              Kind = kind
              Payload = payload
              Revision = int64 revision }

        let edgeId = EdgeId.create ("e" + string revision)

        let edge: HyperEdge =
            { Id = edgeId
              Tails = Set.ofList [ target ]
              Heads = Set.ofList [ target ]
              Relation = relation
              Payload = None }

        GraphPatched(
            { UpsertNodes = [ node ]
              RemoveNodes = []
              UpsertEdges = [ edge ]
              RemoveEdges = [] }
            : GraphPatch
        )

    let private decodeGraphPatched (raw: obj) (revision: int) : Result<CoreEventBody, CoreError> =
        let patch = getField raw "patch"

        if isNullish patch then
            fail "missing-patch" "patch is required"
        else
            fieldString patch "kind"
            |> Result.bind (fun kind ->
                fieldString patch "relation"
                |> Result.bind (fun relation ->
                    fieldString patch "target"
                    |> Result.bind (fun targetText ->
                        NodeId.tryCreate targetText
                        |> withCode "invalid-node"
                        |> Result.bind (fun (target: NodeId) ->
                            validateGraphPatch kind relation
                            |> Result.bind (fun () ->
                                decodeGraphPayload raw
                                |> Result.map (fun payload -> graphPatchedOf target kind relation payload revision))))))

    let private bindingEnvelope
        (observation: obj)
        (name: string)
        (schemaId: string)
        (schemaHash: string)
        (required: bool)
        : Result<JsonEnvelope, CoreError> =
        let found = getField observation name

        match isNullish found, required with
        | true, true -> fail ("missing-" + name) (name + " is required")
        | true, false -> emptyEnvelope schemaId schemaHash
        | false, _ -> envelopeWith (emptySchema schemaId schemaHash) found

    let private observationRequiredFields: string list =
        [ "rootSnapshotHash"
          "branch"
          "work"
          "attempt"
          "pluginLock"
          "schema"
          "promptId"
          "questionId"
          "wording"
          "permutation"
          "treatment"
          "blindToken"
          "seed"
          "model"
          "sampling"
          "usage" ]

    let private missingObservationField (observation: obj) : string option =
        observationRequiredFields
        |> List.tryFind (fun name -> isNullish (getField observation name))

    let private requireObservationFields (observation: obj) : Result<unit, CoreError> =
        match missingObservationField observation with
        | Some name -> fail ("missing-" + name) (name + " is required")
        | None -> Ok()

    let private decodeObservationIds
        (branchText: string)
        (workText: string)
        (blindText: string)
        : Result<BranchId * WorkId * BlindToken, CoreError> =
        match BranchId.tryCreate branchText, WorkId.tryCreate workText, BlindToken.tryCreate blindText with
        | Error message, _, _ -> fail "invalid-branch-id" message
        | _, Error message, _ -> fail "invalid-work-id" message
        | _, _, Error message -> fail "invalid-blind-token" message
        | Ok branch, Ok work, Ok blind -> Ok(branch, work, blind)

    let private validateObservationScalars
        (attempt: int)
        (rootSnapshot: string)
        (seed: string)
        : Result<unit, CoreError> =
        if attempt < 1 then
            fail "invalid-attempt" "attempt must be positive"
        elif String.IsNullOrWhiteSpace rootSnapshot then
            fail "missing-rootSnapshotHash" "root snapshot must not be blank"
        elif String.IsNullOrWhiteSpace seed then
            fail "missing-seed" "seed must not be blank"
        else
            Ok()

    let private decodeObservation (raw: obj) : Result<CoreEventBody, CoreError> =
        let observation = getField raw "observation"

        if isNullish observation then
            fail "missing-observation" "observation is required"
        else
            requireObservationFields observation
            |> Result.bind (fun () ->
                fieldString observation "rootSnapshotHash"
                |> Result.bind (fun rootSnapshot ->
                    fieldString observation "branch"
                    |> Result.bind (fun branchText ->
                        fieldString observation "work"
                        |> Result.bind (fun workText ->
                            fieldInt observation "attempt"
                            |> Result.bind (fun attempt ->
                                decodeLockEntries (getField observation "pluginLock")
                                |> Result.bind (fun pluginLock ->
                                    schemaRefValue (getField observation "schema")
                                    |> Result.bind (fun schema ->
                                        fieldString observation "promptId"
                                        |> Result.bind (fun promptId ->
                                            fieldString observation "questionId"
                                            |> Result.bind (fun questionId ->
                                                fieldString observation "treatment"
                                                |> Result.bind (fun treatment ->
                                                    fieldString observation "blindToken"
                                                    |> Result.bind (fun blindText ->
                                                        fieldString observation "seed"
                                                        |> Result.bind (fun seed ->
                                                            decodeObservationIds
                                                                branchText
                                                                workText
                                                                blindText
                                                            |> Result.bind (fun (branch, work, blind) ->
                                                                validateObservationScalars
                                                                    attempt
                                                                    rootSnapshot
                                                                    seed
                                                                |> Result.bind (fun () ->
                                                                    bindingEnvelope
                                                                        observation
                                                                        "wording"
                                                                        "sphinx.wording@1"
                                                                        "sphinx-wording-v1"
                                                                        true
                                                                    |> Result.bind (fun wording ->
                                                                        bindingEnvelope
                                                                            observation
                                                                            "permutation"
                                                                            "sphinx.permutation@1"
                                                                            "sphinx-permutation-v1"
                                                                            true
                                                                        |> Result.bind
                                                                            (fun permutation ->
                                                                                bindingEnvelope
                                                                                    observation
                                                                                    "model"
                                                                                    "sphinx.model@1"
                                                                                    "sphinx-model-v1"
                                                                                    true
                                                                                |> Result.bind
                                                                                    (fun model ->
                                                                                        bindingEnvelope
                                                                                            observation
                                                                                            "sampling"
                                                                                            "sphinx.sampling@1"
                                                                                            "sphinx-sampling-v1"
                                                                                            true
                                                                                        |> Result.bind
                                                                                            (fun
                                                                                                sampling ->
                                                                                                bindingEnvelope
                                                                                                    observation
                                                                                                    "usage"
                                                                                                    "sphinx.usage@1"
                                                                                                    "sphinx-usage-v1"
                                                                                                    true
                                                                                                |> Result.bind
                                                                                                    (fun
                                                                                                        usage ->
                                                                                                        bindingEnvelope
                                                                                                            observation
                                                                                                            "payload"
                                                                                                            "sphinx.observation/payload@1"
                                                                                                            "sphinx-observation-payload-v1"
                                                                                                            false
                                                                                                        |> Result.map
                                                                                                            (fun
                                                                                                                payload ->
                                                                                                                ObservationAccepted(
                                                                                                                    { RootSnapshotHash =
                                                                                                                        rootSnapshot
                                                                                                                      BranchId =
                                                                                                                        branch
                                                                                                                      WorkId =
                                                                                                                        work
                                                                                                                      Attempt =
                                                                                                                        attempt
                                                                                                                      PluginLock =
                                                                                                                        pluginLock
                                                                                                                      Schema =
                                                                                                                        schema
                                                                                                                      PromptId =
                                                                                                                        promptId
                                                                                                                      QuestionId =
                                                                                                                        questionId
                                                                                                                      Wording =
                                                                                                                        wording
                                                                                                                      Permutation =
                                                                                                                        permutation
                                                                                                                      Treatment =
                                                                                                                        treatment
                                                                                                                      BlindToken =
                                                                                                                        blind
                                                                                                                      RandomSeed =
                                                                                                                        seed
                                                                                                                      Model =
                                                                                                                        model
                                                                                                                      Sampling =
                                                                                                                        sampling
                                                                                                                      Usage =
                                                                                                                        usage
                                                                                                                      Payload =
                                                                                                                        payload }
                                                                                                                    : ProtocolBinding
                                                                                                                )))))))))))))))))))))

    let private stringArrayOption (raw: obj) (name: string) : string[] option =
        let found = getField raw name

        if isNullish found then
            None
        else
            stringArrayValue found |> Result.toOption |> Option.map List.toArray

    let private tryEventId (item: obj) : EventId option =
        if jsType item = "string" then
            EventId.tryCreate (unbox<string> item) |> Result.toOption
        else
            None

    let private eventIdList (raw: obj) (name: string) : EventId list =
        let found = getField raw name

        if isNullish found || not (isArray found) then
            []
        else
            unbox<obj array> found |> Array.toList |> List.choose tryEventId

    let private rawText (value: obj) : string option =
        if not (isNullish value) && jsType value = "string" then
            Some(unbox<string> value)
        else
            None

    let private guaranteeOf (patch: obj) (fallback: string) : string option =
        match rawText (getField patch "guaranteeKind"), rawText (getField patch "guarantee") with
        | Some text, _ -> Some text
        | None, Some text -> Some text
        | None, None -> Some fallback

    let private scopeOf (patch: obj) (fallback: string) : string option =
        match rawText (getField patch "scope") with
        | Some text -> Some text
        | None -> Some fallback

    let private storedCertificate (state: InquiryState option) (node: NodeId) : ValueCertificate =
        state
        |> Option.bind (fun (current: InquiryState) -> Map.tryFind node current.Certificates)
        |> Option.defaultValue (Certificate.empty node)

    let private residualSourceOf (patch: obj) : obj =
        let primary = getField patch "residualValue"
        let secondary = getField patch "residual"

        if not (isNullish primary) then primary
        elif not (isNullish secondary) then secondary
        else getField patch "value"

    let private certificateBase (patch: obj) (slot: string) : CertificatePatchRequest =
        { Slot = slot
          Value = None
          Lower = None
          Upper = None
          Summary = None
          Constraints = None
          Posterior = None
          ResidualValue = None
          GuaranteeKind = None
          Level = None
          Error = None
          Assumptions = stringArrayOption patch "assumptions"
          Scope = scopeOf patch slot
          Witnesses = eventIdList patch "witnesses"
          Derivations = eventIdList patch "derivations" }

    let private certificateRequestOf (patch: obj) (slot: string) : CertificatePatchRequest =
        let baseOf = certificateBase patch slot

        match slot with
        | "exact" ->
            let value = getField patch "value"

            { baseOf with
                Value = (if isNullish value then None else Some value)
                GuaranteeKind = guaranteeOf patch "inclusion" }
        | "bound" ->
            { baseOf with
                Lower = optFloat patch "lower"
                Upper = optFloat patch "upper"
                GuaranteeKind = guaranteeOf patch "inclusion" }
        | "sample" ->
            let summary = getField patch "summary"

            { baseOf with
                Summary = (if isNullish summary then None else Some summary)
                GuaranteeKind = guaranteeOf patch "coverage"
                Level = optFloat patch "level"
                Error = optFloat patch "error" }
        | "ordinal" ->
            let constraints = getField patch "constraints"

            { baseOf with
                Constraints = (if isNullish constraints then None else Some constraints)
                GuaranteeKind = guaranteeOf patch "ordinal" }
        | "latent" ->
            let posterior = getField patch "posterior"

            { baseOf with
                Posterior = (if isNullish posterior then None else Some posterior)
                GuaranteeKind = guaranteeOf patch "coverage"
                Level = optFloat patch "level"
                Error = optFloat patch "error" }
        | "residual" ->
            { baseOf with
                ResidualValue = (asFloat (residualSourceOf patch) |> Result.toOption) }
        | "witness" -> baseOf
        | _ ->
            { baseOf with
                Assumptions = None
                Scope = None
                Witnesses = []
                Derivations = [] }

    let private applyCertificate
        (stored: ValueCertificate)
        (request: CertificatePatchRequest)
        : Result<CoreEventBody, CoreError> =
        Certificate.apply stored request
        |> Result.map (fun (certificate: ValueCertificate) ->
            CertificatePatched({ Certificate = certificate }: CertificatePatch))
        |> Result.mapError (fun fault ->
            ({ Code = fault.Code
               Message = fault.Message }
            : CoreError))

    let private decodeCertificate (state: InquiryState option) (raw: obj) : Result<CoreEventBody, CoreError> =
        let patch = getField raw "patch"

        if isNullish patch then
            fail "missing-patch" "patch is required"
        else
            fieldString patch "node"
            |> Result.bind (fun nodeText ->
                fieldString patch "slot"
                |> Result.bind (fun slot ->
                    NodeId.tryCreate nodeText
                    |> withCode "invalid-node"
                    |> Result.bind (fun (node: NodeId) ->
                        applyCertificate (storedCertificate state node) (certificateRequestOf patch slot))))

    let private decodeBudgetDebited (raw: obj) : Result<CoreEventBody, CoreError> =
        let debit = getField raw "debit"

        if isNullish debit then
            fail "missing-debit" "debit is required"
        else
            budgetValue debit |> Result.map BudgetDebited

    let private decodeAnswerCommitted (raw: obj) : Result<CoreEventBody, CoreError> =
        let answer = getField raw "answer"

        if isNullish answer then
            fail "missing-answer" "answer is required"
        else
            envelopeWith (emptySchema "sphinx.answer@1" "sphinx-answer-v1") answer
            |> Result.map AnswerCommitted

    let private decodePluginSetBound (raw: obj) : Result<CoreEventBody, CoreError> =
        let first = getField raw "lock"
        let second = getField raw "pluginLock"
        let chosen = if not (isNullish first) then first else second

        if isNullish chosen then
            fail "missing-lock" "plugin lock is required"
        else
            decodeLockEntries chosen |> Result.map PluginSetBound

    let private statusFailureReason (raw: obj) : string =
        match optString raw "reason", optString raw "error" with
        | Some text, _ -> text
        | None, Some text -> text
        | None, None -> ""

    let private decodeStatusChanged (raw: obj) : Result<CoreEventBody, CoreError> =
        fieldString raw "status"
        |> Result.bind (fun status ->
            match status with
            | "Active" -> Ok(InquiryStatusChanged Active)
            | "InputRequired" -> Ok(InquiryStatusChanged InquiryStatus.InputRequired)
            | "Cancelling" -> Ok(InquiryStatusChanged Cancelling)
            | "Completed" -> Ok(InquiryStatusChanged Completed)
            | "Cancelled" -> Ok(InquiryStatusChanged InquiryStatus.Cancelled)
            | "Suspended" -> Ok(InquiryStatusChanged(Suspended(optString raw "reason" |> Option.defaultValue "")))
            | _ when status.StartsWith("Suspended:", StringComparison.Ordinal) ->
                Ok(InquiryStatusChanged(Suspended(status.Substring("Suspended:".Length))))
            | _ when status.StartsWith("Failed:", StringComparison.Ordinal) ->
                Ok(InquiryStatusChanged(InquiryStatus.Failed(status.Substring("Failed:".Length))))
            | "Failed" -> Ok(InquiryStatusChanged(InquiryStatus.Failed(statusFailureReason raw)))
            | _ -> fail "invalid-status" ("unknown inquiry status " + status))

    let private validateRevision (position: int) (revision: int) : Result<unit, CoreError> =
        if revision <> position then
            fail "revision-conflict" "event revision must equal its position"
        else
            Ok()

    let private decodeParent (parentText: string) : Result<EventId option, CoreError> =
        if parentText = "none" then
            Ok None
        else
            EventId.tryCreate parentText |> withCode "invalid-parent" |> Result.map Some

    let private decodeEventBody
        (state: InquiryState option)
        (raw: obj)
        (eventType: string)
        (revision: int)
        : Result<CoreEventBody, CoreError> =
        match eventType with
        | "InquiryCreated" -> decodeInquiryCreated raw
        | "WorkPlanned" -> decodeWorkPlanned raw
        | "WorkTransitioned" -> decodeWorkTransitioned raw
        | "GraphPatched" -> decodeGraphPatched raw revision
        | "ObservationAccepted" -> decodeObservation raw
        | "CertificatePatched" -> decodeCertificate state raw
        | "BudgetDebited" -> decodeBudgetDebited raw
        | "AnswerCommitted" -> decodeAnswerCommitted raw
        | "PluginSetBound" -> decodePluginSetBound raw
        | "InquiryStatusChanged" -> decodeStatusChanged raw
        | _ -> fail "unknown-event-type" ("unknown event type " + eventType)

    let decodeEventAt (state: InquiryState option) (raw: obj) (position: int) : Result<InquiryEvent, CoreError> =
        if isNullish raw || jsType raw <> "object" then
            fail "invalid-event" "event must be an object"
        else
            fieldString raw "type"
            |> Result.bind (fun eventType ->
                fieldString raw "inquiry"
                |> Result.bind (fun inquiryText ->
                    fieldInt raw "revision"
                    |> Result.bind (fun revision ->
                        fieldString raw "parent"
                        |> Result.bind (fun parentText ->
                            validateRevision position revision
                            |> Result.bind (fun () ->
                                InquiryId.tryCreate inquiryText
                                |> withCode "invalid-inquiry"
                                |> Result.bind (fun (inquiry: InquiryId) ->
                                    decodeParent parentText
                                    |> Result.bind (fun parentId ->
                                        decodeEventBody state raw eventType revision
                                        |> Result.map (fun (decoded: CoreEventBody) ->
                                            ({ Id = EventId.create ("ev" + string revision)
                                               InquiryId = inquiry
                                               Revision = int64 revision
                                               Parent = parentId
                                               Body = decoded }
                                            : InquiryEvent)))))))))
