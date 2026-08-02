namespace Wanxiangshu.Next.Domain

/// SSOT/15 ENFORCER-100/101/102：Enforcement Nudge 渲染。
///
/// 一个触发批次产生一个 fake user message。每条规则按 NudgeKey 去重后渲染
/// 一行 `# [<NudgeKey>] <CanonicalNudgeText>`；有 evidence 时追加最后一行
/// `# Evidence: <merged>`。`#` 是 TOML-compatible comment，也是模型应认真
/// 对待的 user 内容（ENFORCER-100）。
module EnforcerNudge =

    /// ENFORCER-100：单行渲染。
    let renderLine (nudgeKey: string) (canonicalNudgeText: string) : string =
        sprintf "# [%s] %s" nudgeKey canonicalNudgeText

    /// ENFORCER-102：evidence 行。
    let renderEvidence (evidence: string) : string = sprintf "# Evidence: %s" evidence

    /// ENFORCER-100/101：渲染一个触发批次。
    ///
    /// rules = (CatalogOrdinal, NudgeKey, CanonicalNudgeText) 列表，
    /// 已按 ENFORCER-101 排序（NudgeKey 首个 RuleId 的 CatalogOrdinal）。
    /// evidence 合并：按报告顺序完全去重后 "; " 拼接（ENFORCER-102）。
    let renderBatch (rules: (int * string * string) list) (evidence: string option) : string =
        let lines =
            rules
            |> List.sortBy (fun (ordinal, _, _) -> ordinal)
            |> List.map (fun (_, key, text) -> renderLine key text)

        let withEvidence =
            match evidence with
            | None -> lines
            | Some e when e.Trim().Length = 0 -> lines
            | Some e -> lines @ [ renderEvidence e ]

        String.concat "\n" withEvidence

    /// ENFORCER-102：evidence 合并（按出现顺序去重，"; " 拼接）。
    let mergeEvidence (evidenceItems: string list) : string =
        evidenceItems
        |> List.map (fun s -> s.Trim())
        |> List.filter (fun s -> s.Length > 0)
        |> List.fold
            (fun (seen, acc) item ->
                if Set.contains item seen then
                    (seen, acc)
                else
                    (Set.add item seen, acc @ [ item ]))
            (Set.empty, [])
        |> snd
        |> String.concat "; "
