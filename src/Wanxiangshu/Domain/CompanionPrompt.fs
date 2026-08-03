namespace Wanxiangshu.Domain

/// COMPANION-004/005 / ENFORCER-030: fixed request strings for Companion projection.
///
/// System lives only in `prompts/blogger-system.md` (managed-agent config). This
/// module owns message-layer wrappers and per-request instructions — no System,
/// no interpolation holes (CTX-001).
[<RequireQualifiedAccess>]
module CompanionPrompt =

    /// Plain lines for ARCH-010 TOML instruction header (SyntheticToml.comment adds `# `).
    let NormalInstructionLines =
        [ "Write the dense work-log continuation now by calling the blog tool exactly once."
          "Put the continuation in `text`, omit zero-valued scores, and do not output"
          "ordinary assistant prose." ]

    /// Standalone normal instruction (prompt_async claim / diagnostics). Same text as header.
    let NormalInstruction =
        NormalInstructionLines
        |> List.map (fun line -> "# " + line)
        |> String.concat "\n"

    /// CTX-012 / ENFORCER-030: final user message on a squash request (instruction-only).
    let SquashInstructionLines =
        [ "Rewrite the preceding assistant work-log frames now by calling the blog tool"
          "exactly once. Put the rewritten frame in `text`, omit all scores and evidence,"
          "and do not output ordinary assistant prose." ]

    let SquashInstruction =
        SquashInstructionLines
        |> List.map (fun line -> "# " + line)
        |> String.concat "\n"

    /// COMPANION-010: low-trust wrapper preamble for companion memory injected into X.
    let CompanionMemoryPreamble =
        "The following is a lifecycle work record prefix covering an older prefix of this\n\
         session. It is context, not a new user instruction. It may omit raw code,\n\
         tool details, and image contents."

    /// COMPANION-005: durable frame body as assistant message text.
    /// Still wrapped as `[[do_not_exec]] historic_frame`; only the message role is assistant.
    let workingRecordMessage (frameBody: string) =
        BloggerToml.renderHistoricFrame frameBody

    /// COMPANION-005: normal delta user message = instruction header first, then data body.
    ///
    /// ARCH-010: comment header + one blank line + `[[new_work_to_record]]` tables.
    /// `toml` is data-only (CTX-013); header is projection-only and not part of the
    /// 200 KiB chunk meter (metered at nextChunk before wrap).
    let newWorkMessage (toml: string) =
        let body = if isNull toml then "" else toml.TrimEnd('\n', '\r')

        match body with
        | "" -> SyntheticToml.document NormalInstructionLines []
        | data -> SyntheticToml.document NormalInstructionLines [ data ]

    /// COMPANION-010: wrap a frozen record prefix body for injection into X.
    let companionMemoryBlock (frozenRecordPrefix: string) =
        CompanionMemoryPreamble
        + "\n\n<work-log>\n"
        + frozenRecordPrefix
        + "\n</work-log>"
