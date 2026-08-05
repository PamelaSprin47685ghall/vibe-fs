namespace Wanxiangshu.Domain

/// ENFORCER-170: Rule Catalog pure types + validation.
/// Resource layer loads JSON; Domain never reads files.
type EnforcerRule =
    { RuleId: string
      FieldName: string
      Family: string
      ScoreWhen: string
      Nudge: string
      CatalogOrdinal: int }

/// ENFORCER-004 / 021: tip identity after catalog resolve.
/// FieldName is provider-facing only; RuleId is the durable identity.
type EnforcerTip =
    { RuleId: string
      FieldName: string
      CatalogOrdinal: int }

module EnforcerTip =

    let ofRule (rule: EnforcerRule) : EnforcerTip =
        { RuleId = rule.RuleId
          FieldName = rule.FieldName
          CatalogOrdinal = rule.CatalogOrdinal }

module EnforcerCatalog =

    let private isNonEmpty (s: string) = not (isNull s) && s.Trim().Length > 0

    let private duplicates (keys: string list) =
        keys
        |> List.groupBy id
        |> List.choose (fun (k, group) -> if List.length group > 1 then Some k else None)

    /// schemaVersion=1, non-empty rules, unique id/field, ordinals 1..N, non-empty ScoreWhen/Nudge.
    let validate (schemaVersion: int) (rules: EnforcerRule list) : Result<EnforcerRule list, string> =
        let n = List.length rules

        if schemaVersion <> 1 then
            Error(sprintf "enforcer catalog schemaVersion must be 1, got %d" schemaVersion)
        elif n = 0 then
            Error "enforcer catalog must contain at least one rule"
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
                let expected = [ 1..n ]

                if ordinals <> expected then
                    Error(sprintf "enforcer catalog catalogOrdinal must be contiguous 1..%d" n)
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

    /// ENFORCER-021: exact field → rule. No fuzzy match (ENFORCER-024).
    let tryFindByField (field: string) (rules: EnforcerRule list) : EnforcerRule option =
        if isNull field then
            None
        else
            let trimmed = field.Trim()

            if trimmed.Length = 0 then
                None
            else
                rules |> List.tryFind (fun r -> r.FieldName = trimmed)

    /// Provider enum values: FieldName list in catalog ordinal order.
    let fieldNames (rules: EnforcerRule list) : string list =
        rules
        |> List.sortBy (fun r -> r.CatalogOrdinal)
        |> List.map (fun r -> r.FieldName)
