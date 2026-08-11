namespace Wanxiangshu.Domain

/// Rulebook folder SSOT: directory basename = TipName = provider field = durable RuleId.
/// Bridge fields (ScoreWhen/Nudge/Family/CatalogOrdinal) keep existing consumers compiling
/// until Observation EventStore vocabulary lands; they are derived at load, not authored JSON.
type EnforcerRule =
    {
        /// Directory basename = TipIdentity = provider enum value.
        Name: string
        /// Full text of resources/enforcer/<name>/enforcer.md
        EnforcerText: string
        /// Full text of resources/enforcer/<name>/main.md
        MainText: string
        /// Durable tip id; clean break = Name (no enforcement-a01).
        RuleId: string
        /// Provider-facing tip enum value; = Name.
        FieldName: string
        /// Optional authoring family tag parsed from stub header; not a second identity.
        Family: string
        /// Detection seed (enforcer ScoreWhen section or full enforcer body).
        ScoreWhen: string
        /// Main guidance seed (Nudge / What to do / main body excerpt).
        Nudge: string
        /// Lexical folder order index 1..N (deterministic enum/prompt order only).
        CatalogOrdinal: int
    }

/// ENFORCER-004 / 021: tip identity after rulebook resolve.
/// FieldName is provider-facing; RuleId is durable — both equal TipName after folder cutover.
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

    /// schemaVersion kept for test/facade compatibility (folder loader always passes 1).
    /// non-empty rules, unique Name/RuleId/FieldName, ordinals 1..N, non-empty texts.
    let validate (schemaVersion: int) (rules: EnforcerRule list) : Result<EnforcerRule list, string> =
        let n = List.length rules

        if schemaVersion <> 1 then
            Error(sprintf "enforcer catalog schemaVersion must be 1, got %d" schemaVersion)
        elif n = 0 then
            Error "enforcer catalog must contain at least one rule"
        else
            let nameDupes = rules |> List.map (fun r -> r.Name) |> duplicates
            let idDupes = rules |> List.map (fun r -> r.RuleId) |> duplicates
            let fieldDupes = rules |> List.map (fun r -> r.FieldName) |> duplicates

            if not (List.isEmpty nameDupes) then
                Error(sprintf "enforcer catalog duplicate rule name: %s" (String.concat ", " nameDupes))
            elif not (List.isEmpty idDupes) then
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
                            not (isNonEmpty r.Name)
                            || not (isNonEmpty r.RuleId)
                            || not (isNonEmpty r.FieldName)
                            || not (isNonEmpty r.Family)
                            || not (isNonEmpty r.ScoreWhen)
                            || not (isNonEmpty r.Nudge)
                            || not (isNonEmpty r.EnforcerText)
                            || not (isNonEmpty r.MainText)
                            || r.Name <> r.RuleId
                            || r.Name <> r.FieldName)
                    with
                    | Some bad ->
                        Error(
                            sprintf
                                "enforcer catalog empty text or identity mismatch on rule ordinal %d"
                                bad.CatalogOrdinal
                        )
                    | None -> Ok ordered

    /// ENFORCER-021: exact field/TipName → rule. No fuzzy match (ENFORCER-024).
    let tryFindByField (field: string) (rules: EnforcerRule list) : EnforcerRule option =
        if isNull field then
            None
        else
            let trimmed = field.Trim()

            if trimmed.Length = 0 then
                None
            else
                rules |> List.tryFind (fun r -> r.FieldName = trimmed || r.Name = trimmed)

    /// Provider enum values: TipName list in lexical (CatalogOrdinal) order.
    let fieldNames (rules: EnforcerRule list) : string list =
        rules
        |> List.sortBy (fun r -> r.CatalogOrdinal)
        |> List.map (fun r -> r.FieldName)
