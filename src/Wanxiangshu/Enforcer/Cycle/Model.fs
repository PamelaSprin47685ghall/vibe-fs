namespace Wanxiangshu.Enforcer.Cycle

open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.EnforcerCodec
open Wanxiangshu.Participant.Provider

module EnforcerCycle =

    [<Literal>]
    let MaxBlogTextBytes = 512 * 1024

    [<Literal>]
    let MaxEvidenceBytes = 128 * 1024

    type ContentBounds =
        { TextBytes: int
          TipSectionsBytes: int
          EvidenceBytes: int }

    [<RequireQualifiedAccess>]
    type ContentBoundsRejection =
        | TextTooLarge
        | TipSectionsTooLarge
        | EvidenceTooLarge

    type CanonicalCycle =
        { MergedText: string
          TipName: string
          TipSectionsText: string
          MergedEvidence: string }

    let private oneLine (text: string) =
        text.Split([| ' '; '\t'; '\r'; '\n' |], System.StringSplitOptions.RemoveEmptyEntries)
        |> String.concat " "

    let private renderStructured (record: ChronicleRecord) =
        let prose =
            [ record.Charge; record.Occurrence; record.Settlement; record.Consequence ]
            |> List.map oneLine
            |> String.concat " "

        let inlineEvidence (evidence: string) =
            evidence
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\u0085", "\\u0085")
                .Replace("\u2028", "\\u2028")
                .Replace("\u2029", "\\u2029")

        match record.Evidence with
        | Some evidence -> prose + " [" + inlineEvidence evidence + "]"
        | None -> prose

    let private tipSectionTitles =
        function
        | ProviderLanguage.SimplifiedChinese -> [ "现在该做什么"; "为什么重要"; "修复策略"; "决策分支"; "常见假修复"; "验证"; "完成条件" ]
        | ProviderLanguage.English ->
            [ "What To Do Now"
              "Why This Matters"
              "Repair Strategy"
              "Decision Branches"
              "Common Wrong Fixes"
              "Verification"
              "Done When" ]

    /// C7.8 / D4：按撰写语言把七节拼装为「## <节名> + 空行 + 节文」段，
    /// 段间空行、单一文本、无尾随换行。纯函数（language 为参数）。
    let renderTipSections (language: ProviderLanguage) (sections: string list) : string =
        List.zip (tipSectionTitles language) sections
        |> List.map (fun (title, body) -> sprintf "## %s\n\n%s" title body)
        |> String.concat "\n\n"

    let ofCall (call: CanonicalBlogCall) : CanonicalCycle =
        let tipSectionsText =
            renderTipSections
                (GlobalProviderLanguage.current ())
                [ call.WhatToDoNow
                  call.WhyThisMatters
                  call.RepairStrategy
                  call.DecisionBranches
                  call.CommonWrongFixes
                  call.Verification
                  call.DoneWhen ]

        match call.Content with
        | ChronicleContent.Structured record ->
            { MergedText = renderStructured record
              TipName = call.TipName
              TipSectionsText = tipSectionsText
              MergedEvidence = "" }
        | ChronicleContent.Legacy(text, _) ->
            { MergedText = text
              TipName = call.TipName
              TipSectionsText = tipSectionsText
              MergedEvidence = "" }

    let isValidCycle (cycle: CanonicalCycle) : bool = cycle.MergedText.Trim().Length > 0

    let contentBoundsError =
        function
        | ContentBoundsRejection.TextTooLarge -> sprintf "blog cycle text exceeds MaxBlogTextBytes=%d" MaxBlogTextBytes
        | ContentBoundsRejection.TipSectionsTooLarge ->
            sprintf "blog cycle tip sections exceed MaxBlogTextBytes=%d" MaxBlogTextBytes
        | ContentBoundsRejection.EvidenceTooLarge ->
            sprintf "blog cycle evidence exceeds MaxEvidenceBytes=%d" MaxEvidenceBytes

    // semantic-decorator-owner: behavior-diagnosis
    // semantic-decorator-WHAT: behavior-diagnosis-011
    // semantic-decorator-trace-relation: count canonical text exactly once, then tip sections exactly once, then evidence exactly once, before applying the text-first rejection order
    // semantic-decorator-proof: requirements/behavior-diagnosis/tests/bounds.test.mjs::WHAT[behavior-diagnosis-011] ENFORCER_042_bound_constants_match_utf8_byte_thresholds
    // semantic-decorator-failure-policy: a synchronous count failure stops before the remaining counts; a tip sections count failure stops before the evidence count and either bounds decision
    // semantic-decorator-cancel-policy: pure synchronous byte counting introduces no cancellation boundary
    // semantic-decorator-deadline-policy: pure synchronous byte counting introduces no deadline
    // semantic-decorator-invocation-bound: 3
    let validateContentBounds
        (byteCount: string -> int)
        (text: string)
        (tipSections: string)
        (evidence: string)
        : Result<ContentBounds, ContentBoundsRejection> =
        let bounds =
            { TextBytes = byteCount text
              TipSectionsBytes = byteCount tipSections
              EvidenceBytes = byteCount evidence }

        if bounds.TextBytes > MaxBlogTextBytes then
            Error ContentBoundsRejection.TextTooLarge
        elif bounds.TipSectionsBytes > MaxBlogTextBytes then
            Error ContentBoundsRejection.TipSectionsTooLarge
        elif bounds.EvidenceBytes > MaxEvidenceBytes then
            Error ContentBoundsRejection.EvidenceTooLarge
        else
            Ok bounds
