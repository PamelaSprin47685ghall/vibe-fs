namespace Wanxiangshu.Persistence.Journal

open Wanxiangshu.Composition.Durable

open System
open Thoth.Json
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact

/// Fact serialization (PERSIST-005).
module FactCodec =

    let private extra = Extra.empty |> Extra.withInt64

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

    let containsLegacyFallbackFields (json: string) =
        pre050Markers
        |> Array.exists (fun marker -> json.IndexOf(marker, StringComparison.Ordinal) >= 0)

    /// ENFORCER-072: BlogObservationCommitted / legacy BlogEntryCommitted carrying
    /// ScoreVectorRef, or lacking TipRuleId, is a pre-tip-v2 shape. Explicit refuse
    /// — no max-score migration. Check both tags so old journals still fail closed.
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

    /// Dual-decode: physical writes use BlogObservationCommitted /
    /// BlogObservationsSquashed; journals may still carry the pre-cutover tags.
    let rewriteLegacyObservationTags (json: string) : string =
        json
            .Replace("\"BlogEntryCommitted\"", "\"BlogObservationCommitted\"")
            .Replace("\"BlogSquashCommitted\"", "\"BlogObservationsSquashed\"")

    /// HOST-013 anchored replay clean break: the legacy unanchored
    /// `PairProgrammingGuidelineAppended` carried only Ordinal / CallId /
    /// MarkerText. Its transcript position cannot be recovered without a
    /// heuristic ordinal≈batch guess, which would re-create the exact prefix
    /// bug this change fixes. Refuse — never migrate by guessing (cache §13).
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

    let serializeFact (fact: Fact) : string =
        Encode.Auto.toString (0, pinToUtc fact, extra = extra)

    /// Evidence → Decision: one JSON string step after an opening quote.
    let private skipJsonString (json: string) (startAfterQuote: int) : int =
        let rec skipString j =
            if j >= json.Length then j
            elif json.[j] = '\\' then skipString (j + 2)
            elif json.[j] = '"' then j + 1
            else skipString (j + 1)

        skipString startAfterQuote

    type private ObjectScanStep =
        | Exhausted
        | Found of closeIndex: int
        | Advance of nextIndex: int * nextDepth: int

    /// Evidence → Decision: one brace-depth step inside an auto-encoded payload object.
    let private stepJsonObjectScan (json: string) (i: int) (depth: int) : ObjectScanStep =
        if i >= json.Length then
            Exhausted
        elif json.[i] = '{' then
            Advance(i + 1, depth + 1)
        elif json.[i] = '}' && depth = 1 then
            Found i
        elif json.[i] = '}' then
            Advance(i + 1, depth - 1)
        elif json.[i] = '"' then
            Advance(skipJsonString json (i + 1), depth)
        else
            Advance(i + 1, depth)

    let private findMatchingObjectClose (json: string) (openBrace: int) : int option =
        let rec loop i depth =
            match stepJsonObjectScan json i depth with
            | Exhausted -> None
            | Found close -> Some close
            | Advance(nextIndex, nextDepth) -> loop nextIndex nextDepth

        loop (openBrace + 1) 1

    /// Locate the first object after a DU case marker; None when marker/brace/close missing.
    let private tryObjectCloseAfterMarker (json: string) (caseMarker: string) : int option =
        let start = json.IndexOf(caseMarker, StringComparison.Ordinal)

        if start < 0 then None
        elif json.IndexOf('{', start) < 0 then None
        else findMatchingObjectClose json (json.IndexOf('{', start))

    /// Insert auto-encoded optional fields before the payload object's closing brace.
    let private insertFieldsBeforeObjectClose (json: string) (caseMarker: string) (fields: string) : string =
        match tryObjectCloseAfterMarker json caseMarker with
        | None -> json
        | Some close ->
            let before = json.Substring(0, close)
            let after = json.Substring(close)
            let trimmed = before.TrimEnd()

            let needsComma =
                trimmed.Length > 0
                && trimmed.[trimmed.Length - 1] <> '{'
                && trimmed.[trimmed.Length - 1] <> ','

            let piece = if needsComma then "," + fields else fields
            before + piece + after

    /// 0.5.1 → 0.5.2: `HandleCompleted` gained `CompletionRef` / `CompletionDigest`.
    /// Old lines lack both keys; inject null so Decode maps them to `None`.
    let private migrateHandleCompleted (json: string) : string =
        if json.IndexOf("\"HandleCompleted\"", StringComparison.Ordinal) < 0 then
            json
        elif json.IndexOf("\"CompletionRef\"", StringComparison.Ordinal) >= 0 then
            json
        else
            // Anonymous-record payload is the object after the case name. Insert the
            // two optional fields before its closing brace of that object. A single
            // first-object close is enough: HandleCompleted's payload has no nested
            // objects that open before the outer close in the auto-encoded shape.
            insertFieldsBeforeObjectClose json "\"HandleCompleted\"" "\"CompletionRef\":null,\"CompletionDigest\":null"

    /// GLORY-002 / SURFACE-006: `HandleLinked` gained `Ownership`. Old lines
    /// lack the key; inject `DurableParentHandle` so replay keeps the pre-change
    /// meaning (every legacy handle was parent-visible).
    let private migrateHandleOwnership (json: string) : string =
        if json.IndexOf("\"HandleLinked\"", StringComparison.Ordinal) < 0 then
            json
        elif json.IndexOf("\"Ownership\"", StringComparison.Ordinal) >= 0 then
            json
        else
            insertFieldsBeforeObjectClose json "\"HandleLinked\"" "\"Ownership\":\"DurableParentHandle\""

    /// EXEC-002: `HandleLinked` gained a provider presentation identity (`Byname`)
    /// distinct from the Host machine binding (`TargetAgent`). Historical facts
    /// predate that distinction, so an empty Byname asks the fold to fall back to
    /// TargetAgent without fabricating a new logical identity during replay.
    let private migrateHandleByname (json: string) : string =
        if json.IndexOf("\"HandleLinked\"", StringComparison.Ordinal) < 0 then
            json
        elif json.IndexOf("\"Byname\"", StringComparison.Ordinal) >= 0 then
            json
        else
            insertFieldsBeforeObjectClose json "\"HandleLinked\"" "\"Byname\":\"\""

    /// EXEC-029: historical ManagerJobCreated facts predate provider road names.
    /// Empty Byname keeps replay compatible; the projection falls back to the
    /// persisted ManagerAgent for those old facts only.
    let private migrateManagerJobByname (json: string) : string =
        if json.IndexOf("\"ManagerJobCreated\"", StringComparison.Ordinal) < 0 then
            json
        elif json.IndexOf("\"Byname\"", StringComparison.Ordinal) >= 0 then
            json
        else
            insertFieldsBeforeObjectClose json "\"ManagerJobCreated\"" "\"Byname\":\"\""

    let deserializeFact (json: string) : Result<Fact, string> =
        if containsLegacyFallbackFields json then
            Error pre050MigrationMessage
        elif containsLegacyScoreVectorEntry json then
            Error tipV2CleanBreakMessage
        elif containsLegacyUnanchoredGuideline json then
            Error legacyGuidelineCleanBreakMessage
        else
            Decode.Auto.fromString<Fact> (
                json
                |> migrateHandleCompleted
                |> migrateHandleOwnership
                |> migrateHandleByname
                |> migrateManagerJobByname
                |> rewriteLegacyObservationTags,
                extra = extra
            )
            |> Result.map pinToUtc
