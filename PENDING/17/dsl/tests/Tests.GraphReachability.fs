// 有限可判定领域 E2E：有向图可达性（140 版评审第二阶段）。
// 目标：验证 SupportedOnly↔真、RefutedOnly↔假、grade 随证据质量变化、反驳可靠性
// （不可达判定必须穷举证明——浅观察失败是证据不足，不是反驳）。
// 关键：证据由**真实观察协议执行**产生（BFS 遍历 + 结果 hash），非手工注入。
module Meditator.Tests.GraphReachability

open System
open System.Collections.Generic
open Meditator.Ledger
open Meditator.Boundary
open Meditator.Stop
open Meditator.Tests.Scenario
open Meditator.Tests.TestUtil

/// 有向图（邻接表）。
type Graph = int list array

/// 样例图：0→{1,2}、1→3、2→3、3→∅、4→5、5→∅。
/// 真命题：0→3 可达（路径 0-1-3 / 0-2-3）；假命题：0→4 不可达（0 的可达闭包 = {0,1,2,3}）。
let sampleGraph: Graph = [| [ 1; 2 ]; [ 3 ]; [ 3 ]; []; [ 5 ]; [] |]

type ObservationMode =
    | FullBFS // 穷举整个可达闭包（closed-world 可判定）
    | Shallow // 仅检查 from 的直接出边（不完整）

type Observation =
    { Found: bool
      PathLength: int option
      VisitedCount: int
      Complete: bool // 访问集 = 可达闭包（穷举完成）
      Digest: string }

/// 观察协议：真实 BFS 遍历（FullBFS 穷举整个闭包；Shallow 只展开一层）。
/// 观察回执 = 遍历结果的结构化摘要 + digest（真实执行产物）。
let runObservation (g: Graph) (fromN: int) (toN: int) (mode: ObservationMode) : Observation =
    let queue = Queue<int>()
    let visited = HashSet<int>()
    queue.Enqueue fromN
    visited.Add fromN |> ignore

    let mutable found = fromN = toN
    let mutable pathLength = if fromN = toN then Some 0 else None
    let mutable depth = 0

    while queue.Count > 0 && not found do
        let level = queue.Count
        depth <- depth + 1

        for _ in 1..level do
            let cur = queue.Dequeue()

            for next in g.[cur] do
                if next = toN then
                    found <- true
                    pathLength <- Some depth
                elif visited.Add next then
                    queue.Enqueue next

        if mode = Shallow then
            queue.Clear() // 只展开一层

    // Complete：BFS 自然穷举（FullBFS 下 visited = 闭包；Shallow 下必然不完整）。
    let complete = mode = FullBFS

    let digest =
        EventCodec.sha256Hex (
            EventCodec.field "f" (string found)
            + EventCodec.field "v" (string visited.Count)
            + EventCodec.field "p" (string pathLength)
            + EventCodec.field
                "m"
                (match mode with
                 | FullBFS -> "F"
                 | Shallow -> "S")
        )

    { Found = found
      PathLength = pathLength
      VisitedCount = visited.Count
      Complete = complete
      Digest = digest }

/// 从真实观察构造 warrant：支持（找到路径）或反对（穷举证明不可达）。
/// 纪律（认识论）：反对 warrant **只允许 Complete=true**（不可达是穷举证明）；
/// 不完整观察失败 = 证据不足（Unknown），不是反驳。
let warrantFromObservation (obs: Observation) (claim: Claim) (polarity: Polarity) : Result<Warrant, string> =
    if polarity = Opposes && not obs.Complete then
        Error "incomplete observation cannot refute (evidence insufficient, not refuted)"
    else
        let strength = if obs.Complete then Strong else Weak

        let witness =
            VerifierWitness.issue Verifiers.observation VerifierKind.Observation obs.Digest

        let body =
            { Id = WarrantId ""
              ClaimId = claim.Id
              Polarity = polarity
              Kind = WarrantKind.Observation
              Rule = "graph-reachability/v1"
              Strength = strength
              Scope = claim.Scope
              Origin = Provenance.create fixedClock "graph-bfs/v1" "observation/v1"
              VerifierWitnesses = [ witness ]
              DependencyWarrantIds = []
              UltimateSourceIds = [ SourceId("graph:" + claim.Scope.Content.Value) ]
              IntroducedBy = "" }

        { body with
            Id = EventCodec.warrantIdOfData body }
        |> Warrant.create

let private mkReachClaim (statement: string) (graphId: string) : Claim =
    let scope =
        { Content = Some graphId
          Time = None
          Modality = None
          Population = None }

    { Id = ClaimId.ofProposition statement scope
      Statement = statement
      Role = Assertion
      Source = ByObservation
      Scope = scope
      IntroducedBy = "" }

