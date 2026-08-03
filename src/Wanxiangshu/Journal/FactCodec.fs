namespace Wanxiangshu.Journal

open System
open Thoth.Json
open Wanxiangshu.Kernel.Fact

/// Fact serialization (PERSIST-005).
module FactCodec =

    let private extra = Extra.empty |> Extra.withInt64

    let pre050MigrationMessage =
        "Wanxiangshu 0.5.0 does not support pre-0.5.0 runtime journals.\nArchive or remove the old Wanxiangshu runtime journal before starting."

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

    let serializeFact (fact: Fact) : string =
        Encode.Auto.toString (0, fact, extra = extra)

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
            let marker = "\"HandleCompleted\""
            let start = json.IndexOf(marker, StringComparison.Ordinal)

            if start < 0 then
                json
            else
                let brace = json.IndexOf('{', start)

                if brace < 0 then
                    json
                else
                    let rec findClose (i: int) (depth: int) =
                        if i >= json.Length then
                            -1
                        else
                            match json.[i] with
                            | '{' -> findClose (i + 1) (depth + 1)
                            | '}' when depth = 1 -> i
                            | '}' -> findClose (i + 1) (depth - 1)
                            | '"' ->
                                let rec skipString j =
                                    if j >= json.Length then j
                                    elif json.[j] = '\\' then skipString (j + 2)
                                    elif json.[j] = '"' then j + 1
                                    else skipString (j + 1)

                                findClose (skipString (i + 1)) depth
                            | _ -> findClose (i + 1) depth

                    match findClose (brace + 1) 1 with
                    | -1 -> json
                    | close ->
                        let insert = "\"CompletionRef\":null,\"CompletionDigest\":null"
                        let before = json.Substring(0, close)
                        let after = json.Substring(close)
                        // Keep valid JSON whether the payload already has fields.
                        let needsComma =
                            let trimmed = before.TrimEnd()

                            trimmed.Length > 0
                            && trimmed.[trimmed.Length - 1] <> '{'
                            && trimmed.[trimmed.Length - 1] <> ','

                        let piece = if needsComma then "," + insert else insert
                        before + piece + after

    let deserializeFact (json: string) : Result<Fact, string> =
        if containsLegacyFallbackFields json then
            Error pre050MigrationMessage
        else
            Decode.Auto.fromString<Fact> (migrateHandleCompleted json, extra = extra)
