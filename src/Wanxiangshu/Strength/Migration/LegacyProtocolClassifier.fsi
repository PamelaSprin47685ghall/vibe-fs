namespace Wanxiangshu.Strength.Migration

open Thoth.Json

/// DELEGATE: offline recognition of the pre-delegation Strength protocol.
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
    val legacyStrengthEventTypes: string list

    /// Wire names that exist only after the clean break.
    val currentOnlyStrengthEventTypes: string list

    /// Verdict from the event type plus the legacy-only payload fields.
    val classifyPayload: eventType: string -> payload: JsonValue -> LegacyProtocolVerdict

    /// JS-native boundary form: the payload arrives as canonical JSON text.
    val classifyEnvelopeJson: eventType: string -> payloadJson: string -> LegacyClassification

    val isLegacyStrength: eventType: string -> payload: JsonValue -> bool

    /// The refusal an unmigrated store produces before any fold runs.
    val refusalMessage: reason: string -> string
