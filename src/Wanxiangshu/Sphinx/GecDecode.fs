namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Core
open Wanxiangshu.Sphinx.Runtime

module GecDecode =

    let private fail code message : Result<'value, CoreError> = Error { Code = code; Message = message }

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
            match asString found with
            | Ok text -> Ok text
            | Error _ -> fail ("invalid-" + name) (name + " must be a string")

    let private optString (raw: obj) (name: string) : string option =
        let found = getField raw name

        if isNullish found then
            None
        else
            match asString found with
            | Ok text when not (String.IsNullOrWhiteSpace text) -> Some text
            | _ -> None

    let private fieldInt (raw: obj) (name: string) : Result<int, CoreError> =
        let found = getField raw name

        if isNullish found then
            fail ("missing-" + name) (name + " is required")
        else
            match asFloat found with
            | Ok number -> Ok(int number)
            | Error _ -> fail ("invalid-" + name) (name + " must be a finite number")

    let private optFloat (raw: obj) (name: string) : float option =
        let found = getField raw name

        if isNullish found then
            None
        else
            match asFloat found with
            | Ok number -> Some number
            | Error _ -> None

    let private stringArrayValue (value: obj) : Result<string list, CoreError> =
        if not (isArray value) then
            fail "invalid-list" "expected string array"
        else
            let items = unbox<obj array> value |> Array.toList

            items
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun acc ->
                        match asString item with
                        | Ok text -> Ok(acc @ [ text ])
                        | Error _ -> fail "invalid-list" "array items must be strings"))
                (Ok [])

    let private optStringArray (raw: obj) (name: string) : Result<string list, CoreError> =
        let found = getField raw name

        if isNullish found then
            Ok []
        else
            stringArrayValue found

    let private budgetValue (value: obj) : Result<ResourceBudget, CoreError> =
        if isNullish value || jsType value <> "object" || isArray value then
            fail "invalid-budget" "budget must be an object"
        else
            keysOf value
            |> Array.toList
            |> List.fold
                (fun state key ->
                    state
                    |> Result.bind (fun acc ->
                        let amount = getField value key

                        match asFloat amount with
                        | Ok number -> Ok(Map.add key number acc)
                        | Error _ -> fail "invalid-budget" ("budget " + key + " must be finite")))
                (Ok Map.empty)

    let private schemaRefValue (value: obj) : Result<SchemaRef, CoreError> =
        if isNullish value || jsType value <> "object" then
            fail "invalid-schema" "schema must be an object"
        else
            let idField = getField value "id"
            let hashField = getField value "hash"

            let idText =
                if isNullish idField then
                    ""
                else
                    match asString idField with
                    | Ok text -> text
                    | Error _ -> ""

            let hashText =
                if isNullish hashField then
                    ""
                else
                    match asString hashField with
                    | Ok text -> text
                    | Error _ -> ""

            if jsType idField <> "string" && not (isNullish idField) then
                fail "invalid-schema" "schema id must be a string"
            elif jsType hashField <> "string" && not (isNullish hashField) then
                fail "invalid-schema" "schema hash must be a string"
            else
                Ok { Id = idText; Hash = hashText }

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
        if not (isNullish (getField raw "plugin")) then
            let plugin = getField raw "plugin"

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
                                    { Plugin = { Id = id; Release = release; AbiHash = abiHash }
                                      Capabilities = Set.ofList capabilities
                                      Dependencies = Set.ofList dependencies
                                      Schemas = schemas }))))))
        else
            decodeManifest raw |> Result.map lockEntryOfManifest

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
                    |> Result.bind (fun acc ->
                        lockEntryOfRaw item |> Result.map (fun entry -> acc @ [ entry ])))
                (Ok [])

    let private envelopeWith (schema: SchemaRef) (payload: obj) : Result<JsonEnvelope, CoreError> =
        let body = if isNullish payload then box {| |} else payload

        match JsonEnvelope.create schema body with
        | Ok envelope -> Ok envelope
        | Error message -> fail "invalid-envelope" message

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
                    |> Result.map (fun rootEnvelope -> InquiryCreated(rootEnvelope, lockEntries, budget))
                    |> fun result ->
                        match result with
                        | Ok body when String.IsNullOrWhiteSpace question ->
                            fail "missing-question" "question must not be blank" |> Result.bind (fun _ -> Ok body)
                        | _ -> result)))

    let private workIds (raw: obj) (name: string) : Result<WorkId list, CoreError> =
        optStringArray raw name
        |> Result.bind (fun names ->
            names
            |> List.fold
                (fun state name ->
                    state
                    |> Result.bind (fun acc ->
                        match WorkId.tryCreate name with
                        | Ok id -> Ok(acc @ [ id ])
                        | Error message -> fail "invalid-dependency" message))
                (Ok []))

    let private decodeWorkSpec (work: obj) : Result<WorkSpec, CoreError> =
        fieldString work "id"
        |> Result.bind (fun idText ->
            fieldString work "branch"
            |> Result.bind (fun branchText ->
                fieldInt work "attempt"
                |> Result.bind (fun attempt ->
                    workIds work "dependencies"
                    |> Result.bind (fun dependencies ->
                        match WorkId.tryCreate idText with
                        | Error message -> fail "invalid-work-id" message
                        | Ok id ->
                            match BranchId.tryCreate branchText with
                            | Error message -> fail "invalid-branch-id" message
                            | Ok branch ->
                                if attempt < 1 then
                                    fail "invalid-attempt" "work attempt must be positive"
                                else
                                    Ok
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
                                          Budget = Map.empty })))))

    let private decodeWorkPlanned (raw: obj) : Result<CoreEventBody, CoreError> =
        let work = getField raw "work"

        if isNullish work then
            fail "missing-work" "work is required"
        else
            decodeWorkSpec work |> Result.map (fun spec -> WorkPlanned [ spec ])

    let private wallClockFields : string list =
        [ "leaseExpiresAt"; "heartbeatTimeout"; "wallClock"; "expiresAt"; "timeoutMs" ]

    let private hasWallClock (raw: obj) (work: obj) : bool =
        wallClockFields
        |> List.exists (fun name ->
            (not (isNullish raw) && hasKey raw name)
            || (not (isNullish work) && hasKey work name))

    let private leaseProofOf (work: obj) (attempt: int) : LeaseProof =
        let fence =
            match optString work "fence" with
            | Some text -> text
            | None -> ""

        let session = optString work "session"
        { Attempt = attempt; Fence = fence; Session = session }

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
            Ok { Attempt = attempt; EventId = None; Detail = None }
        else
            envelopeWith (emptySchema schemaId schemaHash) chosen
            |> Result.map (fun detail -> { Attempt = attempt; EventId = None; Detail = Some detail })

    let private decodeWorkTransitioned (raw: obj) : Result<CoreEventBody, CoreError> =
        let work = getField raw "work"

        if isNullish work then
            fail "missing-work" "work is required"
        elif hasWallClock raw work then
            fail "wall-clock-field" "wall clock fields must never drive the lifecycle"
        else
            decodeWorkSpec work
            |> Result.bind (fun spec ->
                fieldString raw "from"
                |> Result.bind (fun fromState ->
                    fieldString raw "to"
                    |> Result.bind (fun toState ->
                        match toState with
                        | "Planned" -> Ok(WorkTransitioned(spec, fromState, Planned))
                        | "Ready" -> Ok(WorkTransitioned(spec, fromState, Ready))
                        | "Leased" ->
                            let proof = leaseProofOf work spec.Attempt
                            Ok(WorkTransitioned(spec, fromState, Leased proof))
                        | "Running" ->
                            let proof = leaseProofOf work spec.Attempt
                            Ok(WorkTransitioned(spec, fromState, Running proof))
                        | "InputRequired" ->
                            let proof = leaseProofOf work spec.Attempt
                            Ok(WorkTransitioned(spec, fromState, WorkState.InputRequired proof))
                        | "Succeeded" ->
                            completionProofOf raw spec.Attempt
                            |> Result.map (fun proof -> WorkTransitioned(spec, fromState, Succeeded proof))
                        | "Failed" ->
                            completionProofOf raw spec.Attempt
                            |> Result.map (fun proof -> WorkTransitioned(spec, fromState, WorkState.Failed proof))
                        | "Cancelled" ->
                            completionProofOf raw spec.Attempt
                            |> Result.map (fun proof -> WorkTransitioned(spec, fromState, WorkState.Cancelled proof))
                        | "Superseded" ->
                            let successor = getField raw "successor"

                            if isNullish successor then
                                fail "missing-successor" "superseded work requires a successor"
                            else
                                let successorText =
                                    if jsType successor = "string" then
                                        unbox<string> successor
                                    else
                                        let idField = getField successor "id"

                                        if not (isNullish idField) && jsType idField = "string" then
                                            unbox<string> idField
                                        else
                                            ""

                                match WorkId.tryCreate successorText with
                                | Ok successorId -> Ok(WorkTransitioned(spec, fromState, Superseded successorId))
                                | Error message -> fail "invalid-successor" message
                        | _ -> fail "illegal-transition" ("unknown work state " + toState))))

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
                        match NodeId.tryCreate targetText with
                        | Error message -> fail "invalid-node" message
                        | Ok target ->
                            if String.IsNullOrWhiteSpace kind then
                                fail "invalid-patch" "node kind must not be blank"
                            elif String.IsNullOrWhiteSpace relation then
                                fail "invalid-patch" "edge relation must not be blank"
                            else
                                let envelopeRaw = getField raw "envelope"

                                (if isNullish envelopeRaw then
                                     emptyEnvelope "sphinx.graph/empty@1" "sphinx-graph-empty-v1"
                                 else
                                     schemaRefValue (getField envelopeRaw "schema")
                                     |> Result.bind (fun schema ->
                                         let payload = getField envelopeRaw "payload"
                                         envelopeWith schema (if isNullish payload then box {| |} else payload)))
                                |> Result.map (fun payload ->
                                    let node =
                                        { Id = target
                                          Kind = kind
                                          Payload = payload
                                          Revision = int64 revision }

                                    let edgeId = EdgeId.create ("e" + string revision)

                                    let edge =
                                        { Id = edgeId
                                          Tails = Set.ofList [ target ]
                                          Heads = Set.ofList [ target ]
                                          Relation = relation
                                          Payload = None }

                                    GraphPatched
                                        { UpsertNodes = [ node ]
                                          RemoveNodes = []
                                          UpsertEdges = [ edge ]
                                          RemoveEdges = [] })))))

    let private bindingEnvelope
        (observation: obj)
        (name: string)
        (schemaId: string)
        (schemaHash: string)
        (required: bool)
        : Result<JsonEnvelope, CoreError> =
        let found = getField observation name

        if isNullish found then
            if required then
                fail ("missing-" + name) (name + " is required")
            else
                emptyEnvelope schemaId schemaHash
        else
            envelopeWith (emptySchema schemaId schemaHash) found

    let private decodeObservation (raw: obj) : Result<CoreEventBody, CoreError> =
        let observation = getField raw "observation"

        if isNullish observation then
            fail "missing-observation" "observation is required"
        else
            let required =
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

            let missing =
                required |> List.tryFind (fun name -> isNullish (getField observation name))

            match missing with
            | Some name -> fail ("missing-" + name) (name + " is required")
            | None ->
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
                                                            match BranchId.tryCreate branchText with
                                                            | Error message -> fail "invalid-branch-id" message
                                                            | Ok branch ->
                                                                match WorkId.tryCreate workText with
                                                                | Error message -> fail "invalid-work-id" message
                                                                | Ok work ->
                                                                    match BlindToken.tryCreate blindText with
                                                                    | Error message -> fail "invalid-blind-token" message
                                                                    | Ok blind ->
                                                                        if attempt < 1 then
                                                                            fail "invalid-attempt" "attempt must be positive"
                                                                        elif String.IsNullOrWhiteSpace rootSnapshot then
                                                                            fail "missing-rootSnapshotHash" "root snapshot must not be blank"
                                                                        elif String.IsNullOrWhiteSpace seed then
                                                                            fail "missing-seed" "seed must not be blank"
                                                                        else
                                                                            bindingEnvelope observation "wording" "sphinx.wording@1" "sphinx-wording-v1" true
                                                                            |> Result.bind (fun wording ->
                                                                                bindingEnvelope observation "permutation" "sphinx.permutation@1" "sphinx-permutation-v1" true
                                                                                |> Result.bind (fun permutation ->
                                                                                    bindingEnvelope observation "model" "sphinx.model@1" "sphinx-model-v1" true
                                                                                    |> Result.bind (fun model ->
                                                                                        bindingEnvelope observation "sampling" "sphinx.sampling@1" "sphinx-sampling-v1" true
                                                                                        |> Result.bind (fun sampling ->
                                                                                            bindingEnvelope observation "usage" "sphinx.usage@1" "sphinx-usage-v1" true
                                                                                            |> Result.bind (fun usage ->
                                                                                                bindingEnvelope observation "payload" "sphinx.observation/payload@1" "sphinx-observation-payload-v1" false
                                                                                                |> Result.map (fun payload ->
                                                                                                    ObservationAccepted
                                                                                                        { RootSnapshotHash = rootSnapshot
                                                                                                          BranchId = branch
                                                                                                          WorkId = work
                                                                                                          Attempt = attempt
                                                                                                          PluginLock = pluginLock
                                                                                                          Schema = schema
                                                                                                          PromptId = promptId
                                                                                                          QuestionId = questionId
                                                                                                          Wording = wording
                                                                                                          Permutation = permutation
                                                                                                          Treatment = treatment
                                                                                                          BlindToken = blind
                                                                                                          RandomSeed = seed
                                                                                                          Model = model
                                                                                                          Sampling = sampling
                                                                                                          Usage = usage
                                                                                                          Payload = payload }))))))))))))))))))

    let private stringArrayOption (raw: obj) (name: string) : string[] option =
        let found = getField raw name

        if isNullish found then
            None
        else
            match stringArrayValue found with
            | Ok items -> Some(List.toArray items)
            | Error _ -> None

    let private eventIdList (raw: obj) (name: string) : EventId list =
        let found = getField raw name

        if isNullish found || not (isArray found) then
            []
        else
            unbox<obj array> found
            |> Array.toList
            |> List.choose (fun item ->
                if jsType item = "string" then
                    match EventId.tryCreate (unbox<string> item) with
                    | Ok id -> Some id
                    | Error _ -> None
                else
                    None)

    let private decodeCertificate (state: InquiryState option) (raw: obj) : Result<CoreEventBody, CoreError> =
        let patch = getField raw "patch"

        if isNullish patch then
            fail "missing-patch" "patch is required"
        else
            fieldString patch "node"
            |> Result.bind (fun nodeText ->
                fieldString patch "slot"
                |> Result.bind (fun slot ->
                    match NodeId.tryCreate nodeText with
                    | Error message -> fail "invalid-node" message
                    | Ok node ->
                        let stored =
                            match state with
                            | Some current ->
                                match Map.tryFind node current.Certificates with
                                | Some certificate -> certificate
                                | None -> Certificate.empty node
                            | None -> Certificate.empty node

                        let guaranteeOf (fallback: string) : string option =
                            let direct = getField patch "guaranteeKind"

                            if not (isNullish direct) && jsType direct = "string" then
                                Some(unbox<string> direct)
                            else
                                let legacy = getField patch "guarantee"

                                if not (isNullish legacy) && jsType legacy = "string" then
                                    Some(unbox<string> legacy)
                                else
                                    Some fallback

                        let scopeOf (fallback: string) : string option =
                            let found = getField patch "scope"

                            if isNullish found || jsType found <> "string" then
                                Some fallback
                            else
                                Some(unbox<string> found)

                        let request : CertificatePatchRequest =
                            match slot with
                            | "exact" ->
                                { Slot = slot
                                  Value = (let v = getField patch "value" in if isNullish v then None else Some v)
                                  Lower = None
                                  Upper = None
                                  Summary = None
                                  Constraints = None
                                  Posterior = None
                                  ResidualValue = None
                                  GuaranteeKind = guaranteeOf "inclusion"
                                  Level = None
                                  Error = None
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "exact"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "bound" ->
                                { Slot = slot
                                  Value = None
                                  Lower = optFloat patch "lower"
                                  Upper = optFloat patch "upper"
                                  Summary = None
                                  Constraints = None
                                  Posterior = None
                                  ResidualValue = None
                                  GuaranteeKind = guaranteeOf "inclusion"
                                  Level = None
                                  Error = None
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "bound"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "sample" ->
                                let summary = getField patch "summary"

                                { Slot = slot
                                  Value = None
                                  Lower = None
                                  Upper = None
                                  Summary = (if isNullish summary then None else Some summary)
                                  Constraints = None
                                  Posterior = None
                                  ResidualValue = None
                                  GuaranteeKind = guaranteeOf "coverage"
                                  Level = optFloat patch "level"
                                  Error = optFloat patch "error"
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "sample"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "ordinal" ->
                                let constraints = getField patch "constraints"

                                { Slot = slot
                                  Value = None
                                  Lower = None
                                  Upper = None
                                  Summary = None
                                  Constraints = (if isNullish constraints then None else Some constraints)
                                  Posterior = None
                                  ResidualValue = None
                                  GuaranteeKind = guaranteeOf "ordinal"
                                  Level = None
                                  Error = None
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "ordinal"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "latent" ->
                                let posterior = getField patch "posterior"

                                { Slot = slot
                                  Value = None
                                  Lower = None
                                  Upper = None
                                  Summary = None
                                  Constraints = None
                                  Posterior = (if isNullish posterior then None else Some posterior)
                                  ResidualValue = None
                                  GuaranteeKind = guaranteeOf "coverage"
                                  Level = optFloat patch "level"
                                  Error = optFloat patch "error"
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "latent"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "residual" ->
                                let residualRaw =
                                    let primary = getField patch "residualValue"

                                    if not (isNullish primary) then
                                        primary
                                    else
                                        let secondary = getField patch "residual"

                                        if not (isNullish secondary) then
                                            secondary
                                        else
                                            getField patch "value"

                                { Slot = slot
                                  Value = None
                                  Lower = None
                                  Upper = None
                                  Summary = None
                                  Constraints = None
                                  Posterior = None
                                  ResidualValue =
                                    (if isNullish residualRaw then
                                         None
                                     else
                                         match asFloat residualRaw with
                                         | Ok number -> Some number
                                         | Error _ -> None)
                                  GuaranteeKind = None
                                  Level = None
                                  Error = None
                                  Assumptions = stringArrayOption patch "assumptions"
                                  Scope = scopeOf "residual"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | "witness" ->
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
                                  Scope = scopeOf "witness"
                                  Witnesses = eventIdList patch "witnesses"
                                  Derivations = eventIdList patch "derivations" }
                            | _ ->
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
                                  Assumptions = None
                                  Scope = None
                                  Witnesses = []
                                  Derivations = [] }

                        match Certificate.apply stored request with
                        | Ok certificate -> Ok(CertificatePatched { Certificate = certificate })
                        | Error fault -> fail fault.Code fault.Message))

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

    let private decodeStatusChanged (raw: obj) : Result<CoreEventBody, CoreError> =
        fieldString raw "status"
        |> Result.bind (fun status ->
            match status with
            | "Active" -> Ok(InquiryStatusChanged Active)
            | "InputRequired" -> Ok(InquiryStatusChanged InquiryStatus.InputRequired)
            | "Cancelling" -> Ok(InquiryStatusChanged Cancelling)
            | "Completed" -> Ok(InquiryStatusChanged Completed)
            | "Cancelled" -> Ok(InquiryStatusChanged InquiryStatus.Cancelled)
            | "Suspended" ->
                let reason =
                    match optString raw "reason" with
                    | Some text -> text
                    | None -> ""
                Ok(InquiryStatusChanged(Suspended reason))
            | _ when status.StartsWith("Suspended:", StringComparison.Ordinal) ->
                Ok(InquiryStatusChanged(Suspended(status.Substring("Suspended:".Length))))
            | _ when status.StartsWith("Failed:", StringComparison.Ordinal) ->
                Ok(InquiryStatusChanged(InquiryStatus.Failed(status.Substring("Failed:".Length))))
            | "Failed" ->
                let reason =
                    match optString raw "reason" with
                    | Some text -> text
                    | None ->
                        match optString raw "error" with
                        | Some text -> text
                        | None -> ""
                Ok(InquiryStatusChanged(InquiryStatus.Failed reason))
            | _ -> fail "invalid-status" ("unknown inquiry status " + status))

    let decodeEventAt
        (state: InquiryState option)
        (raw: obj)
        (position: int)
        : Result<InquiryEvent, CoreError> =
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
                            if revision <> position then
                                fail "revision-conflict" "event revision must equal its position"
                            else
                                match InquiryId.tryCreate inquiryText with
                                | Error message -> fail "invalid-inquiry" message
                                | Ok inquiry ->
                                    let parent =
                                        if parentText = "none" then
                                            Ok None
                                        else
                                            match EventId.tryCreate parentText with
                                            | Ok parentId -> Ok(Some parentId)
                                            | Error message -> fail "invalid-parent" message

                                    parent
                                    |> Result.bind (fun parentId ->
                                        let id = EventId.create ("ev" + string revision)

                                        let body =
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

                                        body
                                        |> Result.map (fun decoded ->
                                            { Id = id
                                              InquiryId = inquiry
                                              Revision = int64 revision
                                              Parent = parentId
                                              Body = decoded }))))))
