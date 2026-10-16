namespace Wanxiangshu.Enforcer

/// Rulebook folder SSOT: directory basename = RuleId = FieldName (bd [003]/[005]).
/// Rule payload is full enforcer.md + main.md texts; LexicalOrder is folder order 1..N.
type EnforcerRule =
    {
        /// Directory basename; library identity (= RuleId = FieldName), not a provider enum.
        Name: string
        /// Full text of resources/enforcer/<name>/enforcer.md
        EnforcerText: string
        /// Full text of resources/enforcer/<name>/main.md
        MainText: string
        /// Durable tip id; clean break = Name (no enforcement-a01).
        RuleId: string
        /// Library field name; = Name. Blogger tipName is free-form: it may match
        /// this exactly (tryFindByField) or be a new name absent from the library.
        FieldName: string
        /// Lexical folder order index 1..N (deterministic enum/prompt order only).
        LexicalOrder: int
    }

module EnforcerCatalog =

    let private isNonEmpty (s: string) = not (isNull s) && s.Trim().Length > 0

    let private duplicates (keys: string list) =
        keys
        |> List.groupBy id
        |> List.choose (fun (k, group) -> if List.length group > 1 then Some k else None)

    /// schemaVersion kept for test/facade compatibility (folder loader always passes 1).
    /// non-empty rules, unique Name/RuleId/FieldName, order 1..N, non-empty texts.
    let private validateDuplicates (rules: EnforcerRule list) : Result<unit, string> =
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
            Ok()

    let private validateOrder (rules: EnforcerRule list) : Result<EnforcerRule list, string> =
        let ordered = rules |> List.sortBy (fun r -> r.LexicalOrder)
        let orders = ordered |> List.map (fun r -> r.LexicalOrder)
        let expected = [ 1 .. List.length rules ]

        if orders <> expected then
            Error(sprintf "enforcer catalog lexicalOrder must be contiguous 1..%d" (List.length rules))
        else
            Ok ordered

    let private validateContents (ordered: EnforcerRule list) : Result<EnforcerRule list, string> =
        match
            ordered
            |> List.tryFind (fun r ->
                not (isNonEmpty r.Name)
                || not (isNonEmpty r.RuleId)
                || not (isNonEmpty r.FieldName)
                || not (isNonEmpty r.EnforcerText)
                || not (isNonEmpty r.MainText)
                || r.Name <> r.RuleId
                || r.Name <> r.FieldName)
        with
        | Some bad ->
            Error(sprintf "enforcer catalog empty text or identity mismatch on rule ordinal %d" bad.LexicalOrder)
        | None -> Ok ordered

    let validate (schemaVersion: int) (rules: EnforcerRule list) : Result<EnforcerRule list, string> =
        let n = List.length rules

        if schemaVersion <> 1 then
            Error(sprintf "enforcer catalog schemaVersion must be 1, got %d" schemaVersion)
        elif n = 0 then
            Error "enforcer catalog must contain at least one rule"
        else
            validateDuplicates rules
            |> Result.bind (fun () -> validateOrder rules)
            |> Result.bind validateContents


    /// ENFORCER-021: exact field/TipName → rule. No fuzzy match (ENFORCER-024).
    let tryFindByField (field: string) (rules: EnforcerRule list) : EnforcerRule option =
        if isNull field || field.Trim().Length = 0 then
            None
        else
            let trimmed = field.Trim()
            rules |> List.tryFind (fun r -> r.FieldName = trimmed || r.Name = trimmed)

    /// Library TipName list (= field names) in lexical (LexicalOrder) order;
    /// provider-side tipName is free-form, not an enum domain.
    let fieldNames (rules: EnforcerRule list) : string list =
        rules
        |> List.sortBy (fun r -> r.LexicalOrder)
        |> List.map (fun r -> r.FieldName)
