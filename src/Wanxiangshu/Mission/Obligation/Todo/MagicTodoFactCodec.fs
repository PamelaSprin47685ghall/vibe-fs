namespace Wanxiangshu.Mission.Obligation.Todo

open Thoth.Json
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Obligation.Todo.MagicTodo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts
open Wanxiangshu.Persistence.EventStore

/// Canonical tagged codec for `Fact.MagicTodo` payload bytes.
///
/// `FactCodec` owns the outer journal union; this module owns only the typed
/// Magic Todo payload and fails closed on any unknown or malformed inner case.
module MagicTodoFactCodec =

    let private cursorEncoder (c: XTraceCursor) =
        Encode.object [ "Sequence", Encode.int64 (XTraceCursor.sequence c) ]

    let private cursorDecoder: Decoder<XTraceCursor> =
        Decode.object (fun get -> XTraceCursor.create (get.Required.Field "Sequence" Decode.int64))

    let private todoWriteIdEncoder (id: TodoWriteId) = Encode.string (TodoWriteId.value id)

    let private todoWriteIdDecoder: Decoder<TodoWriteId> =
        Decode.string |> Decode.map TodoWriteId.create

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

    let private encodeOptional (encode: 'a -> JsonValue) =
        function
        | None -> Encode.nil
        | Some value -> encode value

    let private evidenceKindEncoder (k: PrefixEvidenceKind) =
        match k with
        | PrefixEvidenceKind.Probe probeId ->
            Encode.object [ "kind", Encode.string "Probe"; "probeId", Encode.string probeId ]
        | PrefixEvidenceKind.TodoCheckpoint(trigger, covered) ->
            Encode.object
                [ "kind", Encode.string "TodoCheckpoint"
                  "triggerTodoWriteId", todoWriteIdEncoder trigger
                  "coveredBeforeTodoWriteId", encodeOptional todoWriteIdEncoder covered ]

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

    /// A blob handle is an EventStore payload reference only when it is the
    /// content address (lowercase sha256 hex) the store names files by. Anything
    /// else — a test placeholder or malformed data — is not a payload dependency.
    let private payloadRefOfContentAddress (value: string) : PayloadRef option =
        let isHex c =
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')

        if value.Length = 64 && value |> Seq.forall isHex then
            Some(PayloadRef.create value)
        else
            None

    /// PayloadRef a BlobRef names ("blobs/<sha256>"); None when not content-addressed.
    let payloadRefOfBlobRef (ref: BlobRef) : PayloadRef option =
        let value = BlobRef.value ref
        let prefix = "blobs/"

        if value.StartsWith(prefix, System.StringComparison.Ordinal) then
            payloadRefOfContentAddress (value.Substring(prefix.Length))
        else
            None

    /// PayloadRef a BlobDigest names (sha256 of the payload bytes); None when not content-addressed.
    let payloadRefOfBlobDigest (digest: BlobDigest) : PayloadRef option =
        payloadRefOfContentAddress (BlobDigest.value digest)

    /// Unified payload closure of one MagicTodo fact: every EventStore payload it
    /// references, so the envelope `payload_refs` field is authoritative rather
    /// than always-empty. Caller canonicalizes the combined list.
    let payloadRefs (fact: MagicTodoFact) : PayloadRef list =
        match fact with
        | MagicTodoFact.TodoWritePrepared p ->
            List.choose
                id
                [ payloadRefOfBlobRef p.BaseTodoRef
                  payloadRefOfBlobDigest p.BaseTodoDigest
                  payloadRefOfBlobRef p.ProposedTodoRef
                  payloadRefOfBlobDigest p.ProposedTodoDigest ]
        | MagicTodoFact.LegacyTodoSeedAdopted p ->
            List.choose id [ payloadRefOfBlobRef p.SeedTodoRef; payloadRefOfBlobDigest p.SeedTodoDigest ]
        | MagicTodoFact.PrefixRebaseCommittedV2 p ->
            List.choose
                id
                [ payloadRefOfBlobRef p.FrozenRecordPrefixRef
                  payloadRefOfBlobDigest p.FrozenRecordPrefixDigest ]
            @ (p.YBundleRef |> Option.toList |> List.choose payloadRefOfBlobRef)
            @ (p.YBundleDigest |> Option.toList |> List.choose payloadRefOfBlobDigest)
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
                      "ManagerLifeId", encodeOptional (ManagerLifeId.value >> Encode.string) p.ManagerLifeId
                      "PreviousEpochId", Encode.int64 (PrefixEpochId.value p.PreviousEpochId)
                      "NextEpochId", Encode.int64 (PrefixEpochId.value p.NextEpochId)
                      "EvidenceKind", evidenceKindEncoder p.EvidenceKind
                      "FrozenRecordPrefixRef", Encode.string (BlobRef.value p.FrozenRecordPrefixRef)
                      "FrozenRecordPrefixDigest", Encode.string (BlobDigest.value p.FrozenRecordPrefixDigest)
                      "CutoffExclusive", Encode.int p.CutoffExclusive
                      "CoveredPrefixDigest", Encode.string p.CoveredPrefixDigest
                      "SealRoot", Encode.string p.SealRoot
                      "SyntheticMessageId", Encode.string p.SyntheticMessageId
                      "YBundleRef", encodeOptional (BlobRef.value >> Encode.string) p.YBundleRef
                      "YBundleDigest", encodeOptional (BlobDigest.value >> Encode.string) p.YBundleDigest
                      "ProviderPrefixDigest", encodeOptional Encode.string p.ProviderPrefixDigest
                      "SolvingProviderRun",
                      encodeOptional (ProviderRunIdentity.value >> Encode.string) p.SolvingProviderRun ]

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
            Decode.fromString decoder json
        with error ->
            Error error.Message
