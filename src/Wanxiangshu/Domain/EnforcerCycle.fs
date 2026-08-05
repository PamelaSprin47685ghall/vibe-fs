namespace Wanxiangshu.Domain

open Wanxiangshu.Domain.EnforcerCodec

/// spec/15 ENFORCER-041/042/043/025：多调用 Cycle 归并。
///
/// 同一 ProviderRunIdentity 的所有成功 blog 调用组成一个 Blogger Cycle。
/// 排序键 = assistant message 中 tool-call part 的 provider-visible ordinal
/// （ENFORCER-042）——禁止 execute 顺序/完成顺序/Promise resolution 顺序/
/// Journal append 顺序/callID 字典序。
///
/// tip：第一个按 PartOrdinal 的有效 tip（ENFORCER-025）；不合并、不按字典序/
/// 严重度/max-score 猜选。text/evidence 仍拼接。
module EnforcerCycle =

    /// ENFORCER-042：一次 Blogger Cycle 归并后的结果。
    type MergedCycle =
        {
            MergedText: string
            CanonicalTip: EnforcerTip
            MergedEvidence: string
            /// True when more than one call contributed (protocol violation).
            MultiCall: bool
        }

    /// ENFORCER-042 / 025：归并。
    ///
    /// calls 必须非空且每项已通过 tip 二次校验（decodeCall Ok）。
    /// MergedText = 非空 text 按 PartOrdinal，"\n\n" 拼接。
    /// CanonicalTip = 第一个按 PartOrdinal 的 tip。
    /// MergedEvidence = 非空 evidence 按 PartOrdinal，完全相同去重，"; " 拼接。
    let mergeCalls (calls: (int * CanonicalBlogCall) list) : MergedCycle =
        let sorted = calls |> List.sortBy fst

        let mergedText =
            sorted |> List.choose (fun (_, call) -> call.Text) |> String.concat "\n\n"

        let canonicalTip =
            match sorted with
            | (_, first) :: _ -> first.Tip
            | [] ->
                // validateCycle 保证非空；此分支不可达。
                { RuleId = ""
                  FieldName = ""
                  CatalogOrdinal = 0 }

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
          CanonicalTip = canonicalTip
          MergedEvidence = mergedEvidence
          MultiCall = List.length sorted > 1 }

    /// ENFORCER-043：Cycle 有效当且仅当合并后 text 规范化后非空。
    let isValidCycle (merged: MergedCycle) : bool = merged.MergedText.Trim().Length > 0
