namespace Wanxiangshu.Context.Companion.Blogger

open Wanxiangshu.Foundation

/// CTX-013: what a Blogger delta part is, and how it renders as TOML.
///
/// Shared syntax lives in `LlmFacing` (ARCH-010). This module owns the
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
/// DSL-state-combination: domain — role, rendered part and truncation marker
/// are one immutable transcript item; truncation records an observed byte-bound
/// fact, not a workflow stage.
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
    let dataBlock (item: BloggerDeltaItem) : LlmFacing.DataBlock =
        let truncated =
            if item.Truncated then
                [ LlmFacing.Data.boolMember "truncated" true ]
            else
                []

        let entry fields =
            LlmFacing.Data.tableArray NewWorkTable (fields @ truncated)

        match item.Part with
        | BloggerDeltaPart.TextPart text ->
            // Role is the field name: user / assistant / tool / …
            entry [ LlmFacing.Data.stringMember item.Role text ]
        | BloggerDeltaPart.ReasoningPart text -> entry [ LlmFacing.Data.stringMember "reasoning" text ]
        | BloggerDeltaPart.ToolCallPart(tool, args) ->
            // `args` is already canonical from the Host codec; do not re-sort.
            entry
                [ LlmFacing.Data.stringMember "tool_call" tool
                  LlmFacing.Data.stringMember "arguments" args ]
        | BloggerDeltaPart.ToolResultPart text -> entry [ LlmFacing.Data.stringMember "tool_result" text ]
        | BloggerDeltaPart.ImageOmitted mediaType
        | BloggerDeltaPart.MediaOmitted mediaType ->
            let mediaValue = mediaType |> Option.defaultValue "untyped"
            entry [ LlmFacing.Data.stringMember "media_omitted" mediaValue ]

    let renderItem (item: BloggerDeltaItem) : string =
        LlmFacing.empty |> LlmFacing.withData [ dataBlock item ] |> LlmFacing.render

    /// Message-layer historic frame: low-trust prior work-log, not an instruction.
    ///
    ///   [[do_not_exec]]
    ///   historic_frame = '''…'''
    ///
    /// Frame blob text stays pure body; this wrapper is projection-only (COMPANION-005).
    let renderHistoricFrame (frameBody: string) : string =
        LlmFacing.empty
        |> LlmFacing.withData
            [ LlmFacing.Data.tableArray DoNotExecTable [ LlmFacing.Data.stringMember "historic_frame" frameBody ] ]
        |> LlmFacing.render

    /// ENFORCER-071: one low-trust prior tip block (not a parent instruction).
    ///
    ///   [[do_not_exec]]
    ///   kind = "previous_enforcer_tip"
    ///   tip = "primitive-obsession"
    ///   cycle = "…"
    let renderPreviousEnforcerTip (tipField: string) (cycleId: string) : string =
        LlmFacing.empty
        |> LlmFacing.withData
            [ LlmFacing.Data.tableArray
                  DoNotExecTable
                  [ LlmFacing.Data.stringMember "kind" "previous_enforcer_tip"
                    LlmFacing.Data.stringMember "tip" tipField
                    LlmFacing.Data.stringMember "cycle" cycleId ] ]
        |> LlmFacing.render

    let documentWith (instructions: string list) (items: BloggerDeltaItem list) : LlmFacing.Document =
        LlmFacing.instructions instructions
        |> LlmFacing.withData (items |> List.map dataBlock)

    /// CTX-013: the whole document, optionally carrying an instruction header.
    let renderWith (instructions: string list) (items: BloggerDeltaItem list) : string =
        documentWith instructions items |> LlmFacing.render

    /// The data-only document for callers whose instruction is carried separately.
    let render (items: BloggerDeltaItem list) : string = renderWith [] items
