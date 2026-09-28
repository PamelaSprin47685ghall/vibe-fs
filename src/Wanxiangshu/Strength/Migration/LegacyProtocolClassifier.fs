namespace Wanxiangshu.Strength.Migration

open Thoth.Json

/// DELEGATE: offline recognition of the pre-delegation Strength protocol.
///
/// The old protocol persisted four candidate facts
/// (StrengthCandidatePrepared / CandidatePromoted / FramesTraced /
/// CandidateAbandoned) and three of those type names were reused by the
/// current protocol with a different causal shape, so a verdict is decided
/// from the event type plus the legacy-only payload fields — never from the
/// type name alone. Promoted / Traced / Abandoned are shape-identical to
/// their current namesakes; the migration planner resolves them per decision.
[<RequireQualifiedAccess>]
type LegacyProtocolVerdict =
    | NotStrength
    | CurrentProtocol
    | LegacyStrength of reason: string

/// JS-native projection of `LegacyProtocolVerdict` at the tool boundary.
[<RequireQualifiedAccess>]
type LegacyClassification = { Kind: string; Reason: string option }

[<RequireQualifiedAccess>]
module LegacyProtocolClassifier =

    /// Wire names of the four pre-delegation Strength fact types.
    let legacyStrengthEventTypes =
        [ "StrengthCandidatePrepared"
          "StrengthCandidatePromoted"
          "StrengthFramesTraced"
          "StrengthCandidateAbandoned" ]

    /// Wire names that exist only after the clean break.
    let currentOnlyStrengthEventTypes =
        [ "DelegationRequested"
          "DelegationBound"
          "DelegationClosed"
          "DelegationHistoryImported" ]

    let private isLegacyBudget (budget: string option) =
        match budget with
        | Some value -> value = "K0" || value = "K1" || value = "K2"
        | None -> false

    let private budgetDecoder =
        Decode.object (fun get -> get.Optional.Field "budget" Decode.string)

    /// Definitive legacy evidence in a Prepared payload is the tier budget
    /// string the current protocol removed. The candidate type name alone is
    /// not evidence: the current protocol reuses it.
    let classifyPayload (eventType: string) (payload: JsonValue) : LegacyProtocolVerdict =
        if currentOnlyStrengthEventTypes |> List.contains eventType then
            LegacyProtocolVerdict.CurrentProtocol
        elif not (legacyStrengthEventTypes |> List.contains eventType) then
            LegacyProtocolVerdict.NotStrength
        elif eventType <> "StrengthCandidatePrepared" then
            LegacyProtocolVerdict.CurrentProtocol
        else
            match Decode.fromValue "$" budgetDecoder payload with
            | Ok budget when isLegacyBudget budget ->
                LegacyProtocolVerdict.LegacyStrength "legacy StrengthCandidatePrepared carries tier budget string"
            | _ -> LegacyProtocolVerdict.CurrentProtocol

    let private classifyEnvelopeJsonVerdict (eventType: string) (payloadJson: string) : LegacyProtocolVerdict =
        if eventType <> "StrengthCandidatePrepared" then
            classifyPayload eventType (unbox<JsonValue> null)
        else
            match Decode.fromString budgetDecoder payloadJson with
            | Ok budget when isLegacyBudget budget ->
                LegacyProtocolVerdict.LegacyStrength "legacy StrengthCandidatePrepared carries tier budget string"
            | _ -> LegacyProtocolVerdict.CurrentProtocol

    /// The JS-native boundary form: the payload arrives as canonical JSON text.
    let classifyEnvelopeJson (eventType: string) (payloadJson: string) : LegacyClassification =
        match classifyEnvelopeJsonVerdict eventType payloadJson with
        | LegacyProtocolVerdict.NotStrength -> { Kind = "not-strength"; Reason = None }
        | LegacyProtocolVerdict.CurrentProtocol -> { Kind = "current"; Reason = None }
        | LegacyProtocolVerdict.LegacyStrength reason ->
            { Kind = "legacy"
              Reason = Some reason }

    let isLegacyStrength (eventType: string) (payload: JsonValue) : bool =
        match classifyPayload eventType payload with
        | LegacyProtocolVerdict.LegacyStrength _ -> true
        | _ -> false

    /// The refusal an unmigrated store produces before any fold runs.
    let refusalMessage (reason: string) : string =
        sprintf
            "this Strength store predates the readonly delegation protocol (%s); run the offline migration first: node scripts/migrate-delegation-history.mjs --backup <copy-of-.git> --input-version pre-delegation --contract-revision 1 --report <file> --execute; the runtime never migrates data"
            reason