let run () =
    printfn "== 图可达性 E2E（140 版） =="

    // ── 任务 A（真命题）：0 → 3 可达——完整 BFS 找到路径 → 支持 + Confirmed。
    let claimA = mkReachClaim "node 0 reaches node 3" "graph-sample"
    let obsA = runObservation sampleGraph 0 3 FullBFS
    check "A: full BFS finds the path (ground truth = reachable)" (obsA.Found && obsA.Complete)

    let wA =
        warrantFromObservation obsA claimA Supports
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let lA =
        foldAll MeditationLedger.Empty [ ClaimFramed claimA; ContributionAccepted wA ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "A: SupportedOnly corresponds to true" (polarityOf lA claimA.Id claimA.Scope = SupportedOnly)
    check "A: complete observation yields Confirmed" ((gradeOfWarrant wA).Reliability = Confirmed)

    check
        "A: observation digest is execution-derived (not hand-injected)"
        (wA
         |> Warrant.data
         |> fun d ->
             d.VerifierWitnesses
             |> List.exists (fun v -> VerifierWitness.digest v = obsA.Digest))

    // ── 任务 B（假命题）：0 → 4 不可达——穷举证明（Complete）→ 反对 + RefutedOnly。
    let claimB = mkReachClaim "node 0 reaches node 4" "graph-sample"
    let obsB = runObservation sampleGraph 0 4 FullBFS

    check
        "B: full BFS exhausts the closure without finding 4 (ground truth = unreachable)"
        (not obsB.Found && obsB.Complete)

    let wB =
        warrantFromObservation obsB claimB Opposes
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let lB =
        foldAll MeditationLedger.Empty [ ClaimFramed claimB; ContributionAccepted wB ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "B: RefutedOnly corresponds to false" (polarityOf lB claimB.Id claimB.Scope = RefutedOnly)

    // ── 任务 C（浅观察漏判）：0 → 3 用 Shallow（0 的出边 {1,2} 不含 3）→ 未找到且不完整。
    let obsC = runObservation sampleGraph 0 3 Shallow
    check "C: shallow observation misses the path (incomplete)" (not obsC.Found && not obsC.Complete)

    // 纪律：不完整观察失败 ≠ 反驳——反对 warrant 被拒，极性保持 Unknown（证据不足）。
    match warrantFromObservation obsC claimA Opposes with
    | Error _ -> check "C: incomplete observation cannot refute (evidence insufficient, not refuted)" true
    | Ok _ -> check "C: incomplete observation cannot refute (evidence insufficient, not refuted)" false

    // ── 任务 D（弱协议但成功）：0 → 1 直接可达——Shallow 找到 → Tentative（协议质量反映在 grade）。
    let claimD = mkReachClaim "node 0 reaches node 1" "graph-sample"
    let obsD = runObservation sampleGraph 0 1 Shallow
    check "D: shallow observation finds the direct edge" (obsD.Found && not obsD.Complete)

    let wD =
        warrantFromObservation obsD claimD Supports
        |> function
            | Ok w -> w
            | Error e -> failwith e

    let lD =
        foldAll MeditationLedger.Empty [ ClaimFramed claimD; ContributionAccepted wD ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "D: SupportedOnly (weak protocol)" (polarityOf lD claimD.Id claimD.Scope = SupportedOnly)

    check
        "D: weak protocol yields Tentative (grade tracks observation quality)"
        ((gradeOfWarrant wD).Reliability = Tentative)

    // ── 基准统计：真/假对照（确定性领域）——SupportedOnly↔真、RefutedOnly↔假 100%。
    let tasks =
        [ (0, 1, true)
          (0, 2, true)
          (0, 3, true)
          (1, 3, true)
          (2, 3, true)
          (4, 5, true)
          (0, 4, false)
          (0, 5, false)
          (1, 4, false)
          (2, 4, false)
          (3, 4, false)
          (4, 0, false) ]

    let mutable correct = 0
    let mutable refutedTrue = 0 // 真命题被误判为反驳（假阴性）
    let mutable supportedFalse = 0 // 假命题被误判为支持（假阳性）

    for (f, t, truth) in tasks do
        let claim = mkReachClaim $"node {f} reaches node {t}" "graph-sample"
        let obs = runObservation sampleGraph f t FullBFS

        let warrantResult =
            if obs.Found then
                warrantFromObservation obs claim Supports
            else
                warrantFromObservation obs claim Opposes

        match warrantResult with
        | Error _ -> ()
        | Ok w ->
            let l =
                foldAll MeditationLedger.Empty [ ClaimFramed claim; ContributionAccepted w ]
                |> function
                    | Ok l -> l
                    | Error e -> failwith $"%A{e}"

            let pol = polarityOf l claim.Id claim.Scope

            if truth && pol = SupportedOnly then
                correct <- correct + 1
            elif truth && pol = RefutedOnly then
                refutedTrue <- refutedTrue + 1
            elif not truth && pol = RefutedOnly then
                correct <- correct + 1
            elif not truth && pol = SupportedOnly then
                supportedFalse <- supportedFalse + 1

    check
        "benchmark: polarity matches ground truth (12/12 deterministic)"
        (correct = 12 && refutedTrue = 0 && supportedFalse = 0)
