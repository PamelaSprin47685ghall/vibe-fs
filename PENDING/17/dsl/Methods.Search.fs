// ⑥ 搜索控制（设计目录）：策略与有界搜索组合子。不产内容——frontier/剪枝/遍历顺序
// 是控制层的参数；不能改善 grade、不能关闭 unknown（§14.1）。
// 编译顺序：15（依赖 Boundary、Meditation）。
module Meditator.Methods.Search

open Meditator.Boundary
open Meditator.Meditation

/// search_space_exploration：空间是纯值声明，遍历策略由控制层选择。
type SearchSpace<'state, 'move> =
    { Nodes: 'state list
      Moves: 'move list }

let declareSpace (nodes: 'state list) (moves: 'move list) : SearchSpace<'state, 'move> =
    { Nodes = nodes; Moves = moves }

/// H-prune：branch_and_bound。无 bound witness 不剪（§16.6.2）——
/// witness 缺失的分支保留，不是被剪。
let pruneWith (bound: 'branch -> 'witness option) (branches: 'branch list) : 'branch list =
    branches |> List.filter (fun b -> (bound b).IsNone)

/// H-memo：dynamic_programming。规范化相同的子问题共享结果。
/// 并发安全：ConcurrentDictionary 防并发写入破坏容器；同 key 并发重复求解
/// 是允许的（T5 保证同 key 同结果），GetOrAdd 只写入首个结果（评审：共享可变 Dictionary 的问题）。
let memoize (normalize: 'p -> 'k) (solve: 'p -> Meditation<'r>) : 'p -> Meditation<'r> =
    let cache = System.Collections.Concurrent.ConcurrentDictionary<'k, 'r>()

    fun problem ->
        meditation {
            let key = normalize problem

            match cache.TryGetValue key with
            | true, cached -> return cached
            | false, _ ->
                let! result = solve problem
                return cache.GetOrAdd(key, (fun _ -> result))
        }

/// H-sample（§12.6）：monte_carlo_sampling。无确定性复核函数即规则不可用——
/// 采样只能寻找稳定模式，不能提高结论 grade（§13.2⑥）。复核 witness 由程序集内权柄签发（P0-1）。
let sampleThenVerify
    (seed: uint64)
    (count: int)
    (sample: uint64 -> int -> Meditation<'s>)
    (summarize: 's list -> Meditation<'r>)
    (verifyDeterministically: 'r -> Meditation<VerifierWitness list>)
    : Meditation<Result<Validated<'r>, 'r>> =
    meditation {
        let! samples = mapBounded 4 [ 1..count ] (fun i -> sample seed i)
        let! summary = summarize samples

        match! verifyDeterministically summary with
        | [] -> return Error summary
        | witnesses ->
            match Validated.create Verifiers.deterministicCheck witnesses summary with
            | Ok validated -> return Ok validated
            | Error _ -> return Error summary
    }

/// H-anneal：simulated_annealing。bestCandidateVerification 词法化（§14.2）。
// val anneal :
//     objective:('s -> float) -> neighbors:('s -> 's list) -> schedule:CoolingSchedule
//     -> verifyBest:('s -> Meditation<VerifierWitness list>) -> initial:'s
//     -> Meditation<Result<Validated<'s>, 's>>

/// H-swarm：swarm_optimization。canonicalDedup + independentEvidenceCheck 词法化（§14.2）。
// val swarm :
//     candidates:'c list -> explore:('c -> Meditation<'c>) -> dedupeCanonically:('c list -> 'c list)
//     -> independentCheck:('c -> Meditation<VerifierWitness list>)
//     -> Meditation<Result<Validated<'c>, 'c list>>
