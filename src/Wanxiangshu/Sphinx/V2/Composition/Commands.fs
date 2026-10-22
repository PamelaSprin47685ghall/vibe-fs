namespace Wanxiangshu.Sphinx.V2.Composition

open System
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core
open Wanxiangshu.Sphinx.V2.Hosts
open Wanxiangshu.Sphinx.V2.Persistence
open Wanxiangshu.Sphinx.V2.Runtime
open Wanxiangshu.Sphinx.V2.Wire

type internal AuthorizedStartConfiguration =
    { CommandNamespace: string
      CreatedBy: string
      Profile: DefaultProfile
      ResourceSpecs: ResourceSpec list
      RenderReserve: Map<string, float> }

[<Sealed>]
type AppendCutUnknownIncident
    internal
    (
        inquiryId: InquiryId,
        commandId: string,
        eventId: Wanxiangshu.Foundation.Identity.EventId,
        evidence: AppendCommitUnknownEvidence
    ) =
    member _.InquiryId = inquiryId
    member _.CommandId = commandId
    member _.EventId = eventId
    member _.Evidence = evidence

[<Sealed>]
type RuntimeHandle
    internal
    (
        store: EventStoreHandle,
        configuration: AuthorizedStartConfiguration option,
        onAppendCutUnknown: AppendCutUnknownIncident -> unit
    ) =
    let delivered =
        System.Collections.Generic.HashSet<Wanxiangshu.Foundation.Identity.EventId>()

    let deliverCutUnknown inquiryId commandId eventId evidence =
        if not (delivered.Add eventId) then
            invalidOp "Sphinx append-cut incident was already delivered"

        onAppendCutUnknown (AppendCutUnknownIncident(inquiryId, commandId, eventId, evidence))

    member internal _.Store = store.Store
    member internal _.Configuration = configuration
    member internal _.Dispose() = store.Dispose()

    member internal _.SettleAppendCutUnknown(inquiryId, commandId, eventId, failure) =
        match failure with
        | AppendError.CommitUnknown evidence when not (List.isEmpty evidence.Prepared.Cuts) ->
            deliverCutUnknown inquiryId commandId eventId evidence
        | _ -> ()

