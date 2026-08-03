namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.EnforcerCodec

/// spec/15 ENFORCER-041/042/043：多调用 Cycle 合并。
///
/// 同一 ProviderRunIdentity 的所有成功 blog 调用组成一个 Blogger Cycle。
/// 排序键 = assistant message 中 tool-call part 的 provider-visible ordinal
/// （ENFORCER-042）——禁止 execute 顺序/完成顺序/Promise resolution 顺序/
/// Journal append 顺序/callID 字典序。
///
/// ToolCallId 唯一性与 ProviderRunIdentity 绑定属于 Host 接线层
/// （ENFORCER-041：身份只来自 ToolContext），本模块只做内容合并。
module EnforcerCycle =

    /// ENFORCER-042：一次 Blogger Cycle 合并后的结果。
    type MergedCycle =
        { MergedText: string
          MergedScores: Map<string, byte>
          MergedEvidence: string }

    /// ENFORCER-042：合并规则。
    ///
    /// MergedText = 所有非空 text 按 PartOrdinal 排序，"\n\n" 拼接。
    /// MergedScore[rule] = max(所有调用中该 rule 的有效分值)。
    /// MergedEvidence = 非空 evidence 按 PartOrdinal 排序，完全相同去重，"; " 拼接。
    let mergeCalls (calls: (int * CanonicalBlogCall) list) : MergedCycle =
        let sorted = calls |> List.sortBy fst

        let mergedText =
            sorted |> List.choose (fun (_, call) -> call.Text) |> String.concat "\n\n"

        let mergedScores =
            sorted
            |> List.collect (fun (_, call) -> call.Scores |> Map.toList)
            |> List.groupBy fst
            |> List.map (fun (ruleId, pairs) -> ruleId, pairs |> List.map snd |> List.max)
            |> Map.ofList

        let mergedEvidence =
            sorted
            |> List.choose (fun (_, call) -> call.Evidence)
            |> List.fold
                (fun (seen, acc) item ->
                    if Set.contains item seen then
                        (seen, acc)
                    else
                        (Set.add item seen, acc @ [ item ]))
                (Set.empty, [])
            |> snd
            |> String.concat "; "

        { MergedText = mergedText
          MergedScores = mergedScores
          MergedEvidence = mergedEvidence }

    /// ENFORCER-043：Cycle 有效当且仅当：
    /// - 至少一个成功执行的 blog call（调用方保证，列表非空）
    /// - 合并后的 text 规范化后非空
    let isValidCycle (merged: MergedCycle) : bool = merged.MergedText.Trim().Length > 0
