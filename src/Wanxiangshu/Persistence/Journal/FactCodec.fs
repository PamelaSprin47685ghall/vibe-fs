namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Composition.Durable

open System
open Fable.Core
open Fable.Core.JsInterop
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Obligation.Todo.MagicTodoFacts

/// Fact serialization (PERSIST-005).
module FactCodec =

    let private baseExtra =
        { Extra.empty with
            Hash = "system-int64"
            Coders =
                Extra.empty.Coders
                |> Map.add "System.Int64" (Encode.boxEncoder Encode.int64, Decode.boxDecoder Decode.int64) }

    let private extra = PromptFactCodec.withCoder baseExtra

    let pre050MigrationMessage =
        "Wanxiangshu 0.5.0 does not support pre-0.5.0 runtime journals.\nArchive or remove the old Wanxiangshu runtime journal before starting."

    /// ENFORCER-072 / PERSIST-005: tip v2 clean break. Old ScoreVectorRef-era
    /// BlogObservationCommitted (legacy BlogEntryCommitted) lines cannot losslessly
    /// become a single tip (ties, empties, multi-high scores). Refuse — never invent
    /// a tip from max score.
    let tipV2CleanBreakMessage =
        "Wanxiangshu tip v2 requires BlogObservationCommitted.TipRuleId; ScoreVectorRef-era entries are not supported (ENFORCER-072 / PERSIST-005).\nArchive or remove the old Wanxiangshu runtime journal before starting."

    /// Field and case names that only a pre-0.5.0 journal can contain.
    ///
    /// Two groups, both fatal:
    ///
    /// Fields — the old Fallback projection stored Dead/Failures counters and
    /// model ids. Guessing a modulo-4 cursor from them would be inventing
    /// history, and VERIFY-006 lists a journal carrying model ids as a No-Go.
    ///
    /// Case names — the facts replaced in this migration. Without them the
    /// decoder would fail with an opaque union error, so the operator would see
    /// "cannot parse line 3" instead of "this journal predates 0.5.0". A precise
    /// diagnosis is the difference between archiving the file and debugging the
    /// codec.
    let private pre050Markers =
        [| "\"FailuresOnCurrentSide\""
           "\"IsDead\""
           "\"TotalFailures\""
           "\"BaseModelID\""
           "\"BaseProviderID\""
           "\"EffectiveModelID\""
           "\"EffectiveProviderID\""
           "\"PluginPromptAccepted\""
           "\"HumanPromptAccepted\""
           "\"GuardPromptAccepted\""
           "\"InteractionRepairClaimed\""
           "\"ReviewConfirmedIdle\""
           "\"AgentLinked\""
           "\"AgentForked\""
           "\"AgentUnlinked\""
           "\"OrchestratorManagerJobCreated\""
           "\"OrchestratorCandidateRegistered\""
           "\"OrchestratorPublished\""
           "\"OrchestratorRejected\""
           "\"OrchestratorRebased\""
           "\"OrchestratorConflictDetected\""
           "\"OrchestratorPreRebaseReviewConfirmed\""
           "\"OrchestratorPostRebaseReviewConfirmed\""
           "\"OrchestratorPublishClaimed\""
           "\"EnforcementCycleCommitted\""
           // 0.5.1 generic durable-effect union — never written in production;
           // refuse with the migration message rather than an opaque DU error.
           "\"DurableEffectRequested\""
           "\"DurableEffectAccepted\"" |]

    /// Replay keeps the modern hot path free of legacy detection. Only after a
    /// Journal decode has already failed do we classify a known historical
    /// shape as ignorable compatibility noise.
    let isIgnoredLegacyDecodeError (error: string) : bool =
        pre050Markers
        |> Array.exists (fun marker ->
            let name = marker.Trim('"')
            error.IndexOf(name, StringComparison.Ordinal) >= 0)
        || error.IndexOf("BlogEntryCommitted", StringComparison.Ordinal) >= 0
        || error.IndexOf("ScoreVectorRef", StringComparison.Ordinal) >= 0
        || error.IndexOf("TipRuleId", StringComparison.Ordinal) >= 0
        || error.IndexOf("PairProgrammingGuidelineAppended", StringComparison.Ordinal) >= 0

    // retention horizon: durable-events HOW §223 (pre-0.5.0 markers) — decode-only, delete when external census proves 0
    let containsLegacyFallbackFields (json: string) =
        pre050Markers
        |> Array.exists (fun marker -> json.IndexOf(marker, StringComparison.Ordinal) >= 0)

    /// ENFORCER-072: BlogObservationCommitted / legacy BlogEntryCommitted carrying
    /// ScoreVectorRef, or lacking TipRuleId, is a pre-tip-v2 shape. Explicit refuse
    /// — no max-score migration. Check both tags so old journals still fail closed.
    // retention horizon: durable-events HOW §224 (tip-v2 clean break) — decode-only, delete when external census proves 0
    let containsLegacyScoreVectorEntry (json: string) =
        let isObservationCommit =
            json.IndexOf("\"BlogObservationCommitted\"", StringComparison.Ordinal) >= 0
            || json.IndexOf("\"BlogEntryCommitted\"", StringComparison.Ordinal) >= 0

        if not isObservationCommit then
            false
        else
            let hasScoreVector =
                json.IndexOf("\"ScoreVectorRef\"", StringComparison.Ordinal) >= 0

            let hasTipRuleId = json.IndexOf("\"TipRuleId\"", StringComparison.Ordinal) >= 0
            hasScoreVector || not hasTipRuleId

    /// HOST-013 anchored replay clean break: the legacy unanchored
    /// `PairProgrammingGuidelineAppended` carried only Ordinal / CallId /
    /// MarkerText. Its transcript position cannot be recovered without a
    /// heuristic ordinal≈batch guess, which would re-create the exact prefix
    /// bug this change fixes. Refuse — never migrate by guessing (cache §13).
    // retention horizon: durable-events HOW §226 (EXEC-009 HandleCompleted missing completion) — decode-only, delete when external census proves 0
    let containsHandleCompletedMissingCompletionFields (json: string) =
        let isHandleCompleted =
            json.IndexOf("\"HandleCompleted\"", StringComparison.Ordinal) >= 0

        isHandleCompleted
        && (json.IndexOf("\"CompletionRef\"", StringComparison.Ordinal) < 0
            || json.IndexOf("\"CompletionDigest\"", StringComparison.Ordinal) < 0)

    // retention horizon: durable-events HOW §225 (HOST-013 unanchored guideline) — decode-only, delete when external census proves 0
    let containsLegacyUnanchoredGuideline (json: string) =
        json.IndexOf("\"PairProgrammingGuidelineAppended\"", StringComparison.Ordinal)
        >= 0

    let legacyGuidelineCleanBreakMessage =
        "Wanxiangshu HOST-013 requires anchored PairProgrammingGuidelineAnchored facts; legacy unanchored PairProgrammingGuidelineAppended journals are not supported (anchored replay clean break).\nArchive or remove the old Wanxiangshu runtime journal before starting."

    /// PERSIST-001: the fact's own bytes must not depend on the machine that
    /// reads them. Embedded DateTimeOffset fields (RuntimeStarted.StartedAt,
    /// HandleAbandoned.AbandonedAt, HostTurnObserved.ObservedAt) share the
    /// envelope-`ObservedAt` hazard: the DECODER attaches the reader's local
    /// offset, so a fact serialized as `+00:00`, decoded and re-serialized on a
    /// `TZ=Asia/Shanghai` host would render `+08:00` for the same instant, and a
    /// byte comparison of two replicas would report a difference that is not one.
    /// Mirror `Envelope.serialize`: pin to offset zero before encoding, and on
    /// read so the decoded value also carries `+00:00`.
    /// `ToOffset TimeSpan.Zero` rather than `ToUniversalTime()`: Fable's
    /// `toUniversalTime` leaves the emitted value's `offset` field `undefined`.
    /// Keep this step in sync with any new fact case that embeds a DateTimeOffset.
    let private pinToUtc (fact: Fact) : Fact =
        match fact with
        | Runtime(RuntimeStarted started) ->
            Runtime(
                RuntimeStarted
                    {| started with
                        StartedAt = started.StartedAt.ToOffset TimeSpan.Zero |}
            )
        | Agent(AgentFact.Execution(ExecutionFactCases.HandleAbandoned payload)) ->
            Agent(
                ExecutionFact.HandleAbandoned
                    {| payload with
                        AbandonedAt = payload.AbandonedAt.ToOffset TimeSpan.Zero |}
            )
        | Agent(AgentFact.Execution(ExecutionFactCases.HostTurnObserved payload)) ->
            Agent(
                ExecutionFact.HostTurnObserved
                    {| payload with
                        ObservedAt = payload.ObservedAt.ToOffset TimeSpan.Zero |}
            )
        | Agent(AgentFact.Host(HostFactCases.SessionStartedAtBound payload)) ->
            Agent(
                HostFact.SessionStartedAtBound
                    {| payload with
                        StartedAt = payload.StartedAt.ToOffset TimeSpan.Zero |}
            )
        | other -> other

    let private decodeMagicTodoCanonical canonical =
        match MagicTodoFactCodec.tryDecode canonical with
        | Ok fact -> Fact.MagicTodo fact
        | Error reason -> failwith ("invalid MagicTodo canonical payload: " + reason)

    let private decodeMagicTodoFact decoder json =
        match Decode.fromString decoder json with
        | Ok fact -> Some fact
        | Error _ -> None

    let private tryDecodeMagicTodo (json: string) : Fact option =
        let decoder: Decoder<Fact> =
            Decode.object (fun get ->
                match get.Optional.Field "MagicTodo" Decode.string with
                | Some canonical -> decodeMagicTodoCanonical canonical
                | None -> failwith "not a MagicTodo fact")

        try
            decodeMagicTodoFact decoder json
        with _ ->
            None

    let serializeFact (fact: Fact) : string =
        match fact with
        | MagicTodo magicTodo ->
            Encode.Auto.toString (0, {| MagicTodo = MagicTodoFactCodec.encode magicTodo |}, extra = extra)
        | other -> Encode.Auto.toString (0, pinToUtc other, extra = extra)

    let private deserializeCurrentFact json =
        match tryDecodeMagicTodo json with
        | Some fact -> Ok fact
        | None -> Decode.Auto.fromString<Fact> (json, extra = extra) |> Result.map pinToUtc

    let validateFact (fact: Fact) : Result<Fact, string> =
        let validate caseName schemaVersion =
            if schemaVersion = 1 then
                Ok fact
            else
                Error(sprintf "ChatExecution %s schema version is unsupported: %d" caseName schemaVersion)

        match fact with
        | Agent(AgentFact.ChatExecution(ChatExecutionFactCases.Accepted payload)) ->
            validate "Accepted" payload.SchemaVersion
        | Agent(AgentFact.ChatExecution(ChatExecutionFactCases.ProviderStarted payload)) ->
            validate "ProviderStarted" payload.SchemaVersion
        | Agent(AgentFact.ChatExecution(ChatExecutionFactCases.Terminal payload)) ->
            validate "Terminal" payload.SchemaVersion
        | _ -> Ok fact

    let deserializeFact (json: string) : Result<Fact, string> =
        if containsLegacyFallbackFields json then
            Error pre050MigrationMessage
        elif containsLegacyScoreVectorEntry json then
            Error tipV2CleanBreakMessage
        elif containsLegacyUnanchoredGuideline json then
            Error legacyGuidelineCleanBreakMessage
        elif containsHandleCompletedMissingCompletionFields json then
            Error "HandleCompleted requires CompletionRef and CompletionDigest; decode migration is not supported."
        else
            deserializeCurrentFact json |> Result.bind validateFact