[<RequireQualifiedAccess>]
module Commands =
    let private refusal code path message : ToolRefusal =
        { Code = code
          Path = path
          Message = message }

    let private native fields = createObj fields
    let private strings values = values |> List.toArray |> box

    let private digest value =
        CanonicalJson.canonicalJson value |> HostDigest.sha256Hex

    // The two schema documents the Runtime itself binds. A caller never hand-computes
    // these hashes: the prompt and the answer schema are internal facts, and their
    // references are derived from the canonical document so two documents always differ.
    let private promptSchemaDocument =
        "{\"additionalProperties\":false,\"properties\":{\"goal\":{\"type\":\"string\"},\"constraints\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},\"step\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"title\":{\"type\":\"string\"}},\"required\":[\"id\",\"title\"]}},\"required\":[\"goal\",\"step\"],\"type\":\"object\"}"

    let private answerSchemaDocument =
        "{\"additionalProperties\":false,\"properties\":{\"answer\":{\"type\":\"string\"}},\"required\":[\"answer\"],\"type\":\"object\"}"

    let private promptSchema =
        { Id = "sphinx.prompt@1"
          Hash = HostDigest.sha256Hex promptSchemaDocument }

    let private answerSchema =
        { Id = "sphinx.answer@1"
          Hash = HostDigest.sha256Hex answerSchemaDocument }

    /// A plan step becomes one work item. Dependencies are the steps the user named; the
    /// Runtime resolves their ids and refuses an unknown reference or a cycle before any
    /// durable byte is written.
    let private planSpecs (steps: PlanStep list) (goalText: string) (constraints: string list) : WorkSpec list =
        steps
        |> List.map (fun step ->
            let prompt =
                CanonicalJson.canonicalJson (
                    box
                        {| goal = goalText
                           constraints = constraints
                           step = {| id = step.Id; title = step.Title |} |}
                )

            { Id = WorkId.create step.Id
              Attempt = Attempt.first
              Fence = Fence.create (step.Id + ":" + string (Attempt.value Attempt.first))
              RoundId = None
              PlanId = PlanId.create ("plan-" + step.Id)
              Producer = "sphinx.runtime"
              Capability = "answer"
              Input = Some(JsonEnvelope.ofCanonical promptSchema prompt)
              OutputSchema = Some answerSchema
              Dependencies = step.DependsOn |> List.map WorkId.create |> Set.ofList
              ConflictKeys = Set.empty
              PhysicalRef = None
              Reserved = Map.empty })

    /// Shape and resource checks only: ids resolve, dependencies exist, the graph is
    /// acyclic and every step is named once. No step is re-ranked and no content veto.
    let private validatePlan (steps: PlanStep list) : Result<unit, ToolRefusal> =
        let ids = steps |> List.map (fun step -> step.Id)

        let uniqueIds =
            ids |> Set.ofList |> Set.count = List.length ids

        let declared = ids |> Set.ofList

        let unknownDependency =
            steps
            |> List.tryPick (fun step ->
                step.DependsOn
                |> List.tryFind (fun dependency -> not (Set.contains dependency declared))
                |> Option.map (fun dependency -> step.Id, dependency))

        let selfDependency =
            steps
            |> List.tryFind (fun step -> List.contains step.Id step.DependsOn)
            |> Option.map (fun step -> step.Id, step.Id)

        let hasCycle () =
            let rec reaches (fromId: string) (visited: Set<string>) : bool =
                if Set.contains fromId visited then
                    true
                else
                    let visited = Set.add fromId visited

                    steps
                    |> List.tryFind (fun step -> step.Id = fromId)
                    |> Option.map (fun step ->
                        step.DependsOn
                        |> List.exists (fun dependency -> reaches dependency visited))
                    |> Option.defaultValue false

            steps |> List.exists (fun step -> reaches step.Id Set.empty)

        match uniqueIds, unknownDependency, selfDependency with
        | false, _, _ -> Error(refusal "PLAN_SHAPE_INVALID" "steps" "plan step ids must be unique")
        | _, Some(stepId, dependency), _ ->
            Error(
                refusal
                    "PLAN_SHAPE_INVALID"
                    "steps"
                    (sprintf "step %s depends on unknown step %s" stepId dependency)
            )
        | _, _, Some(stepId, _) ->
            Error(refusal "PLAN_SHAPE_INVALID" "steps" (sprintf "step %s depends on itself" stepId))
        | _, _, _ when hasCycle () ->
            Error(refusal "PLAN_SHAPE_INVALID" "steps" "plan dependencies form a cycle")
        | _, _, _ -> Ok()

    /// The last step in the plan is the one whose result is the answer.
    let private answerWorkId (plan: WorkId list) : WorkId option =
        plan |> List.rev |> List.tryHead

    /// The step the Runtime is about to dispatch, if any, in the plain shape a worker
    /// reads back from `status`. The fence is the holder's proof; the prompt is the
    /// exact payload that will be dispatched.
    let private nextStepView (state: InquiryState) : obj =
        /// The step the worker currently holds: the first dispatched, not-yet-terminal
        /// step in the user's plan order. A step that is only planned or ready is the
        /// fallback, so a freshly created inquiry still reports its first executable
        /// step.
        let nextWorkItem =
            let ordered =
                state.Plan
                |> List.map (fun id -> state.Work |> Map.tryFind id)
                |> List.choose id

            let held =
                ordered
                |> List.tryFind (fun item ->
                    match item.State with
                    | WorkState.Leased _
                    | WorkState.Running _
                    | WorkState.InputRequired _ -> true
                    | _ -> false)

            held |> Option.orElseWith (fun () -> InquiryState.readyWork state |> List.tryHead)

        match nextWorkItem with
        | None -> null
        | Some item ->
            native
                [ "workId", box (WorkId.value item.Spec.Id)
                  "attempt", box (string (Attempt.value item.Spec.Attempt))
                  "fence", box (Fence.value item.Spec.Fence)
                  "title",
                  box (
                      item.Spec.Input
                      |> Option.map (fun envelope ->
                          let parsed = JS.JSON.parse envelope.CanonicalPayload
                          let step = parsed?step
                          step?title)
                      |> Option.toObj
                  )
                  "prompt", box (item.Spec.Input |> Option.map (fun envelope -> envelope.CanonicalPayload) |> Option.toObj) ]

    /// The committed answer text, if the inquiry has one. The text is the accepted
    /// worker result, never a new computation performed during a read.
    let private answerView (state: InquiryState) : obj =
        state.Answer
        |> Option.bind (fun answer -> state.Observations |> Map.tryFind (ObservationId.value answer.ResultObservationId))
        |> Option.map (fun observation -> box observation.CanonicalResult)
        |> Option.toObj

    let private amount =
        Decode.float
        |> Decode.andThen (fun value ->
            if Double.IsNaN value || Double.IsInfinity value || value < 0.0 then
                Decode.fail "resource amounts must be finite and nonnegative"
            else
                Decode.succeed value)

    let private resourceKind =
        BodyDto.exact
            [ "case"; "payload" ]
            (Decode.object (fun get ->
                get.Required.Field "case" Decode.string, get.Required.Field "payload" BodyDto.nonBlank))
        |> Decode.andThen (function
            | "consumed", name -> Decode.succeed (ResourceKind.Consumed name)
            | "capacity", name -> Decode.succeed (ResourceKind.Capacity name)
            | _ -> Decode.fail "resource kind must be consumed or capacity")

    let private resourceSpec =
        BodyDto.exact
            [ "name"; "kind"; "authorizedLimit" ]
            (Decode.object (fun get ->
                { Name = get.Required.Field "name" BodyDto.nonBlank
                  Kind = get.Required.Field "kind" resourceKind
                  AuthorizedLimit = get.Required.Field "authorizedLimit" amount }))

    let private executionMode =
        Decode.string
        |> Decode.andThen (function
            | "delegated" -> Decode.succeed ExecutionMode.Delegated
            | "independent" -> Decode.succeed ExecutionMode.Independent
            | _ -> Decode.fail "executionMode must be delegated or independent")

    let private configurationDecoder =
        BodyDto.exact
            [ "commandNamespace"
              "createdBy"
              "profileRef"
              "executionMode"
              "resourceSpecs"
              "renderReserve" ]
            (Decode.object (fun get ->
                let mode = get.Required.Field "executionMode" executionMode
                let profileRef = get.Required.Field "profileRef" BodyDto.nonBlank

                { CommandNamespace = get.Required.Field "commandNamespace" BodyDto.nonBlank
                  CreatedBy = get.Required.Field "createdBy" BodyDto.nonBlank
                  Profile =
                    { Profile.defaultProfile with
                        ProfileRef = profileRef
                        ExecutionMode = mode }
                  ResourceSpecs = get.Required.Field "resourceSpecs" (Decode.list resourceSpec)
                  RenderReserve = get.Required.Field "renderReserve" (Decode.keyValuePairs amount) |> Map.ofList }))

    let private validateConfiguration configuration =
        if configuration.Profile.ProfileRef <> Profile.defaultProfile.ProfileRef then
            Error "profileRef is not a registered startup profile"
        else
            Profile.validate configuration.Profile
            |> Result.mapError (fun fault -> fault.Message)
            |> Result.bind (fun _ ->
                Budget.validateSpecs configuration.ResourceSpecs
                |> Result.mapError (fun fault -> fault.Message))
            |> Result.bind (fun () ->
                let limits =
                    configuration.ResourceSpecs
                    |> List.map (fun spec -> spec.Name, spec.AuthorizedLimit)
                    |> Map.ofList

                match
                    configuration.RenderReserve
                    |> Map.toList
                    |> List.tryFind (fun (name, value) ->
                        Map.tryFind name limits |> Option.forall (fun limit -> value > limit))
                with
                | Some _ -> Error "renderReserve must name authorized resources and remain within their limits"
                | None -> Ok configuration)

    let private decodeConfiguration value =
        try
            Decode.fromString configurationDecoder (CanonicalJson.canonicalJson value)
            |> Result.bind validateConfiguration
        with error ->
            Error error.Message

    let create
        (store: IEventStore)
        (raw: obj option)
        (onAppendCutUnknown: AppendCutUnknownIncident -> unit)
        : Result<RuntimeHandle, ToolRefusal> =
        let decoded =
            match raw with
            | None -> Ok None
            | Some value -> decodeConfiguration value |> Result.map Some

        decoded
        |> Result.mapError (refusal "INVALID_START_CONFIGURATION" "configuration")
        |> Result.map (fun configuration ->
            RuntimeHandle(EventStoreHandle.Create store, configuration, onAppendCutUnknown))

    let store (handle: RuntimeHandle) = handle.Store
    let dispose (handle: RuntimeHandle) = handle.Dispose()

    let settleAppendCutUnknown (handle: RuntimeHandle) inquiryId commandId eventId failure =
        handle.SettleAppendCutUnknown(inquiryId, commandId, eventId, failure)

    let private configurationView configuration =
        let profile = configuration.Profile

        native
            [ "commandNamespace", box configuration.CommandNamespace
              "createdBy", box configuration.CreatedBy
              "profile",
              native
                  [ "profileRef", box profile.ProfileRef
                    "metaDepth", box profile.MetaDepth
                    "maxActivePlanCards", box profile.MaxActivePlanCards
                    "fitMaxIterations", box profile.FitMaxIterations
                    "fitGradientTolerance", box profile.FitGradientTolerance
                    "thetaL2", box profile.ThetaL2
                    "orderL2", box profile.OrderL2
                    "tieKappaPriorMean", box profile.TieKappaPriorMean
                    "tieKappaPriorVariance", box profile.TieKappaPriorVariance
                    "maxWorkAttempts", box profile.MaxWorkAttempts
                    "maxPureStepsPerAdvance", box profile.MaxPureStepsPerAdvance
                    "executionMode",
                    box (
                        match profile.ExecutionMode with
                        | ExecutionMode.Delegated -> "delegated"
                        | ExecutionMode.Independent -> "independent"
                    )
                    "questionTemplates", strings profile.QuestionTemplates
                    "probeIds", strings profile.ProbeIds ]
              "resourceSpecs",
              Encode.list configuration.ResourceSpecs (fun spec ->
                  let kind, name =
                      match spec.Kind with
                      | ResourceKind.Consumed name -> "consumed", name
                      | ResourceKind.Capacity name -> "capacity", name

                  native
                      [ "name", box spec.Name
                        "kind", native [ "case", box kind; "payload", box name ]
                        "authorizedLimit", box spec.AuthorizedLimit ])
              "renderReserve",
              Encode.list (Map.toList configuration.RenderReserve) (fun (name, value) ->
                  native [ "key", box name; "value", box value ]) ]

    let private currentRefusal fault =
        match fault with
        | CurrentError.DomainConflict _ ->
            refusal "DOMAIN_CONFLICT" "inquiryId" "inquiry has multiple legitimate durable heads"
        | CurrentError.SemanticRejected reason -> refusal "PERSISTENCE_SEMANTIC_CUT" "inquiryId" reason

    let private unknown inquiryId =
        refusal "UNKNOWN_INQUIRY" "inquiryId" (sprintf "inquiry %s is not in the durable record" inquiryId)

    let private refresh (store: IEventStore) =
        store.ReloadLocal()
        |> Result.mapError (refusal "PERSISTENCE_READ_FAILED" "inquiryId")

    let private find (store: IEventStore) inquiryId =
        refresh store
        |> Result.bind (fun () -> Bind.tryInquiry store inquiryId |> Result.mapError currentRefusal)

    let private read (store: IEventStore) inquiryId =
        find store inquiryId
        |> Result.bind (function
            | Some state -> Ok state
            | None -> Error(unknown (InquiryId.value inquiryId)))

    let private advance state =
        let outcome, detail =
            match Driver.classify state with
            | AdvanceOutcome.AwaitingResults count -> "awaiting-results", string count
            | AdvanceOutcome.InputRequired authorization -> "input-required", authorization
            | AdvanceOutcome.NoRunnalbeWork reason -> "no-runnable-work", reason
            | AdvanceOutcome.Terminal status -> "terminal", status
            | AdvanceOutcome.RefinementPending remaining -> "refinement-pending", string remaining

        native [ "outcome", box outcome; "detail", box detail ]

    let status (handle: RuntimeHandle) (args: StatusArgs) : Result<obj, ToolRefusal> =
        read handle.Store (InquiryId.create args.InquiryId)
        |> Result.map (fun state ->
            native
                [ "apiVersion", box Contract.apiVersion
                  "outcome", box "read"
                  "inquiryId", box args.InquiryId
                  "status", box (Encode.statusOf state)
                  "revision", box (Encode.revision state.Revision)
                  "advance", advance state
                  "nextStep", nextStepView state
                  "answer", answerView state
                  "inquiry", Encode.semanticView state ])

    let private requestView (args: StartArgs) =
        native
            [ "commandId", box args.CommandId
              "goalText", box args.GoalText
              "constraints", strings args.Constraints
              "materialRefs", strings args.MaterialRefs
              "authorizationRef", box args.AuthorizationRef
              "profileRef", box args.ProfileRef
              "steps",
              Encode.list args.Steps (fun step ->
                  native
                      [ "id", box step.Id
                        "title", box step.Title
                        "dependsOn", strings step.DependsOn ]) ]

    let private refusalView (fault: ToolRefusal) =
        native [ "code", box fault.Code; "path", box fault.Path; "message", box fault.Message ]

    let private receiptCurrentFields inquiryId current =
        match current with
        | Ok(Some state) -> [ "status", box (Encode.statusOf state); "advance", advance state ]
        | Error fault -> [ "currentRefusal", currentRefusal fault |> refusalView ]
        | Ok None -> [ "currentRefusal", unknown (InquiryId.value inquiryId) |> refusalView ]

    let private receiptPayload (store: IEventStore) outcome inquiryId (receipt: CommandReceipt) =
        native (
            [ "apiVersion", box Contract.apiVersion
              "outcome", box outcome
              "inquiryId", box (InquiryId.value inquiryId)
              "revision", box (Encode.revision receipt.Revision)
              "eventId", box (EventId.value receipt.EventId) ]
            @ receiptCurrentFields inquiryId (Bind.tryInquiry store inquiryId)
        )

    let private terminalToolRefusal handle inquiryId commandId eventId fault =
        settleAppendCutUnknown handle inquiryId commandId eventId fault

        let settlement code =
            let message =
                sprintf
                    "inquiry %s; command %s; event %s: %s. Reconcile the durable record before any retry."
                    (InquiryId.value inquiryId)
                    commandId
                    (Wanxiangshu.Foundation.Identity.EventId.value eventId)
                    (AppendError.describe fault)

            refusal code "inquiryId" message

        match fault with
        | AppendError.StorageInvalid invalid ->
            refusal "PERSISTENCE_REJECTED" "inquiryId" (sprintf "the store refused invalid input: %A" invalid)
        | AppendError.SemanticCut cut -> refusal "PERSISTENCE_REJECTED" "inquiryId" cut.Reason
        | AppendError.AppendFailed reason -> refusal "PERSISTENCE_REJECTED" "inquiryId" reason
        | AppendError.AppendNotAttempted _ -> settlement "PERSISTENCE_NOT_ATTEMPTED"
        | AppendError.CommitUnknown _ -> settlement "COMMIT_UNKNOWN"
        | AppendError.NoNewWriteReleaseFailed _ -> settlement "RELEASE_FAILED"

    let private resolveSteps (args: StartArgs) : Result<PlanStep list, ToolRefusal> =
        match args.Steps with
        | [] -> Ok []
        | steps -> Ok steps

    /// One transition batch is the unit of durability (WHAT[sphinx-v2-019]): the
    /// creation, the plan, and the dispatch of every executable step are sealed as a
    /// single accepted envelope, or none of them are.
    let private prepareCreation configuration (args: StartArgs) inquiryId fingerprint configHash =
        if args.ProfileRef <> configuration.Profile.ProfileRef then
            Error(refusal "PROFILE_NOT_AVAILABLE" "profileRef" "start must select the authorized startup profile")
        else
            resolveSteps args
            |> Result.bind (fun steps ->
                validatePlan steps
                |> Result.bind (fun () ->
                    let goal =
                        { GoalId = GoalId.create (InquiryId.value inquiryId + "-goal")
                          Revision = Revision.origin
                          OriginalText = args.GoalText
                          Constraints = args.Constraints
                          MaterialRefs = args.MaterialRefs |> List.map ArtifactRef.create
                          AuthorizationRef = args.AuthorizationRef
                          CreatedBy = configuration.CreatedBy
                          Amendments = [] }

                    let specs = planSpecs steps args.GoalText args.Constraints

                    let creationEvents =
                        [ InquiryEventBody.InquiryCreated
                              { Goal = goal
                                ResourceSpecs = configuration.ResourceSpecs
                                RenderReserve = configuration.RenderReserve
                                ProfileRef = configuration.Profile.ProfileRef
                                ConfigHash = configHash }
                        ] @ (if List.isEmpty specs then [] else [ InquiryEventBody.WorkPlanned specs ])

                    let provisionalEventId =
                        EventId.create ("provisional-" + InquiryId.value inquiryId)

                    let provisionalBatch =
                        { SchemaVersion = "2"
                          InquiryId = inquiryId
                          PreviousRevision = Revision.origin
                          PreviousHead = None
                          Revision = Revision.origin
                          CommandId = args.CommandId
                          CommandFingerprint = fingerprint
                          PostStateFingerprint = None
                          Events = creationEvents }

                    Reducer.applyTransition HostDigest.sha256Hex provisionalEventId None provisionalBatch
                    |> Result.mapError (fun fault -> refusal fault.Code "steps" fault.Message)
                    |> Result.bind (fun created ->
                        let dispatchEvents = (Driver.advance created).Events

                        let batch =
                            { provisionalBatch with
                                Events = creationEvents @ dispatchEvents }

                        Codec.seal HostDigest.sha256Hex None batch
                        |> Result.mapError (fun fault -> refusal fault.Code "steps" fault.Message))))

    let private appendCreation handle inquiryId commandId fingerprint encoded =
        task {
            let store = store handle

            match! store.Append [ encoded ] with
            | Error fault -> return Error(terminalToolRefusal handle inquiryId commandId encoded.EventId fault)
            | Ok receipt when not (List.isEmpty receipt.Cuts) ->
                return
                    Error(
                        refusal
                            "PERSISTENCE_SEMANTIC_CUT"
                            "inquiryId"
                            (receipt.Cuts |> List.map (fun cut -> cut.Reason) |> String.concat "; ")
                    )
            | Ok _ ->
                let receipt =
                    { Fingerprint = fingerprint
                      Revision = Revision.origin
                      EventId = Wanxiangshu.Foundation.Identity.EventId.value encoded.EventId |> EventId.create }

                return Ok(receiptPayload store "created" inquiryId receipt)
        }

    let private createInquiry handle configuration args inquiryId fingerprint configHash =
        task {
            match prepareCreation configuration args inquiryId fingerprint configHash with
            | Error fault -> return Error fault
            | Ok encoded -> return! appendCreation handle inquiryId args.CommandId fingerprint encoded
        }

    let private existingCreation
        store
        configuration
        (args: StartArgs)
        inquiryId
        fingerprint
        configHash
        (state: InquiryState)
        =
        let command =
            InquiryCommand.StartCommand
                { CommandId = args.CommandId
                  Goal = state.Goal
                  ResourceSpecs = configuration.ResourceSpecs
                  RenderReserve = configuration.RenderReserve
                  ProfileRef = args.ProfileRef
                  ConfigHash = configHash }

        Admission.admitCommand state args.CommandId fingerprint command
        |> Result.mapError (fun fault -> refusal fault.Code "commandId" fault.Message)
        |> Result.bind (function
            | IdempotencyOutcome.Conflict message -> Error(refusal "COMMAND_CONFLICT" "commandId" message)
            | IdempotencyOutcome.Fresh _ ->
                Error(
                    refusal
                        "COMMAND_CONFLICT"
                        "commandId"
                        "inquiry identity already exists without this creation receipt"
                )
            | IdempotencyOutcome.Replay _ ->
                InquiryState.commandReceipt state args.CommandId
                |> Option.map (receiptPayload store "replayed" inquiryId >> Ok)
                |> Option.defaultValue (
                    Error(refusal "PERSISTENCE_READ_FAILED" "commandId" "accepted command receipt is missing")
                ))

    let private configuredStart handle configuration args =
        task {
            let store = store handle
            let configHash = configurationView configuration |> digest

            let fingerprint =
                native [ "request", requestView args; "configHash", box configHash ] |> digest

            let inquiryId =
                native
                    [ "namespace", box configuration.CommandNamespace
                      "commandId", box args.CommandId ]
                |> digest
                |> fun value -> InquiryId.create ("inq" + value)

            match find store inquiryId with
            | Error fault -> return Error fault
            | Ok None -> return! createInquiry handle configuration args inquiryId fingerprint configHash
            | Ok(Some state) -> return existingCreation store configuration args inquiryId fingerprint configHash state
        }

    let start (handle: RuntimeHandle) (args: StartArgs) : Task<Result<obj, ToolRefusal>> =
        match handle.Configuration with
        | None ->
            Task.FromResult(
                Error(
                    refusal
                        "CONFIG_REQUIRED"
                        "configuration"
                        "start requires explicit authorized startup resources and execution mode"
                )
            )
        | Some configuration -> configuredStart handle configuration args

    /// One submitted result is one accepted observation. Its identity is a pure
    /// function of the work, attempt and fence, so a repeated delivery of the same
    /// result returns the original receipt instead of booking a second observation.
    let private observationIdentity (args: WorkSubmitArgs) : ObservationId =
        let derived =
            native
                [ "workId", box args.WorkId
                  "attempt", box (string (Attempt.value args.Attempt))
                  "fence", box (Fence.value args.Fence) ]
            |> digest

        ObservationId.create ("obs" + derived)

    let private submitResultEvents
        (state: InquiryState)
        (args: WorkSubmitArgs)
        (item: WorkItem)
        (observationId: ObservationId)
        : InquiryEventBody list =
        let spec = item.Spec

        match args.Outcome with
        | WorkOutcome.Failed ->
            [ InquiryEventBody.WorkAttemptTransitioned
                  { WorkId = spec.Id
                    Attempt = spec.Attempt
                    Fence = spec.Fence
                    FromState = Work.stateName item.State
                    NextState = WorkState.Failed spec.Attempt
                    PhysicalRef = None } ]
        | WorkOutcome.Succeeded ->
            let resultSchema = spec.OutputSchema |> Option.defaultValue answerSchema

            let accepted =
                InquiryEventBody.ResultAccepted
                    { WorkId = spec.Id
                      Attempt = spec.Attempt
                      Fence = spec.Fence
                      ObservationId = observationId
                      CanonicalResult = args.CanonicalResult
                      ResultSchema = resultSchema
                      ClusterId = WorkId.value spec.Id }

            let isAnswerWork =
                answerWorkId state.Plan = Some spec.Id

            if isAnswerWork then
                [ accepted
                  InquiryEventBody.AnswerPrepared
                      { RenderWorkId = spec.Id
                        DraftRef = ObservationId.value observationId }
                  InquiryEventBody.AnswerCommitted
                      { RenderWorkId = spec.Id
                        ResultObservationId = observationId
                        AnswerRef = ObservationId.value observationId
                        StopReason = "answer-committed" } ]
            else
                [ accepted ]

    let submit (handle: RuntimeHandle) (args: WorkSubmitArgs) : Task<Result<obj, ToolRefusal>> =
        task {
            let store = store handle
            let inquiryId = InquiryId.create args.InquiryId

            match read store inquiryId with
            | Error fault -> return Error fault
            | Ok state ->
                let workId = WorkId.create args.WorkId

                match state.Work |> Map.tryFind workId with
                | None ->
                    return
                        Error(
                            refusal
                                "UNKNOWN_WORK"
                                "workId"
                                (sprintf "work %s is not part of this inquiry" args.WorkId)
                        )
                | Some item ->
                    if item.Spec.Attempt <> args.Attempt then
                        return Error(refusal "ATTEMPT_MISMATCH" "attempt" "result attempt does not match the work attempt")
                    elif item.Spec.Fence <> args.Fence then
                        return Error(refusal "STALE_FENCE" "fence" "result fence does not match the work attempt")
                    else
                        let observationId = observationIdentity args
                        let key = ObservationId.value observationId

                        match state.Observations |> Map.tryFind key with
                        | Some existing when existing.CanonicalResult = args.CanonicalResult ->
                            return
                                native
                                    [ "apiVersion", box Contract.apiVersion
                                      "outcome", box "replayed"
                                      "inquiryId", box args.InquiryId
                                      "workId", box args.WorkId
                                      "observationId", box key
                                      "revision", box (Encode.revision state.Revision) ]
                                |> Ok
                        | Some _ ->
                            return
                                Error(
                                    refusal
                                        "RESULT_PAYLOAD_CONFLICT"
                                        "canonicalResult"
                                        "this result identity is already accepted with different bytes"
                                )
                        | None ->
                            let running =
                                match item.State with
                                | WorkState.Leased fence when fence = args.Fence -> true
                                | WorkState.Running _ -> true
                                | _ -> false

                            if not running then
                                return
                                    Error(
                                        refusal
                                            "WORK_NOT_LEASED"
                                            "workId"
                                            (sprintf "work %s is not leased to the submitting attempt" args.WorkId)
                                    )
                            else
                                let fingerprint =
                                    native
                                        [ "request",
                                          native
                                              [ "commandId", box args.CommandId
                                                "inquiryId", box args.InquiryId
                                                "workId", box args.WorkId
                                                "attempt", box (string (Attempt.value args.Attempt))
                                                "fence", box (Fence.value args.Fence)
                                                "canonicalResult", box args.CanonicalResult
                                                "outcome",
                                                box (
                                                    match args.Outcome with
                                                    | WorkOutcome.Succeeded -> "succeeded"
                                                    | WorkOutcome.Failed -> "failed"
                                                )
                                                "reason", args.Reason |> Option.map box |> Option.toObj ] ]
                                    |> digest

                                let submission : ResultSubmission =
                                    { WorkId = workId
                                      Attempt = args.Attempt
                                      Fence = args.Fence
                                      ObservationId = observationId
                                      CanonicalResult = args.CanonicalResult
                                      ResultSchema = item.Spec.OutputSchema |> Option.defaultValue answerSchema
                                      ClusterId = WorkId.value workId
                                      Usage = None }

                                match
                                    Admission.admitCommand
                                        state
                                        args.CommandId
                                        fingerprint
                                        (InquiryCommand.SubmitResultCommand [ submission ])
                                with
                                | Error fault ->
                                    return Error(refusal fault.Code "commandId" fault.Message)
                                | Ok(IdempotencyOutcome.Conflict message) ->
                                    return Error(refusal "COMMAND_CONFLICT" "commandId" message)
                                | Ok(IdempotencyOutcome.Replay revision) ->
                                    return
                                        native
                                            [ "apiVersion", box Contract.apiVersion
                                              "outcome", box "replayed"
                                              "inquiryId", box args.InquiryId
                                              "workId", box args.WorkId
                                              "observationId", box key
                                              "revision", box (Encode.revision revision) ]
                                        |> Ok
                                | Ok(IdempotencyOutcome.Fresh _) ->
                                    let resultEvents = submitResultEvents state args item observationId

                                    let nextRevision = Revision.next state.Revision

                                    let provisionalBatch =
                                        { SchemaVersion = "2"
                                          InquiryId = inquiryId
                                          PreviousRevision = state.Revision
                                          PreviousHead = state.EventHead
                                          Revision = nextRevision
                                          CommandId = args.CommandId
                                          CommandFingerprint = fingerprint
                                          PostStateFingerprint = None
                                          Events = resultEvents }

                                    match
                                        Reducer.applyTransition
                                            HostDigest.sha256Hex
                                            (EventId.create ("provisional-" + args.CommandId))
                                            (Some state)
                                            provisionalBatch
                                    with
                                    | Error fault -> return Error(refusal fault.Code "workId" fault.Message)
                                    | Ok acceptedState ->
                                        let followUp =
                                            if InquiryState.isTerminal acceptedState.Status then
                                                []
                                            else
                                                (Driver.advance acceptedState).Events

                                        let batch =
                                            { provisionalBatch with
                                                Events = resultEvents @ followUp }

                                        match Codec.seal HostDigest.sha256Hex (Some state) batch with
                                        | Error fault -> return Error(refusal fault.Code "workId" fault.Message)
                                        | Ok encoded ->
                                            match! store.Append [ encoded ] with
                                            | Error fault ->
                                                return Error(terminalToolRefusal handle inquiryId args.CommandId encoded.EventId fault)
                                            | Ok receipt when not (List.isEmpty receipt.Cuts) ->
                                                return
                                                    Error(
                                                        refusal
                                                            "PERSISTENCE_SEMANTIC_CUT"
                                                            "inquiryId"
                                                            (receipt.Cuts
                                                             |> List.map (fun cut -> cut.Reason)
                                                             |> String.concat "; ")
                                                    )
                                            | Ok _ ->
                                                return
                                                    native
                                                        [ "apiVersion", box Contract.apiVersion
                                                          "outcome", box "applied"
                                                          "inquiryId", box args.InquiryId
                                                          "workId", box args.WorkId
                                                          "observationId", box key
                                                          "revision", box (Encode.revision nextRevision)
                                                          "status", box (Encode.statusOf acceptedState)
                                                          "answer", answerView acceptedState ]
                                                    |> Ok
        }

    let exportInquiry (handle: RuntimeHandle) (args: ExportArgs) : Result<obj, ToolRefusal> =
        let store = handle.Store
        let inquiryId = InquiryId.create args.InquiryId

        read store inquiryId
        |> Result.bind (fun state ->
            Bind.tryTrace store inquiryId
            |> Result.mapError currentRefusal
            |> Result.bind (function
                | None -> Error(unknown args.InquiryId)
                | Some trace ->
                    let canonicalEnvelopes = trace |> List.map CanonicalEventCodec.encode
                    let events = canonicalEnvelopes |> List.map JS.JSON.parse |> List.toArray |> box

                    let semantic = Encode.semanticView state
                    let complete = Representation.state state

                    let fields =
                        [ "apiVersion", box Contract.apiVersion
                          "outcome", box "exported"
                          "inquiryId", box args.InquiryId
                          "revision", box (Encode.revision state.Revision)
                          "status", box (Encode.statusOf state)
                          "traceHash", Projection.traceHash canonicalEnvelopes
                          "stateHash", digest complete
                          "semanticHash", digest semantic
                          "inquiry", semantic ]

                    match args.Mode with
                    | Wanxiangshu.Sphinx.V2.Hosts.ExportMode.Summary ->
                        Ok(native (fields @ [ "mode", box "summary"; "replayability", box "summary-only" ]))
                    | Wanxiangshu.Sphinx.V2.Hosts.ExportMode.Full ->
                        let externalInputs =
                            native [ "kind", box "startup-configuration"; "ref", box state.ConfigHash ]
                            :: (state.Goal.MaterialRefs
                                |> List.map (fun reference ->
                                    native [ "kind", box "material"; "ref", box (ArtifactRef.value reference) ]))

                        Ok(
                            native (
                                fields
                                @ [ "mode", box "full"
                                    "replayability", box "requires-external-inputs"
                                    "replayabilityReason",
                                    box
                                        "Startup configuration, schema documents, plugin implementations and external materials are not bundled."
                                    "externalInputsComplete", box false
                                    "events", events
                                    "state", complete
                                    "externalInputs", externalInputs |> List.toArray |> box ]
                            )
                        )))
