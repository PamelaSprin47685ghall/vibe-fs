namespace Wanxiangshu.Next.Domain

open System

/// spec/15 ENFORCER-020…025：`blog` 工具参数的 canonical codec。
///
/// 纯函数：raw JSON object → CanonicalBlogCall。字段名拼写容错
/// （ENFORCER-024：NFKC → lowercase → 连字符 → 精确匹配 → DL 最近邻），
/// 值解析容错（ENFORCER-023），同 RuleId 取 max（ENFORCER-025）。
module EnforcerCodec =

    /// ENFORCER-026：领域闭合类型。
    type CanonicalBlogCall =
        {
            /// ProviderRunIdentity 由 ToolContext.messageID 提供（ENFORCER-041），
            /// codec 层不构造它——这里只承载 text/evidence/scores。
            Text: string option
            Evidence: string option
            /// RuleId → 0..9 分。缺失字段等价于 0（ENFORCER-022）。
            Scores: Map<string, byte>
        }

    /// ENFORCER-024：字段名规范化。
    ///
    /// 1. 保留 text 与 evidence 两个 reserved key
    /// 2. Unicode NFKC
    /// 3. 转 ASCII lowercase
    /// 4. 空格/下划线/句点/连续连字符 → 单个 "-"
    /// 5. 删除首尾 "-"
    let normalizeFieldName (raw: string) : string =
        let nfkc = raw.Normalize(Text.NormalizationForm.FormKC).ToLowerInvariant()

        let collapsed =
            nfkc
            |> Seq.map (fun ch ->
                match ch with
                | ' '
                | '_'
                | '.' -> '-'
                | c when c = '-' -> '-'
                | c -> c)
            |> System.String.Concat

        // 连续连字符合并
        let rec collapseDashes (s: string) =
            if s.Contains("--") then
                collapseDashes (s.Replace("--", "-"))
            else
                s

        collapseDashes collapsed |> fun s -> s.Trim('-')

    /// ENFORCER-024：统一前缀命名空间。codec 只对 `enf_` 开头的 unknown key
    /// 做最近邻映射；其他未知属性忽略。
    let hasEnfPrefix (normalized: string) : bool =
        normalized.StartsWith("enf-", StringComparison.Ordinal)

    /// ENFORCER-023：值容错解析。0..9 整数；越界/非数字/布尔 → None（归零）。
    ///
    /// 只用 float 分支匹配 number：Fable 把 F# 的 `:? int` 编译为 `value | 0`
    /// 截断，`NaN | 0 === 0` 会让 NaN 错误地解析为 0（实测）。统一走 float
    /// 分支并检查整数性（`f = floor f`）即可排除 NaN/Infinity/小数。
    let parseScore (value: obj) : byte option =
        match value with
        | null -> None
        | :? string as s ->
            let t = s.Trim()

            match Int32.TryParse t with
            | true, n when n >= 0 && n <= 9 -> Some(byte n)
            | _ -> None
        | :? float as f when
            not (Double.IsNaN f)
            && not (Double.IsInfinity f)
            && f >= 0.0
            && f <= 9.0
            && f = Math.Floor f
            ->
            Some(byte f)
        | _ -> None

    /// Damerau–Levenshtein 距离（ENFORCER-024 最近邻）。
    ///
    /// 用交错数组而非 Array2D：Array2D 是 .NET 特有 API，Fable 不支持。
    let damerauLevenshtein (a: string) (b: string) : int =
        let aLen = a.Length
        let bLen = b.Length

        // d[i][j] = 距离；交错数组每行 bLen+1 列。
        let d = Array.init (aLen + 1) (fun _ -> Array.zeroCreate (bLen + 1))

        for i in 0..aLen do
            d.[i].[0] <- i

        for j in 0..bLen do
            d.[0].[j] <- j

        for i in 1..aLen do
            for j in 1..bLen do
                let cost = if a.[i - 1] = b.[j - 1] then 0 else 1
                d.[i].[j] <- min (d.[i - 1].[j] + 1) (min (d.[i].[j - 1] + 1) (d.[i - 1].[j - 1] + cost))

                if i > 1 && j > 1 && a.[i - 1] = b.[j - 2] && a.[i - 2] = b.[j - 1] then
                    d.[i].[j] <- min d.[i].[j] (d.[i - 2].[j - 2] + 1)

        d.[aLen].[bLen]

    /// ENFORCER-024：最近邻映射的平局规则。
    /// 1. 公共前缀更长者 2. 公共 token 数更多者 3. Catalog 固定顺序更前者。
    let private tieBreak (candidates: (int * int * int * int * string) list) =
        candidates
        |> List.sortBy (fun (dist, negPrefix, negTokens, ordinal, _) -> dist, negPrefix, negTokens, ordinal)
        |> List.head

    /// ENFORCER-024/025：把 raw 字段名映射到 RuleId，返回该字段的解析值。
    ///
    /// catalog = (FieldName, RuleId, CatalogOrdinal) 列表。
    /// 返回 (RuleId, score option)。text/evidence 是 reserved，不参与映射。
    let resolveField
        (catalog: (string * string * int) list)
        (rawKey: string)
        (rawValue: obj)
        : (string * byte option) option =
        let normalized = normalizeFieldName rawKey

        if normalized = "text" || normalized = "evidence" then
            None
        else
            let score = parseScore rawValue

            // 精确匹配优先
            match List.tryFind (fun (field, _, _) -> field = normalized) catalog with
            | Some(_, ruleId, _) -> Some(ruleId, score)
            | None ->
                // 只对 enf_ 前缀做最近邻（ENFORCER-024）
                if not (hasEnfPrefix normalized) then
                    None
                else
                    // `enf-` 是命名空间标记（"这是评分字段"），不参与距离比较——
                    // 否则前缀本身贡献 4 的距离，任何拼写错误的字段都无法命中。
                    let stripped = normalized.Substring("enf-".Length)

                    let candidates =
                        catalog
                        |> List.map (fun (field, ruleId, ordinal) ->
                            let dist = damerauLevenshtein stripped field

                            // 公共前缀长度
                            let commonPrefix =
                                let rec loop i =
                                    if i < min stripped.Length field.Length && stripped.[i] = field.[i] then
                                        loop (i + 1)
                                    else
                                        i

                                loop 0

                            let tokens = field.Split('-') |> Set.ofArray
                            let normTokens = stripped.Split('-') |> Set.ofArray
                            let commonTokens = Set.intersect tokens normTokens |> Set.count
                            dist, -commonPrefix, -commonTokens, ordinal, ruleId)
                        |> List.filter (fun (d, _, _, _, _) -> d <= 3)

                    match candidates with
                    | [] -> None
                    | _ ->
                        let _, _, _, _, ruleId = tieBreak candidates
                        Some(ruleId, score)

    /// ENFORCER-020/025：解析一个 blog 调用。
    ///
    /// rawArgs 是 provider 传来的原始 JSON object（已跳过 Host 的 closed-schema
    /// 拒绝——canary C-03 证明拼写错误能到达 codec）。
    let decodeCall (catalog: (string * string * int) list) (rawArgs: Map<string, obj>) : CanonicalBlogCall =
        let text =
            rawArgs
            |> Map.tryFind "text"
            |> Option.bind (function
                | :? string as s -> Some s
                | _ -> None)
            |> Option.map (fun s -> s.Trim())
            |> Option.filter (fun s -> s.Length > 0)

        let evidence =
            rawArgs
            |> Map.tryFind "evidence"
            |> Option.bind (function
                | :? string as s -> Some s
                | _ -> None)
            |> Option.map (fun s -> s.Trim())
            |> Option.filter (fun s -> s.Length > 0)

        // ENFORCER-025：同一 RuleId 多个原始字段 → 取 max（只收有效值）。
        let scores =
            rawArgs
            |> Map.toList
            |> List.choose (fun (key, value) -> resolveField catalog key value)
            |> List.choose (fun (ruleId, score) -> score |> Option.map (fun s -> ruleId, s))
            |> List.groupBy fst
            |> List.map (fun (ruleId, pairs) -> ruleId, pairs |> List.map snd |> List.max)
            |> Map.ofList

        { Text = text
          Evidence = evidence
          Scores = scores }

    /// ENFORCER-022：text 必须存在且规范化后非空；缺失评分字段等价于 0。
    let hasValidText (call: CanonicalBlogCall) : bool = call.Text.IsSome
