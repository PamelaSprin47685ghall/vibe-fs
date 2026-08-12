namespace Wanxiangshu.Domain

/// COMPANION-004/005 / ENFORCER-030: Companion projection assemblers (PROMPT-019).
///
/// System lives only in ProviderResources Role Law for Blogger (`role/blogger`),
/// composed by PromptResources and bound at managed-agent config. This module owns
/// semantic paths and pure message-layer wrappers — no Class A literals, no System,
/// no interpolation holes (CTX-001). Call sites load prose via ProviderProse.
[<RequireQualifiedAccess>]
module CompanionPrompt =

    let Normal = "lifecycle/companion/normal"
    let Squash = "lifecycle/companion/squash"
    let MemoryPreamble = "lifecycle/companion/memory-preamble"

    /// Plain lines → ARCH-010 comment-style instruction claim body.
    let asCommentedInstruction (lines: string list) =
        lines
        |> List.map (fun line -> "# " + line)
        |> String.concat "\n"

    /// COMPANION-005: durable frame body as assistant message text.
    /// Still wrapped as `[[do_not_exec]] historic_frame`; only the message role is assistant.
    let workingRecordMessage (frameBody: string) =
        BloggerToml.renderHistoricFrame frameBody

    /// ENFORCER-071: one previous tip as low-trust assistant body (not an instruction).
    let previousTipMessage (tipField: string) (cycleId: string) =
        BloggerToml.renderPreviousEnforcerTip tipField cycleId

    /// COMPANION-005: normal delta user message = instruction header first, then data body.
    ///
    /// ARCH-010: comment header + one blank line + `[[new_work_to_record]]` tables.
    /// `toml` is data-only (CTX-013); header is projection-only and not part of the
    /// 200 KiB chunk meter (metered at nextChunk before wrap).
    let newWorkMessage (instructionLines: string list) (toml: string) =
        let body = if isNull toml then "" else toml.TrimEnd('\n', '\r')

        match body with
        | "" -> SyntheticToml.document instructionLines []
        | data -> SyntheticToml.document instructionLines [ data ]

    /// COMPANION-010: wrap a frozen record prefix body for injection into X.
    let companionMemoryBlock (preamble: string) (frozenRecordPrefix: string) =
        preamble + "\n\n<work-log>\n" + frozenRecordPrefix + "\n</work-log>"
