namespace Wanxiangshu.Next.Journal

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel.Fact

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
           "\"OrchestratorPublishClaimed\"" |]

    let containsLegacyFallbackFields (json: string) =
        pre050Markers
        |> Array.exists (fun marker -> json.IndexOf(marker, StringComparison.Ordinal) >= 0)

    let serializeFact (fact: Fact) : string =
        Encode.Auto.toString (0, fact, extra = extra)

    let deserializeFact (json: string) : Result<Fact, string> =
        if containsLegacyFallbackFields json then
            Error pre050MigrationMessage
        else
            Decode.Auto.fromString<Fact> (json, extra = extra)
