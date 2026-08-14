namespace Wanxiangshu.Enforcer

open Wanxiangshu.Context.Prefix
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt

/// Rulebook folder SSOT: directory basename = TipName = provider field = durable RuleId.
/// Rule payload is full enforcer.md + main.md texts; LexicalOrder is folder order 1..N.
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
        /// Lexical folder order index 1..N (deterministic enum/prompt order only).
        LexicalOrder: int
    }

/// ENFORCER-004 / 021: tip identity after rulebook resolve.
/// FieldName is provider-facing; RuleId is durable — both equal TipName after folder cutover.
type EnforcerTip =
    { RuleId: string
      FieldName: string
      LexicalOrder: int }

module EnforcerTip =

    let ofRule (rule: EnforcerRule) : EnforcerTip =
        { RuleId = rule.RuleId
          FieldName = rule.FieldName
          LexicalOrder = rule.LexicalOrder }

module EnforcerCatalog =

    let private isNonEmpty (s: string) = not (isNull s) && s.Trim().Length > 0

    let private duplicates (keys: string list) =
        keys
        |> List.groupBy id
        |> List.choose (fun (k, group) -> if List.length group > 1 then Some k else None)

    /// schemaVersion kept for test/facade compatibility (folder loader always passes 1).
    /// non-empty rules, unique Name/RuleId/FieldName, order 1..N, non-empty texts.
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
                let ordered = rules |> List.sortBy (fun r -> r.LexicalOrder)
                let orders = ordered |> List.map (fun r -> r.LexicalOrder)
                let expected = [ 1..n ]

                if orders <> expected then
                    Error(sprintf "enforcer catalog lexicalOrder must be contiguous 1..%d" n)
                else
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
                        Error(
                            sprintf
                                "enforcer catalog empty text or identity mismatch on rule ordinal %d"
                                bad.LexicalOrder
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

    /// Provider enum values: TipName list in lexical (LexicalOrder) order.
    let fieldNames (rules: EnforcerRule list) : string list =
        rules
        |> List.sortBy (fun r -> r.LexicalOrder)
        |> List.map (fun r -> r.FieldName)
