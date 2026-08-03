namespace Wanxiangshu.Next.Domain

/// ENFORCER-170: Rule Catalog pure types + validation.
/// Resource layer loads JSON; Domain never reads files.
type EnforcerRule =
    { RuleId: string
      FieldName: string
      Family: string
      ScoreWhen: string
      Nudge: string
      CatalogOrdinal: int }

module EnforcerCatalog =

    let private RequiredRuleCount = 120

    let private isNonEmpty (s: string) = not (isNull s) && s.Trim().Length > 0

    let private duplicates (keys: string list) =
        keys
        |> List.groupBy id
        |> List.choose (fun (k, group) -> if List.length group > 1 then Some k else None)

    /// schemaVersion=1, exactly 120 rules, unique id/field, ordinals 1..120, non-empty ScoreWhen/Nudge.
    let validate (schemaVersion: int) (rules: EnforcerRule list) : Result<EnforcerRule list, string> =
        if schemaVersion <> 1 then
            Error(sprintf "enforcer catalog schemaVersion must be 1, got %d" schemaVersion)
        elif List.length rules <> RequiredRuleCount then
            Error(
                sprintf "enforcer catalog must contain exactly %d rules, got %d" RequiredRuleCount (List.length rules)
            )
        else
            let idDupes = rules |> List.map (fun r -> r.RuleId) |> duplicates
            let fieldDupes = rules |> List.map (fun r -> r.FieldName) |> duplicates

            if not (List.isEmpty idDupes) then
                Error(sprintf "enforcer catalog duplicate rule id: %s" (String.concat ", " idDupes))
            elif not (List.isEmpty fieldDupes) then
                Error(sprintf "enforcer catalog duplicate field: %s" (String.concat ", " fieldDupes))
            else
                let ordered = rules |> List.sortBy (fun r -> r.CatalogOrdinal)
                let ordinals = ordered |> List.map (fun r -> r.CatalogOrdinal)
                let expected = [ 1..RequiredRuleCount ]

                if ordinals <> expected then
                    Error "enforcer catalog catalogOrdinal must be contiguous 1..120"
                else
                    match
                        ordered
                        |> List.tryFind (fun r ->
                            not (isNonEmpty r.RuleId)
                            || not (isNonEmpty r.FieldName)
                            || not (isNonEmpty r.Family)
                            || not (isNonEmpty r.ScoreWhen)
                            || not (isNonEmpty r.Nudge))
                    with
                    | Some bad -> Error(sprintf "enforcer catalog empty text on rule ordinal %d" bad.CatalogOrdinal)
                    | None -> Ok ordered

    /// Provider codec catalog: FieldName, RuleId, CatalogOrdinal.
    let triples (rules: EnforcerRule list) : (string * string * int) list =
        rules |> List.map (fun r -> r.FieldName, r.RuleId, r.CatalogOrdinal)
