namespace Wanxiangshu.Next.Domain

open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Domain.StrengthTypes

/// spec/14 STRENGTH-022：插值 Kneser-Ney 请求级 n-gram 预测器。
///
/// 训练状态只按 X 的 CanonicalRole 分桶（STRENGTH-022），禁止按模型组合/版本/
/// Session/仓库细分。模型切换的非平稳性只通过统一的计数衰减处理。
///
/// 本模块是纯函数：状态显式传入传出，不读时钟、不掷骰子、不碰外部。
module StrengthPredictor =

    /// 三元组计数。键是符号序列（最大 NGramMaximumOrder）。
    type NGramCounts = Map<string list, int64>

    /// 续接计数：一个前缀后出现的不同后继数。
    type ContinuationCounts = Map<string list, int64>

    /// STRENGTH-022：角色桶的共享状态。
    type StrengthRoleState =
        {
            /// 该桶内全部符号计数（含衰减）。
            Counts: NGramCounts
            /// 续接计数。
            Continuations: ContinuationCounts
            /// 已纳入 EffectiveTrainingSequence 的符号累计计数（衰减触发量）。
            EffectiveSymbolCount: int64
            /// 已观测的请求数（冷启动门槛）。
            ObservedRequests: int64
        }

    let emptyRoleState: StrengthRoleState =
        { Counts = Map.empty
          Continuations = Map.empty
          EffectiveSymbolCount = 0L
          ObservedRequests = 0L }

    /// 符号的字符串键（用于 n-gram 键）。
    let symbolKey (symbol: RequestSymbol) : string =
        match symbol with
        | RequestSymbol.Eot -> "eot"
        | RequestSymbol.ReadBatch b -> sprintf "read:%d:%d" b.Parallelism b.ResultBytes
        | RequestSymbol.WriteBatch -> "write"
        | RequestSymbol.ExecuteBatch -> "execute"
        | RequestSymbol.ControlBatch -> "control"
        | RequestSymbol.VerdictBatch -> "verdict"
        | RequestSymbol.OtherBatch -> "other"

    /// 把一次请求产生的符号序列追加到状态（STRENGTH-022：EffectiveTrainingSequence）。
    ///
    /// 每个符号用其前缀（1..order 阶）更新计数；续接计数记录每个前缀后出现的后继。
    let private updateCounts (state: StrengthRoleState) (sequence: string list) : StrengthRoleState =
        let rec loop counts continuations remaining =
            match remaining with
            | [] -> counts, continuations
            | symbol :: rest ->
                // 以该符号为后继的所有阶数前缀
                let prefixLengths = [ 1 .. StrengthPolicy.Strength.NGramMaximumOrder ]

                let counts' =
                    List.fold
                        (fun acc order ->
                            if order <= List.length (symbol :: rest) then
                                let prefix = (symbol :: rest) |> List.take order

                                match Map.tryFind prefix acc with
                                | Some n -> Map.add prefix (n + 1L) acc
                                | None -> Map.add prefix 1L acc
                            else
                                acc)
                        counts
                        prefixLengths

                // 续接计数：每个较短前缀的 distinct 后继
                let continuations' =
                    List.fold
                        (fun acc order ->
                            if order <= List.length remaining then
                                let prefix = remaining |> List.take order
                                let successor = symbol

                                // 只在第一次看到该后继时 +1（用 count=1 检测）
                                let key = prefix @ [ successor ]

                                match Map.tryFind key acc with
                                | Some _ -> acc
                                | None -> Map.add key 1L acc
                            else
                                acc)
                        continuations
                        prefixLengths

                loop counts' continuations' rest

        let counts, continuations = loop state.Counts state.Continuations sequence

        { state with
            Counts = counts
            Continuations = continuations
            EffectiveSymbolCount = state.EffectiveSymbolCount + int64 (List.length sequence)
            ObservedRequests = state.ObservedRequests + 1L }

    /// STRENGTH-022：计数衰减。触发源只能是 EffectiveSymbolCount 跨过
    /// CountDecayInterval 的整数倍——禁止 wall-clock / 进程启动 / 快照时间。
    let maybeDecay (state: StrengthRoleState) : StrengthRoleState =
        let interval = StrengthPolicy.Strength.CountDecayInterval
        let crossings = state.EffectiveSymbolCount / interval

        if crossings <= 0L then
            state
        else
            // 每个已跨过的整数倍应用一次衰减。用对数避免大计数重复乘法：
            // factor^(crossings) 精确到浮点即可（计数是近似统计）。
            let factor = StrengthPolicy.Strength.CountDecayFactor

            let scale = factor ** (float crossings)

            let decayMap (map: NGramCounts) =
                map |> Map.map (fun _ count -> max 1L (int64 (float count * scale)))

            // 衰减后重置计数基准：已应用的 crossings 不再重复应用。
            { state with
                Counts = decayMap state.Counts
                Continuations = decayMap state.Continuations
                EffectiveSymbolCount = state.EffectiveSymbolCount % interval }

    /// 观测一个完整请求（含 Eot 终结符），返回更新后的状态。
    let observeRequest (state: StrengthRoleState) (request: RequestSymbol list) : StrengthRoleState =
        let withEot = request @ [ RequestSymbol.Eot ]

        let updated = updateCounts state (List.map symbolKey withEot)

        maybeDecay updated

    /// STRENGTH-022：插值 Kneser-Ney 概率估计。
    ///
    /// P(w | h) = λ(h) * P_disc(w | h) + (1 - λ(h)) * P_cont(w)
    /// P_disc 用绝对折扣；P_cont 用续接计数归一化；λ 是折扣质量比例。
    let interpolatedProbability (state: StrengthRoleState) (history: string list) (next: string) : float =
        let discount = StrengthPolicy.Strength.KneserNeyAbsoluteDiscount
        let maxOrder = StrengthPolicy.Strength.NGramMaximumOrder
        let trimmed = history |> List.truncate maxOrder

        // P_cont(next) = continuationCount(next) / continuationTotal
        //
        // `updateCounts` builds continuation keys as `prefix @ [successor]` with
        // `prefix = remaining |> take order`, so at order 1 the key is `[x; x]`
        // (the successor doubled, not a distinct context). Continuation keys are
        // only written the first time a successor is seen, so each length-2 key
        // has value 1; filtering by LAST element = `next` therefore yields an
        // indicator of whether `next` ever appeared as its own successor — not a
        // frequency and not a strict KN continuation count (that would need a
        // distinct context dimension). This is a pre-existing structural
        // simplification; what this fix restores is that the backoff is actually
        // alive: the old code queried `Map.tryFind [next]` (length 1, never
        // present), which zeroed pCont unconditionally.
        let totalKeys =
            state.Continuations
            |> Map.toSeq
            |> Seq.filter (fun (key, _) -> List.length key = 2)
            |> Seq.length

        let nextKeys =
            state.Continuations
            |> Map.toSeq
            |> Seq.filter (fun (key, _) -> List.length key = 2 && List.last key = next)
            |> Seq.length

        let continuationTotal = int64 totalKeys
        let continuationCount = int64 nextKeys

        let pCont =
            if continuationTotal > 0L then
                float continuationCount / float continuationTotal
            else
                0.0

        // 前缀 p 的 distinct 后继数（续接计数键 = p @ [successor]）
        let distinctSuccessors (p: string list) =
            state.Continuations
            |> Map.toSeq
            |> Seq.filter (fun (key, _) -> List.length key = List.length p + 1 && List.take (List.length p) key = p)
            |> Seq.length

        // 递归回退：从最高阶逐级降到 P_cont
        let rec backoff (prefix: string list) : float =
            match prefix with
            | [] -> pCont
            | p ->
                let full = p @ [ next ]

                let count =
                    match Map.tryFind full state.Counts with
                    | Some n -> n
                    | None -> 0L

                let total =
                    match Map.tryFind p state.Counts with
                    | Some n -> n
                    | None -> 0L

                if total > 0L then
                    // λ = 折扣质量 / 总计数（discount × distinct successors）
                    let lambda = (float (discount * float (distinctSuccessors p))) / float total

                    let discounted = max 0.0 (float count - discount)
                    let ml = discounted / float total

                    // 插值：λ 部分给折扣 ML，剩余质量给低阶回退
                    if lambda >= 1.0 then
                        ml
                    else
                        (1.0 - lambda) * ml + lambda * backoff (List.tail p)
                else
                    backoff (List.tail p)

        backoff trimmed

    /// STRENGTH-021：给定历史，输出 K1/K2 概率。
    ///
    /// ProbabilityRead1 = 下一次请求为纯只读批次的概率
    /// ProbabilityRead2 = 接下来两个请求均为纯只读批次的概率
    let predictRead
        (state: StrengthRoleState)
        (history: RequestSymbol list)
        (features: StrengthFeatures)
        : float * float =
        let historyKeys = List.map symbolKey history

        let p1 =
            interpolatedProbability
                state
                historyKeys
                (symbolKey (
                    RequestSymbol.ReadBatch
                        { Tools = set [ "read" ]
                          Parallelism = 1
                          ResultBytes = 0L }
                ))

        // 简化：第二个 read 的概率用「read 后 read」的 n-gram
        let p2 =
            let readHistory =
                historyKeys
                @ [ symbolKey (
                        RequestSymbol.ReadBatch
                            { Tools = set [ "read" ]
                              Parallelism = 1
                              ResultBytes = 0L }
                    ) ]

            interpolatedProbability
                state
                readHistory
                (symbolKey (
                    RequestSymbol.ReadBatch
                        { Tools = set [ "read" ]
                          Parallelism = 1
                          ResultBytes = 0L }
                ))

        // 结构特征调制（STRENGTH-023）：grep/glob 命中文件多、路径集中 → 更可能 read
        let structureBoost =
            if features.RecentHitFileCount > 0 then
                min 1.0 (0.3 + 0.1 * float features.RecentHitFileCount)
            else
                0.2

        let p1' = min 0.99 (p1 * 0.5 + structureBoost * 0.5)
        let p2' = min 0.99 (p1' * p2)

        p1', p2'
