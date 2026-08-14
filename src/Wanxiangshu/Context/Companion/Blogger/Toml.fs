namespace Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction

/// CTX-013: what a Blogger delta part is, and how it renders as TOML.
///
/// Shared syntax lives in `SyntheticToml` (ARCH-010). This module owns the
/// Blogger-local schema only:
///
///   delta item     → `[[new_work_to_record]]` with a kind field
///   historic frame → `[[do_not_exec]] historic_frame = …` (assistant message body)
///
/// TOML is a one-way LLM-facing representation. The canonical digest is always
/// taken from `ProviderSemanticProjection`, never from this text. There is no
/// parser.
[<RequireQualifiedAccess>]
type BloggerDeltaPart =
    | TextPart of text: string
    | ReasoningPart of text: string
    | ToolCallPart of tool: string * canonicalArgs: string
    | ToolResultPart of text: string
    /// CTX-013: the Companion has no vision. The placeholder says "an image was
    /// here" and nothing about its content — no OCR, no caption, no digest.
    | ImageOmitted of mediaType: string option
    | MediaOmitted of mediaType: string option

/// One rendered part: which role it came from (message parts only) plus the part.
type BloggerDeltaItem =
    {
        Role: string
        Part: BloggerDeltaPart
        /// Set when CTX-013's third-level hard truncation cut this part's body.
        Truncated: bool
    }

[<RequireQualifiedAccess>]
module BloggerToml =

    /// CTX-013's fixed marker. A caller must not compose its own.
    let TruncationMarker = "[… content truncated by Companion delta 200 KiB limit …]"

    /// Historic-frame table name (message-layer wrapper around durable frame body).
    let DoNotExecTable = "do_not_exec"

    /// Delta-item table name (every part of the current backlog window).
    let NewWorkTable = "new_work_to_record"

    /// CTX-013 sparse schema: one table name for all delta parts; kind is the field.
    ///
    ///   [[new_work_to_record]]
    ///   user / assistant / tool / … = text
    ///
    ///   [[new_work_to_record]]
    ///   reasoning = text
    ///
    ///   [[new_work_to_record]]
    ///   tool_call = name
    ///   arguments = canonical JSON
    ///
    ///   [[new_work_to_record]]
    ///   tool_result = text
    ///
    ///   [[new_work_to_record]]
    ///   media_omitted = media_type | "untyped"
    ///
    /// Absent optional fields are omitted. `truncated = true` only when set.
    let renderItem (item: BloggerDeltaItem) : string =
        let field name value = SyntheticToml.field name value
        let value text = SyntheticToml.renderString text
        let truncated = if item.Truncated then [ field "truncated" "true" ] else []

        let entry fields =
            SyntheticToml.tableArrayEntry NewWorkTable (fields @ truncated)

        match item.Part with
        | BloggerDeltaPart.TextPart text ->
            // Role is the field name: user / assistant / tool / …
            entry [ field item.Role (value text) ]
        | BloggerDeltaPart.ReasoningPart text -> entry [ field "reasoning" (value text) ]
        | BloggerDeltaPart.ToolCallPart(tool, args) ->
            // `args` is already canonical from the Host codec; do not re-sort.
            entry [ field "tool_call" (value tool); field "arguments" (value args) ]
        | BloggerDeltaPart.ToolResultPart text -> entry [ field "tool_result" (value text) ]
        | BloggerDeltaPart.ImageOmitted mediaType
        | BloggerDeltaPart.MediaOmitted mediaType ->
            let mediaValue = mediaType |> Option.defaultValue "untyped"
            entry [ field "media_omitted" (value mediaValue) ]

    /// Message-layer historic frame: low-trust prior work-log, not an instruction.
    ///
    ///   [[do_not_exec]]
    ///   historic_frame = '''…'''
    ///
    /// Frame blob text stays pure body; this wrapper is projection-only (COMPANION-005).
    let renderHistoricFrame (frameBody: string) : string =
        SyntheticToml.document
            []
            [ SyntheticToml.tableArrayEntry
                  DoNotExecTable
                  [ SyntheticToml.field "historic_frame" (SyntheticToml.renderString frameBody) ] ]

    /// ENFORCER-071: one low-trust prior tip block (not a parent instruction).
    ///
    ///   [[do_not_exec]]
    ///   kind = "previous_enforcer_tip"
    ///   tip = "primitive-obsession"
    ///   cycle = "…"
    let renderPreviousEnforcerTip (tipField: string) (cycleId: string) : string =
        SyntheticToml.document
            []
            [ SyntheticToml.tableArrayEntry
                  DoNotExecTable
                  [ SyntheticToml.field "kind" (SyntheticToml.renderString "previous_enforcer_tip")
                    SyntheticToml.field "tip" (SyntheticToml.renderString tipField)
                    SyntheticToml.field "cycle" (SyntheticToml.renderString cycleId) ] ]

    /// CTX-013: the whole document, optionally carrying an instruction header.
    let renderWith (instructions: string list) (items: BloggerDeltaItem list) : string =
        SyntheticToml.document instructions (items |> List.map renderItem)

    /// The data-only document for callers whose instruction is carried separately.
    let render (items: BloggerDeltaItem list) : string = renderWith [] items
