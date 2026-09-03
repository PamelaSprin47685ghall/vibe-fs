namespace Wanxiangshu.Sphinx

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Core
open Wanxiangshu.Sphinx.Runtime

module GecSurface =

    let private fail code message : Result<'value, CoreError> =
        Error { Code = code; Message = message }

    let private isNullish (value: obj) : bool = emitJsExpr value "$0 == null"

    let private isArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private getField (raw: obj) (name: string) : obj = emitJsExpr (raw, name) "$0[$1]"

    let private jsType (value: obj) : string = emitJsExpr value "typeof $0"

    let private isFiniteNumber (value: obj) : bool = emitJsExpr value "Number.isFinite($0)"

    let private errorObject (code: string) (message: string) : obj =
        box
            {| ok = false
               error = box {| code = code; message = message |} |}

    let private coreErrorObject (fault: CoreError) : obj = errorObject fault.Code fault.Message

    let private asStringValue (value: obj) : Result<string, string> =
        if jsType value = "string" then
            Ok(unbox<string> value)
        else
            Error "expected string"

    let private asFloatValue (value: obj) : Result<float, string> =
        if jsType value = "number" && isFiniteNumber value then
            Ok(unbox<float> value)
        else
            Error "expected finite number"

    let private validateId (input: obj) : obj =
        if isNullish input || jsType input <> "object" then
            errorObject "invalid-id" "id input must be an object"
        else
            let kindRaw = getField input "kind"
            let valueRaw = getField input "value"

            if isNullish kindRaw || jsType kindRaw <> "string" then
                errorObject "unknown-kind" "id kind is required"
            elif isNullish valueRaw || jsType valueRaw <> "string" then
                errorObject "invalid-id" "id value must be a string"
            else
                let kind = unbox<string> kindRaw
                let value = unbox<string> valueRaw

                let check (result: Result<'id, string>) : obj =
                    match result with
                    | Ok _ ->
                        box
                            {| ok = true
                               kind = kind
                               value = value |}
                    | Error message -> errorObject "invalid-id" message

                match kind with
                | "InquiryId" -> check (InquiryId.tryCreate value |> Result.map box)
                | "WorkId" -> check (WorkId.tryCreate value |> Result.map box)
                | "BranchId" -> check (BranchId.tryCreate value |> Result.map box)
                | "EventId" -> check (EventId.tryCreate value |> Result.map box)
                | "NodeId" -> check (NodeId.tryCreate value |> Result.map box)
                | "EdgeId" -> check (EdgeId.tryCreate value |> Result.map box)
                | "AttemptId" ->
                    if String.IsNullOrWhiteSpace value then
                        errorObject "invalid-id" "AttemptId must not be blank"
                    elif not (value.StartsWith("att", StringComparison.Ordinal)) then
                        errorObject "invalid-id" "AttemptId must start with att"
                    elif value |> Seq.exists Char.IsWhiteSpace then
                        errorObject "invalid-id" "AttemptId must not contain whitespace"
                    else
                        box
                            {| ok = true
                               kind = kind
                               value = value |}
                | "BlindToken" -> check (BlindToken.tryCreate value |> Result.map box)
                | _ -> errorObject "unknown-kind" ("unknown id kind " + kind)

    let private semanticHash (input: obj) : obj =
        if isNullish input || jsType input <> "object" then
            errorObject "missing-events" "events are required"
        else
            let events = getField input "events"

            if isNullish events || not (isArray events) then
                errorObject "missing-events" "events must be an array"
            else
                let hash = CoreHash.canonicalSha256 events
                box {| ok = true; hash = hash |}

    let private lockEntryList (state: InquiryState) : PluginLockEntry list =
        state.PluginLock |> Map.toList |> List.map snd

    let private checkObservationLock (state: InquiryState) (binding: ProtocolBinding) : Result<unit, CoreError> =
        match PluginRegistry.checkObservation (lockEntryList state) binding.PluginLock binding.Schema with
        | Ok _ -> Ok()
        | Error fault -> fail fault.Code fault.Message

    let private placeholderNode (target: NodeId) (revision: int64) : GraphNode =
        let payload =
            match
                JsonEnvelope.create
                    { Id = "sphinx.graph/empty@1"
                      Hash = "sphinx-graph-empty-v1" }
                    (box {| |})
            with
            | Ok envelope -> envelope
            | Error _ ->
                { Schema =
                    { Id = "sphinx.graph/empty@1"
                      Hash = "sphinx-graph-empty-v1" }
                  CanonicalPayload = "{}" }

        { Id = target
          Kind = "Unknown"
          Payload = payload
          Revision = revision }

    let private applyWithRetry (state: InquiryState option) (event: InquiryEvent) : Result<InquiryState, CoreError> =
        match Reducer.apply state event with
        | Ok next -> Ok next
        | Error fault when fault.Code = "unknown-node" ->
            match state, event.Body with
            | Some current, CertificatePatched patch ->
                let grown =
                    { current with
                        Graph =
                            Map.add
                                patch.Certificate.NodeId
                                (placeholderNode patch.Certificate.NodeId event.Revision)
                                current.Graph }

                Reducer.apply (Some grown) event
            | _ -> Error fault
        | Error fault -> Error fault

    let private foldRawEvents (raws: obj array) : Result<InquiryState, CoreError> =
        let rec loop (state: InquiryState option) (index: int) : Result<InquiryState, CoreError> =
            if index >= raws.Length then
                match state with
                | Some current -> Ok current
                | None -> fail "empty-history" "inquiry has no events"
            else
                GecDecode.decodeEventAt state raws.[index] index
                |> Result.bind (fun event ->
                    match event.Body with
                    | ObservationAccepted binding ->
                        match state with
                        | Some current ->
                            checkObservationLock current binding
                            |> Result.bind (fun _ -> applyWithRetry state event)
                        | None -> applyWithRetry state event
                    | _ -> applyWithRetry state event)
                |> Result.bind (fun next -> loop (Some next) (index + 1))

        loop None 0

    let private decodeIntMap (value: obj) : Map<int, float> =
        if isNullish value || jsType value <> "object" || isArray value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.fold
                (fun acc key ->
                    match Int32.TryParse key with
                    | true, number ->
                        let amount = getField value key

                        match asFloatValue amount with
                        | Ok scalar -> Map.add number scalar acc
                        | Error _ -> acc
                    | _ -> acc)
                Map.empty

    let private decodeIntList (value: obj) : int list =
        if isNullish value || not (isArray value) then
            []
        else
            unbox<obj array> value
            |> Array.toList
            |> List.choose (fun item ->
                match asFloatValue item with
                | Ok number -> Some(int number)
                | Error _ -> None)

    let private decodeEdges (value: obj) : (int * int) list =
        if isNullish value || not (isArray value) then
            []
        else
            unbox<obj array> value
            |> Array.toList
            |> List.choose (fun pair ->
                if isNullish pair || not (isArray pair) then
                    None
                else
                    let parts = unbox<obj array> pair |> Array.toList

                    match parts with
                    | [ left; right ] ->
                        match asFloatValue left, asFloatValue right with
                        | Ok a, Ok b -> Some(int a, int b)
                        | _ -> None
                    | _ -> None)

    let private boolField (raw: obj) (name: string) : bool =
        let found = getField raw name

        if isNullish found then false
        elif jsType found = "boolean" then unbox<bool> found
        else false

    let private floatField (raw: obj) (name: string) (fallback: float) : float =
        let found = getField raw name

        match asFloatValue found with
        | Ok number -> number
        | Error _ -> fallback

    let private intField (raw: obj) (name: string) (fallback: int) : int =
        let found = getField raw name

        match asFloatValue found with
        | Ok number -> int number
        | Error _ -> fallback

    let private decodeClosureDomain (raw: obj) : ClosureDomain option =
        if isNullish raw then
            None
        else
            let kindRaw = getField raw "kind"

            if isNullish kindRaw || jsType kindRaw <> "string" then
                None
            else
                match unbox<string> kindRaw with
                | "finite-dag" ->
                    let nodes = intField raw "nodes" 0
                    Some(FiniteDag(nodes, decodeEdges (getField raw "edges")))
                | "lattice" -> Some(FiniteChain(boolField raw "monotone", boolField raw "continuous"))
                | "metric" -> Some(MetricSpace(floatField raw "modulus" Double.NaN))
                | "none" -> Some NoDomain
                | _ -> Some NoDomain

    let private decodeClosureOperator (raw: obj) : ClosureOperator option =
        if isNullish raw then
            None
        else
            let kindRaw = getField raw "kind"

            if isNullish kindRaw || jsType kindRaw <> "string" then
                None
            else
                match unbox<string> kindRaw with
                | "dag-recurrence" ->
                    let order = decodeIntList (getField raw "order")
                    let seeds = decodeIntMap (getField raw "seeds")

                    let ruleRaw = getField raw "rule"

                    let rule =
                        if isNullish ruleRaw || jsType ruleRaw <> "string" then
                            ""
                        else
                            unbox<string> ruleRaw

                    Some(DagRecurrence(order, seeds, rule))
                | "finite-map" ->
                    let start = intField raw "start" 0
                    Some(FiniteMap(start, decodeIntList (getField raw "table")))
                | "affine" ->
                    Some(
                        AffineMap(
                            floatField raw "factor" Double.NaN,
                            floatField raw "offset" Double.NaN,
                            floatField raw "start" Double.NaN
                        )
                    )
                | "none" -> Some NoOperator
                | _ -> Some NoOperator

    let private decodeClosureAsync (raw: obj) : AsyncExpectation option =
        if isNullish raw || jsType raw <> "object" then
            None
        else
            Some
                { FiniteDecisionSet = boolField raw "finiteDecisionSet"
                  StrictGap = boolField raw "strictGap"
                  VanishingUncertainty = boolField raw "vanishingUncertainty"
                  FairScheduling = boolField raw "fairScheduling"
                  OrderAware = boolField raw "orderAware"
                  CorrectSpecification = None }

    let private fixedPointView (point: FixedPoint) : obj =
        match point with
        | DagPoint values ->
            values
            |> Map.toList
            |> List.map (fun (key, value) -> string key, box value)
            |> createObj
        | ScalarPoint scalar -> box scalar
        | NoPoint -> Option.toObj (None: obj option)

    let private replay (input: obj) : obj =
        if isNullish input || jsType input <> "object" then
            errorObject "missing-events" "events are required"
        else
            let events = getField input "events"

            if isNullish events || not (isArray events) then
                errorObject "missing-events" "events must be an array"
            else
                let raws = unbox<obj array> events
                let stateHash = CoreHash.canonicalSha256 events

                match foldRawEvents raws with
                | Error fault -> coreErrorObject fault
                | Ok state ->
                    let view = Reducer.semanticView state
                    let closureRaw = getField input "closure"

                    if isNullish closureRaw then
                        box
                            {| ok = true
                               state = view
                               stateHash = stateHash |}
                    else
                        let maxIterations = intField closureRaw "maxIterations" 0

                        let request: ClosureRequest =
                            { Domain = decodeClosureDomain (getField closureRaw "domain")
                              Operator = decodeClosureOperator (getField closureRaw "operator")
                              MaxIterations = maxIterations
                              Async = decodeClosureAsync (getField closureRaw "async") }

                        let outcome = Agenda.evaluateClosure request

                        box
                            {| ok = true
                               state = view
                               stateHash = stateHash
                               converged = outcome.Converged
                               fixedPoint = fixedPointView outcome.Point
                               unique = outcome.Unique
                               iterations = outcome.Iterations
                               residual = box {| bound = outcome.ResidualBound |} |}

    let private lockView (entry: PluginLockEntry) : obj =
        let schemas =
            entry.Schemas
            |> Map.toList
            |> List.map (fun (name, schema) -> name, box {| id = schema.Id; hash = schema.Hash |})
            |> createObj

        box
            {| id = entry.Plugin.Id
               release = entry.Plugin.Release
               abiHash = entry.Plugin.AbiHash
               capabilities = entry.Capabilities |> Set.toList |> List.sort |> List.toArray
               dependencies = entry.Dependencies |> Set.toList |> List.sort |> List.toArray
               schemas = schemas |}

    let private decodeManifestList (raw: obj) : Result<PluginManifest list, CoreError> =
        if isNullish raw || not (isArray raw) then
            fail "invalid-manifest" "manifests must be an array"
        else
            unbox<obj array> raw
            |> Array.toList
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun acc ->
                        GecDecode.decodeManifest item |> Result.map (fun manifest -> acc @ [ manifest ])))
                (Ok [])

    let private bindPlugins (input: obj) : obj =
        if isNullish input || jsType input <> "object" then
            errorObject "invalid-manifest" "manifests are required"
        else
            let manifestsRaw = getField input "manifests"

            match decodeManifestList manifestsRaw with
            | Error fault -> coreErrorObject fault
            | Ok manifests ->
                match PluginRegistry.bind manifests with
                | Error fault -> errorObject fault.Code fault.Message
                | Ok bound ->
                    let existingRaw = getField input "existingLock"

                    if isNullish existingRaw then
                        box
                            {| ok = true
                               lock = bound |> List.map lockView |> List.toArray |}
                    else
                        match GecDecode.decodeLockEntries existingRaw with
                        | Error fault -> coreErrorObject fault
                        | Ok existing ->
                            match PluginRegistry.compatible existing bound with
                            | Ok _ ->
                                box
                                    {| ok = true
                                       lock = bound |> List.map lockView |> List.toArray |}
                            | Error fault -> errorObject fault.Code fault.Message

    let private decodeBudgetMap (value: obj) : Result<Map<string, float>, CoreError> =
        if isNullish value then
            Ok Map.empty
        elif jsType value <> "object" || isArray value then
            fail "invalid-budget" "budget must be an object"
        else
            keysOf value
            |> Array.toList
            |> List.fold
                (fun state key ->
                    state
                    |> Result.bind (fun acc ->
                        match asFloatValue (getField value key) with
                        | Ok number -> Ok(Map.add key number acc)
                        | Error _ -> fail "invalid-budget" ("budget " + key + " must be finite")))
                (Ok Map.empty)

    let private decodeTargets (raw: obj) : Result<RefinementTarget list, CoreError> =
        if isNullish raw || not (isArray raw) then
            fail "invalid-target" "targets must be an array"
        else
            unbox<obj array> raw
            |> Array.toList
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun acc ->
                        if isNullish item || jsType item <> "object" then
                            fail "invalid-target" "target must be an object"
                        else
                            let idRaw = getField item "id"

                            (match asStringValue idRaw with
                             | Ok text when not (String.IsNullOrWhiteSpace text) -> Ok text
                             | _ -> fail "invalid-target" "target id must not be blank")
                            |> Result.bind (fun id ->
                                let dependenciesRaw = getField item "dependencies"
                                let conflictRaw = getField item "conflictKeys"

                                let dependencies =
                                    if isNullish dependenciesRaw then
                                        Ok Set.empty
                                    elif not (isArray dependenciesRaw) then
                                        fail "invalid-target" "dependencies must be an array"
                                    else
                                        unbox<obj array> dependenciesRaw
                                        |> Array.toList
                                        |> List.fold
                                            (fun inner rawId ->
                                                inner
                                                |> Result.bind (fun collected ->
                                                    match asStringValue rawId with
                                                    | Ok text when not (String.IsNullOrWhiteSpace text) ->
                                                        Ok(Set.add text collected)
                                                    | _ -> fail "invalid-target" "dependency must not be blank"))
                                            (Ok Set.empty)

                                dependencies
                                |> Result.bind (fun dependencySet ->
                                    let conflicts =
                                        if isNullish conflictRaw then
                                            Ok Set.empty
                                        elif not (isArray conflictRaw) then
                                            fail "invalid-target" "conflict keys must be an array"
                                        else
                                            unbox<obj array> conflictRaw
                                            |> Array.toList
                                            |> List.fold
                                                (fun inner rawKey ->
                                                    inner
                                                    |> Result.bind (fun collected ->
                                                        match asStringValue rawKey with
                                                        | Ok text -> Ok(Set.add text collected)
                                                        | Error _ ->
                                                            fail "invalid-target" "conflict key must be a string"))
                                                (Ok Set.empty)

                                    conflicts
                                    |> Result.bind (fun conflictSet ->
                                        decodeBudgetMap (getField item "cost")
                                        |> Result.bind (fun cost ->
                                            let lossRaw = getField item "loss"

                                            let lossCurrency, lossValue =
                                                if isNullish lossRaw || jsType lossRaw <> "object" then
                                                    None, None
                                                else
                                                    let currencyRaw = getField lossRaw "currency"
                                                    let valueRaw = getField lossRaw "value"

                                                    let currency =
                                                        match asStringValue currencyRaw with
                                                        | Ok text when not (String.IsNullOrWhiteSpace text) ->
                                                            Some text
                                                        | _ -> None

                                                    let loss =
                                                        match asFloatValue valueRaw with
                                                        | Ok number -> Some number
                                                        | Error _ -> None

                                                    match currency, loss with
                                                    | Some _, Some _ -> currency, loss
                                                    | _ -> None, None

                                            let commonRaw = getField item "commonCurrency"

                                            let common =
                                                if isNullish commonRaw then
                                                    None
                                                else
                                                    match asStringValue commonRaw with
                                                    | Ok text when not (String.IsNullOrWhiteSpace text) ->
                                                        Some text
                                                    | _ -> None

                                            Ok(
                                                acc
                                                @ [ { Id = id
                                                      Dependencies = dependencySet
                                                      ConflictKeys = conflictSet
                                                      Cost = cost
                                                      LossCurrency = lossCurrency
                                                      LossValue = lossValue
                                                      CommonCurrency = common
                                                      EffectSlot = None } ]
                                            )))))))
                (Ok [])

    let private decodeCompleted (raw: obj) : Result<Set<string>, CoreError> =
        if isNullish raw then
            Ok Set.empty
        elif not (isArray raw) then
            fail "invalid-target" "completed must be an array"
        else
            unbox<obj array> raw
            |> Array.toList
            |> List.fold
                (fun state item ->
                    state
                    |> Result.bind (fun acc ->
                        match asStringValue item with
                        | Ok text -> Ok(Set.add text acc)
                        | Error _ -> fail "invalid-target" "completed entries must be strings"))
                (Ok Set.empty)

    let private schedule (input: obj) : obj =
        if isNullish input || jsType input <> "object" then
            errorObject "invalid-target" "targets are required"
        else
            match decodeTargets (getField input "targets") with
            | Error fault -> coreErrorObject fault
            | Ok targets ->
                match decodeBudgetMap (getField input "budget") with
                | Error fault -> coreErrorObject fault
                | Ok budget ->
                    match decodeCompleted (getField input "completed") with
                    | Error fault -> coreErrorObject fault
                    | Ok completed ->
                        let request: ScheduleRequest =
                            { Targets = targets
                              Budget = budget
                              Completed = completed }

                        match Agenda.schedule request with
                        | Error fault -> errorObject fault.Code fault.Message
                        | Ok result ->
                            box
                                {| ok = true
                                   batch = result.Batch |> List.toArray
                                   pareto = result.Pareto |> List.toArray
                                   order = result.Order |> List.toArray |}

    let private ownMethods: (string * obj) list =
        [ "validateId", box validateId
          "semanticHash", box semanticHash
          "replay", box replay
          "bindPlugins", box bindPlugins
          "schedule", box schedule ]

    let methods: (string * obj) list = ownMethods

    let gecSurface: obj =
        createObj (
            ownMethods
            @ GecRefine.methods
            @ GecElicit.methods
            @ GecHost.methods
            @ GecLegacy.methods
            @ GecStore.methods
        )
