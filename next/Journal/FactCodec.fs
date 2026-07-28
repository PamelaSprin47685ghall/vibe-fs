namespace Wanxiangshu.Next.Journal

open System
open Thoth.Json
open Wanxiangshu.Next.Kernel.Fact

module FactCodec =

    let private extra = Extra.empty |> Extra.withInt64

    /// §15.4: refuse pre-0.5.0 journal payloads that still carry Dead/Failures
    /// projection fields rather than guessing a modulo-4 cursor migration.
    let pre050MigrationMessage =
        "Wanxiangshu 0.5.0 does not support pre-0.5.0 runtime journals.\nArchive or remove the old Wanxiangshu runtime journal before starting."

    let containsLegacyFallbackFields (json: string) =
        json.IndexOf("\"FailuresOnCurrentSide\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"IsDead\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"TotalFailures\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"BaseModelID\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"BaseProviderID\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"EffectiveModelID\"", StringComparison.Ordinal) >= 0
        || json.IndexOf("\"EffectiveProviderID\"", StringComparison.Ordinal) >= 0

    let serializeFact (fact: Fact) : string =
        Encode.Auto.toString (0, fact, extra = extra)

    let deserializeFact (json: string) : Result<Fact, string> =
        if containsLegacyFallbackFields json then
            Error pre050MigrationMessage
        else
            match Decode.Auto.fromString<Fact> (json, extra = extra) with
            | Ok f -> Ok f
            | Error err -> Error err
