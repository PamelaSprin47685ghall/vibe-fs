namespace Wanxiangshu.Infrastructure.Resources

open System
open Thoth.Json
open Wanxiangshu.Domain

/// Loads `resources/enforcer/catalog.json` on demand.
/// Missing / invalid / schema failure → throw (fail-fast).
/// Module import does not read files.
module EnforcerCatalogResource =

    let private ruleDecoder: Decoder<EnforcerRule> =
        Decode.object (fun get ->
            { RuleId = get.Required.Field "id" Decode.string
              FieldName = get.Required.Field "field" Decode.string
              Family = get.Required.Field "family" Decode.string
              ScoreWhen = get.Required.Field "scoreWhen" Decode.string
              Nudge = get.Required.Field "nudge" Decode.string
              CatalogOrdinal = get.Required.Field "catalogOrdinal" Decode.int })

    let private catalogDecoder: Decoder<int * EnforcerRule list> =
        Decode.object (fun get ->
            let schemaVersion = get.Required.Field "schemaVersion" Decode.int
            let rules = get.Required.Field "rules" (Decode.list ruleDecoder)
            schemaVersion, rules)

    let load () : EnforcerRule list =
        let relative = "enforcer/catalog.json"
        let raw = PackageResources.readText relative

        match Decode.fromString catalogDecoder raw with
        | Error err ->
            raise (InvalidOperationException(sprintf "enforcer catalog JSON invalid at resources/%s: %s" relative err))
        | Ok(schemaVersion, rules) ->
            match EnforcerCatalog.validate schemaVersion rules with
            | Error err ->
                raise (InvalidOperationException(sprintf "enforcer catalog invalid at resources/%s: %s" relative err))
            | Ok validated -> validated
