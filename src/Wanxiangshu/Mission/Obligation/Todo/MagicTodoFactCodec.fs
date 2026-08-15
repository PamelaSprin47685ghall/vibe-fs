namespace Wanxiangshu.Mission.Obligation.Todo

open Wanxiangshu.Composition.Durable

open Thoth.Json
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Foundation.Identity

/// Canonical tagged codec for `Fact.MagicTodo` payload bytes.
///
/// `FactCodec` owns the outer journal union; this module owns only the typed
/// Magic Todo payload and fails closed on any unknown or malformed inner case.
module MagicTodoFactCodec =

    let private cursorEncoder (c: XTraceCursor) =
        Encode.object [ "Sequence", Encode.int64 c.Sequence ]

    let private cursorDecoder: Decoder<XTraceCursor> =
        Decode.object (fun get -> { Sequence = get.Required.Field "Sequence" Decode.int64 })

    let private todoWriteIdEncoder (id: TodoWriteId) = Encode.string (TodoWriteId.value id)

    let private todoWriteIdDecoder: Decoder<TodoWriteId> =
        Decode.string |> Decode.map TodoWriteId.create

    let private todoReviewIdEncoder (id: TodoReviewId) = Encode.string (TodoReviewId.value id)

    let private todoReviewIdDecoder: Decoder<TodoReviewId> =
        Decode.string |> Decode.map TodoReviewId.create

    let private dedicatedIdEncoder (id: DedicatedReviewerId) =
        Encode.string (DedicatedReviewerId.value id)

    let private dedicatedIdDecoder: Decoder<DedicatedReviewerId> =
        Decode.string |> Decode.map DedicatedReviewerId.create

    let private verdictEncoder (v: ProcessReviewVerdict) =
        Encode.string (ProcessReviewVerdict.wire v)

    let private verdictDecoder: Decoder<ProcessReviewVerdict> =
        Decode.string
        |> Decode.andThen (function
            | "PERFECT" -> Decode.succeed ProcessReviewVerdict.Perfect
            | "REVISE" -> Decode.succeed ProcessReviewVerdict.Revise
            | other -> Decode.fail ("unknown ProcessReviewVerdict: " + other))

    let private physicalEvidenceEncoder (e: PhysicalSuccessEvidence) =
        match e with
        | PhysicalSuccessEvidence.LiveAfterSuccess -> Encode.string "LiveAfterSuccess"
        | PhysicalSuccessEvidence.RecoveredCompletedToolPart -> Encode.string "RecoveredCompletedToolPart"

    let private physicalEvidenceDecoder: Decoder<PhysicalSuccessEvidence> =
        Decode.string
        |> Decode.andThen (function
            | "LiveAfterSuccess" -> Decode.succeed PhysicalSuccessEvidence.LiveAfterSuccess
            | "RecoveredCompletedToolPart" -> Decode.succeed PhysicalSuccessEvidence.RecoveredCompletedToolPart
            | other -> Decode.fail ("unknown PhysicalSuccessEvidence: " + other))

    let private evidenceKindEncoder (k: PrefixEvidenceKind) =
        match k with
        | PrefixEvidenceKind.Probe probeId ->
            Encode.object [ "kind", Encode.string "Probe"; "probeId", Encode.string probeId ]
        | PrefixEvidenceKind.TodoCheckpoint(trigger, covered) ->
            Encode.object
                [ "kind", Encode.string "TodoCheckpoint"
                  "triggerTodoWriteId", todoWriteIdEncoder trigger
                  "coveredBeforeTodoWriteId",
                  match covered with
                  | None -> Encode.nil
                  | Some id -> todoWriteIdEncoder id ]

    let private evidenceKindDecoder: Decoder<PrefixEvidenceKind> =
        Decode.object (fun get ->
            match get.Required.Field "kind" Decode.string with
            | "Probe" -> PrefixEvidenceKind.Probe(get.Required.Field "probeId" Decode.string)
            | "TodoCheckpoint" ->
                PrefixEvidenceKind.TodoCheckpoint(
                    get.Required.Field "triggerTodoWriteId" todoWriteIdDecoder,
                    get.Optional.Field "coveredBeforeTodoWriteId" todoWriteIdDecoder
                )
            | other -> failwith ("unknown PrefixEvidenceKind: " + other))

    /// PayloadRef a BlobRef names: the local content-address under the "blobs/" prefix.
    let payloadRefOfBlobRef (ref: BlobRef) : PayloadRef =
        let value = BlobRef.value ref
        let prefix = "blobs/"

        if value.StartsWith(prefix, System.StringComparison.Ordinal) then
            PayloadRef.create (value.Substring(prefix.Length))
        else
            PayloadRef.create value

    /// PayloadRef a BlobDigest names: sha256 of the payload bytes == store filename.
    let payloadRefOfBlobDigest (digest: BlobDigest) : PayloadRef =
        PayloadRef.create (BlobDigest.value digest)

    /// Unified payload closure of one MagicTodo fact: every EventStore payload it
    /// references, so the envelope `payload_refs` field is authoritative rather
    /// than always-empty. Caller canonicalizes the combined list.
    let payloadRefs (fact: MagicTodoFact) : PayloadRef list =
        match fact with
        | MagicTodoFact.TodoWritePrepared p ->
            [ payloadRefOfBlobRef p.BaseTodoRef
              payloadRefOfBlobDigest p.BaseTodoDigest
              payloadRefOfBlobRef p.ProposedTodoRef
              payloadRefOfBlobDigest p.ProposedTodoDigest ]
        | MagicTodoFact.TodoReviewConcluded p ->
            [ payloadRefOfBlobRef p.WorkRecordRef
              payloadRefOfBlobDigest p.WorkRecordDigest
              payloadRefOfBlobRef p.SettledTodoRef
              payloadRefOfBlobDigest p.SettledTodoDigest ]
        | MagicTodoFact.DedicatedTodoReviewerReplaced p -> [ payloadRefOfBlobRef p.EvidenceRef ]
        | MagicTodoFact.LegacyTodoSeedAdopted p ->
            [ payloadRefOfBlobRef p.SeedTodoRef
              payloadRefOfBlobDigest p.SeedTodoDigest ]
        | MagicTodoFact.PrefixRebaseCommittedV2 p ->
            [ payloadRefOfBlobRef p.FrozenRecordPrefixRef
              payloadRefOfBlobDigest p.FrozenRecordPrefixDigest ]
            @ (p.YBundleRef |> Option.toList |> List.map payloadRefOfBlobRef)
            @ (p.YBundleDigest |> Option.toList |> List.map payloadRefOfBlobDigest)
        | _ -> []

    /// Encode one Magic Todo fact as a tagged JSON object (case name + fields).
    let encode (fact: MagicTodoFact) : string =
        let body =
            match fact with
            | MagicTodoFact.TodoWritePrepared p ->
                Encode.object
                    [ "case", Encode.string "TodoWritePrepared"
                      "ManagerSessionId", Encode.string (SessionId.value p.ManagerSessionId)
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "TodoWriteId", todoWriteIdEncoder p.TodoWriteId
                      "ToolCallId", Encode.string (ToolCallId.value p.ToolCallId)
                      "ToolPartOrdinal", Encode.int p.ToolPartOrdinal
                      "BaseTodoRef", Encode.string (BlobRef.value p.BaseTodoRef)
                      "BaseTodoDigest", Encode.string (BlobDigest.value p.BaseTodoDigest)
                      "ProposedTodoRef", Encode.string (BlobRef.value p.ProposedTodoRef)
                      "ProposedTodoDigest", Encode.string (BlobDigest.value p.ProposedTodoDigest)
                      "PlanCompleteDeclared", Encode.bool p.PlanCompleteDeclared
                      "ProviderInputDigest", Encode.string p.ProviderInputDigest
                      "ReviewFrontier", cursorEncoder p.ReviewFrontier
                      "SemanticVersion", Encode.string p.SemanticVersion ]
            | MagicTodoFact.TodoWriteAccepted p ->
                Encode.object
                    [ "case", Encode.string "TodoWriteAccepted"
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "TodoWriteId", todoWriteIdEncoder p.TodoWriteId
                      "ToolCallId", Encode.string (ToolCallId.value p.ToolCallId)
                      "PreparedFactRef", Encode.string (EventId.value p.PreparedFactRef)
                      "InputDigest", Encode.string p.InputDigest
                      "OutputDigest", Encode.string p.OutputDigest
                      "PhysicalSuccessEvidence", physicalEvidenceEncoder p.PhysicalSuccessEvidence
                      "SemanticVersion", Encode.string p.SemanticVersion ]
            | MagicTodoFact.TodoProcessReviewAssigned p ->
                Encode.object
                    [ "case", Encode.string "TodoProcessReviewAssigned"
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "TodoWriteId", todoWriteIdEncoder p.TodoWriteId
                      "TodoReviewId", todoReviewIdEncoder p.TodoReviewId
                      "DedicatedReviewerId", dedicatedIdEncoder p.DedicatedReviewerId
                      "ReviewerSessionId", Encode.string (SessionId.value p.ReviewerSessionId)
                      "ReviewWorkStartCursor", cursorEncoder p.ReviewWorkStartCursor
                      "ManagerReviewFrontier", cursorEncoder p.ManagerReviewFrontier ]
            | MagicTodoFact.TodoReviewConcluded p ->
                Encode.object
                    [ "case", Encode.string "TodoReviewConcluded"
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "TodoWriteId", todoWriteIdEncoder p.TodoWriteId
                      "TodoReviewId", todoReviewIdEncoder p.TodoReviewId
                      "DedicatedReviewerId", dedicatedIdEncoder p.DedicatedReviewerId
                      "ReviewerSessionId", Encode.string (SessionId.value p.ReviewerSessionId)
                      "Verdict", verdictEncoder p.Verdict
                      "WorkRecordRef", Encode.string (BlobRef.value p.WorkRecordRef)
                      "WorkRecordDigest", Encode.string (BlobDigest.value p.WorkRecordDigest)
                      "SettledTodoRef", Encode.string (BlobRef.value p.SettledTodoRef)
                      "SettledTodoDigest", Encode.string (BlobDigest.value p.SettledTodoDigest)
                      "ReviewerRecordFrontier", cursorEncoder p.ReviewerRecordFrontier
                      "ProviderRunId", Encode.string (ProviderRunIdentity.value p.ProviderRunId)
                      "ToolCallId", Encode.string (ToolCallId.value p.ToolCallId) ]
            | MagicTodoFact.DedicatedTodoReviewerEnlisted p ->
                Encode.object
                    [ "case", Encode.string "DedicatedTodoReviewerEnlisted"
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "DedicatedReviewerId", dedicatedIdEncoder p.DedicatedReviewerId
                      "ReviewerSessionId", Encode.string (SessionId.value p.ReviewerSessionId) ]
            | MagicTodoFact.DedicatedTodoReviewerReplaced p ->
                Encode.object
                    [ "case", Encode.string "DedicatedTodoReviewerReplaced"
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "DedicatedReviewerId", dedicatedIdEncoder p.DedicatedReviewerId
                      "OldSessionId", Encode.string (SessionId.value p.OldSessionId)
                      "NewSessionId", Encode.string (SessionId.value p.NewSessionId)
                      "EvidenceRef", Encode.string (BlobRef.value p.EvidenceRef) ]
            | MagicTodoFact.LegacyTodoSeedAdopted p ->
                Encode.object
                    [ "case", Encode.string "LegacyTodoSeedAdopted"
                      "ManagerSessionId", Encode.string (SessionId.value p.ManagerSessionId)
                      "ManagerLifeId", Encode.string (ManagerLifeId.value p.ManagerLifeId)
                      "SeedTodoRef", Encode.string (BlobRef.value p.SeedTodoRef)
                      "SeedTodoDigest", Encode.string (BlobDigest.value p.SeedTodoDigest) ]
            | MagicTodoFact.PrefixRebaseCommittedV2 p ->
                Encode.object
                    [ "case", Encode.string "PrefixRebaseCommittedV2"
                      "SessionId", Encode.string (SessionId.value p.SessionId)
                      "ManagerLifeId",
                      match p.ManagerLifeId with
                      | None -> Encode.nil
                      | Some id -> Encode.string (ManagerLifeId.value id)
                      "PreviousEpochId", Encode.int64 (PrefixEpochId.value p.PreviousEpochId)
                      "NextEpochId", Encode.int64 (PrefixEpochId.value p.NextEpochId)
                      "EvidenceKind", evidenceKindEncoder p.EvidenceKind
                      "FrozenRecordPrefixRef", Encode.string (BlobRef.value p.FrozenRecordPrefixRef)
                      "FrozenRecordPrefixDigest", Encode.string (BlobDigest.value p.FrozenRecordPrefixDigest)
                      "CutoffExclusive", Encode.int p.CutoffExclusive
                      "CoveredPrefixDigest", Encode.string p.CoveredPrefixDigest
                      "SealRoot", Encode.string p.SealRoot
                      "SyntheticMessageId", Encode.string p.SyntheticMessageId
                      "YBundleRef",
                      match p.YBundleRef with
                      | None -> Encode.nil
                      | Some r -> Encode.string (BlobRef.value r)
                      "YBundleDigest",
                      match p.YBundleDigest with
                      | None -> Encode.nil
                      | Some d -> Encode.string (BlobDigest.value d)
                      "ProviderPrefixDigest",
                      match p.ProviderPrefixDigest with
                      | None -> Encode.nil
                      | Some s -> Encode.string s
                      "SolvingProviderRun",
                      match p.SolvingProviderRun with
                      | None -> Encode.nil
                      | Some r -> Encode.string (ProviderRunIdentity.value r) ]

        Encode.toString 0 body

    /// Decode counterpart. Fail closed on unknown case / corrupt fields.
    let tryDecode (json: string) : Result<MagicTodoFact, string> =
        let decoder: Decoder<MagicTodoFact> =
            Decode.object (fun get ->
                match get.Required.Field "case" Decode.string with
                | "TodoWritePrepared" ->
                    MagicTodoFact.TodoWritePrepared
                        { ManagerSessionId = SessionId.create (get.Required.Field "ManagerSessionId" Decode.string)
                          ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          TodoWriteId = get.Required.Field "TodoWriteId" todoWriteIdDecoder
                          ToolCallId = ToolCallId.create (get.Required.Field "ToolCallId" Decode.string)
                          ToolPartOrdinal = get.Required.Field "ToolPartOrdinal" Decode.int
                          BaseTodoRef = BlobRef.create (get.Required.Field "BaseTodoRef" Decode.string)
                          BaseTodoDigest = BlobDigest.create (get.Required.Field "BaseTodoDigest" Decode.string)
                          ProposedTodoRef = BlobRef.create (get.Required.Field "ProposedTodoRef" Decode.string)
                          ProposedTodoDigest =
                            BlobDigest.create (get.Required.Field "ProposedTodoDigest" Decode.string)
                          // Legacy Magic Todo payloads predate planComplete; that
                          // protocol defined every accepted checkpoint as the
                          // complete plan, so absence migrates to true.
                          PlanCompleteDeclared =
                            get.Optional.Field "PlanCompleteDeclared" Decode.bool
                            |> Option.defaultValue true
                          ProviderInputDigest = get.Required.Field "ProviderInputDigest" Decode.string
                          ReviewFrontier = get.Required.Field "ReviewFrontier" cursorDecoder
                          SemanticVersion = get.Required.Field "SemanticVersion" Decode.string }
                | "TodoWriteAccepted" ->
                    MagicTodoFact.TodoWriteAccepted
                        { ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          TodoWriteId = get.Required.Field "TodoWriteId" todoWriteIdDecoder
                          ToolCallId = ToolCallId.create (get.Required.Field "ToolCallId" Decode.string)
                          PreparedFactRef = EventId.create (get.Required.Field "PreparedFactRef" Decode.string)
                          InputDigest = get.Required.Field "InputDigest" Decode.string
                          OutputDigest = get.Required.Field "OutputDigest" Decode.string
                          PhysicalSuccessEvidence =
                            get.Required.Field "PhysicalSuccessEvidence" physicalEvidenceDecoder
                          SemanticVersion = get.Required.Field "SemanticVersion" Decode.string }
                | "TodoProcessReviewAssigned" ->
                    MagicTodoFact.TodoProcessReviewAssigned
                        { ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          TodoWriteId = get.Required.Field "TodoWriteId" todoWriteIdDecoder
                          TodoReviewId = get.Required.Field "TodoReviewId" todoReviewIdDecoder
                          DedicatedReviewerId = get.Required.Field "DedicatedReviewerId" dedicatedIdDecoder
                          ReviewerSessionId = SessionId.create (get.Required.Field "ReviewerSessionId" Decode.string)
                          ReviewWorkStartCursor = get.Required.Field "ReviewWorkStartCursor" cursorDecoder
                          ManagerReviewFrontier = get.Required.Field "ManagerReviewFrontier" cursorDecoder }
                | "TodoReviewConcluded" ->
                    MagicTodoFact.TodoReviewConcluded
                        { ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          TodoWriteId = get.Required.Field "TodoWriteId" todoWriteIdDecoder
                          TodoReviewId = get.Required.Field "TodoReviewId" todoReviewIdDecoder
                          DedicatedReviewerId = get.Required.Field "DedicatedReviewerId" dedicatedIdDecoder
                          ReviewerSessionId = SessionId.create (get.Required.Field "ReviewerSessionId" Decode.string)
                          Verdict = get.Required.Field "Verdict" verdictDecoder
                          WorkRecordRef = BlobRef.create (get.Required.Field "WorkRecordRef" Decode.string)
                          WorkRecordDigest = BlobDigest.create (get.Required.Field "WorkRecordDigest" Decode.string)
                          SettledTodoRef = BlobRef.create (get.Required.Field "SettledTodoRef" Decode.string)
                          SettledTodoDigest = BlobDigest.create (get.Required.Field "SettledTodoDigest" Decode.string)
                          ReviewerRecordFrontier = get.Required.Field "ReviewerRecordFrontier" cursorDecoder
                          ProviderRunId = ProviderRunIdentity.create (get.Required.Field "ProviderRunId" Decode.string)
                          ToolCallId = ToolCallId.create (get.Required.Field "ToolCallId" Decode.string) }
                | "DedicatedTodoReviewerEnlisted" ->
                    MagicTodoFact.DedicatedTodoReviewerEnlisted
                        { ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          DedicatedReviewerId = get.Required.Field "DedicatedReviewerId" dedicatedIdDecoder
                          ReviewerSessionId = SessionId.create (get.Required.Field "ReviewerSessionId" Decode.string) }
                | "DedicatedTodoReviewerReplaced" ->
                    MagicTodoFact.DedicatedTodoReviewerReplaced
                        { ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          DedicatedReviewerId = get.Required.Field "DedicatedReviewerId" dedicatedIdDecoder
                          OldSessionId = SessionId.create (get.Required.Field "OldSessionId" Decode.string)
                          NewSessionId = SessionId.create (get.Required.Field "NewSessionId" Decode.string)
                          EvidenceRef = BlobRef.create (get.Required.Field "EvidenceRef" Decode.string) }
                | "LegacyTodoSeedAdopted" ->
                    MagicTodoFact.LegacyTodoSeedAdopted
                        { ManagerSessionId = SessionId.create (get.Required.Field "ManagerSessionId" Decode.string)
                          ManagerLifeId = ManagerLifeId.create (get.Required.Field "ManagerLifeId" Decode.string)
                          SeedTodoRef = BlobRef.create (get.Required.Field "SeedTodoRef" Decode.string)
                          SeedTodoDigest = BlobDigest.create (get.Required.Field "SeedTodoDigest" Decode.string) }
                | "PrefixRebaseCommittedV2" ->
                    MagicTodoFact.PrefixRebaseCommittedV2
                        { SessionId = SessionId.create (get.Required.Field "SessionId" Decode.string)
                          ManagerLifeId =
                            get.Optional.Field "ManagerLifeId" Decode.string
                            |> Option.map ManagerLifeId.create
                          PreviousEpochId = PrefixEpochId.create (get.Required.Field "PreviousEpochId" Decode.int64)
                          NextEpochId = PrefixEpochId.create (get.Required.Field "NextEpochId" Decode.int64)
                          EvidenceKind = get.Required.Field "EvidenceKind" evidenceKindDecoder
                          FrozenRecordPrefixRef =
                            BlobRef.create (get.Required.Field "FrozenRecordPrefixRef" Decode.string)
                          FrozenRecordPrefixDigest =
                            BlobDigest.create (get.Required.Field "FrozenRecordPrefixDigest" Decode.string)
                          CutoffExclusive = get.Required.Field "CutoffExclusive" Decode.int
                          CoveredPrefixDigest = get.Required.Field "CoveredPrefixDigest" Decode.string
                          SealRoot = get.Required.Field "SealRoot" Decode.string
                          SyntheticMessageId = get.Required.Field "SyntheticMessageId" Decode.string
                          YBundleRef = get.Optional.Field "YBundleRef" Decode.string |> Option.map BlobRef.create
                          YBundleDigest =
                            get.Optional.Field "YBundleDigest" Decode.string |> Option.map BlobDigest.create
                          ProviderPrefixDigest = get.Optional.Field "ProviderPrefixDigest" Decode.string
                          SolvingProviderRun =
                            get.Optional.Field "SolvingProviderRun" Decode.string
                            |> Option.map ProviderRunIdentity.create }
                | other -> failwith ("unknown MagicTodoFact case: " + other))

        try
            match Decode.fromString decoder json with
            | Ok fact -> Ok fact
            | Error err -> Error err
        with error ->
            Error error.Message
