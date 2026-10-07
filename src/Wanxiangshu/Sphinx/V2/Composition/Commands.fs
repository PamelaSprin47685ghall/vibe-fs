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
                  "inquiry", Encode.semanticView state ])

    let private requestView (args: StartArgs) =
        native
            [ "commandId", box args.CommandId
              "goalText", box args.GoalText
              "constraints", strings args.Constraints
              "materialRefs", strings args.MaterialRefs
              "authorizationRef", box args.AuthorizationRef
              "profileRef", box args.ProfileRef ]

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

    let private prepareCreation configuration (args: StartArgs) inquiryId fingerprint configHash =
        let goal =
            { GoalId = GoalId.create (InquiryId.value inquiryId + "-goal")
              Revision = Revision.origin
              OriginalText = args.GoalText
              Constraints = args.Constraints
              MaterialRefs = args.MaterialRefs |> List.map ArtifactRef.create
              AuthorizationRef = args.AuthorizationRef
              CreatedBy = configuration.CreatedBy
              Amendments = [] }

        let batch =
            { SchemaVersion = "2"
              InquiryId = inquiryId
              PreviousRevision = Revision.origin
              PreviousHead = None
              Revision = Revision.origin
              CommandId = args.CommandId
              CommandFingerprint = fingerprint
              PostStateFingerprint = None
              Events =
                [ InquiryEventBody.InquiryCreated
                      { Goal = goal
                        ResourceSpecs = configuration.ResourceSpecs
                        RenderReserve = configuration.RenderReserve
                        ProfileRef = configuration.Profile.ProfileRef
                        ConfigHash = configHash } ] }

        if args.ProfileRef <> configuration.Profile.ProfileRef then
            Error(refusal "PROFILE_NOT_AVAILABLE" "profileRef" "start must select the authorized startup profile")
        else
            Codec.seal HostDigest.sha256Hex None batch
            |> Result.mapError (fun fault -> refusal fault.Code "goalText" fault.Message)

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
