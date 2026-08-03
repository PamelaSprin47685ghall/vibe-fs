// Meditator 认识论最小反例（140 版评审第三阶段）。
// 每个反例锚定一个认识论语义决策（EPISTEMICS.md）；不覆盖的决策会在此失败。
module Meditator.Tests.Counterexamples

open System
open Meditator.Ledger
open Meditator.Boundary
open Meditator.Stop
open Meditator.Obligation
open Meditator.Tests.Scenario
open Meditator.Tests.TestUtil

let private scopeEarth = Scenario.scopeEarth

let private mkClaimLocal (s: string) =
    { Id = ClaimId.ofProposition s scopeEarth
      Statement = s
      Role = Assertion
      Source = ByOracleProposal
      Scope = scopeEarth
      IntroducedBy = "" }

let private mkWitnessLocal (tag: string) =
    VerifierWitness.issue Verifiers.observation VerifierKind.Observation ("cx:" + tag)

let private mkObs
    (claim: Claim)
    (polarity: Polarity)
    (strength: SupportStrength)
    (witness: VerifierWitness)
    (sources: string list)
    : Warrant =
    let body =
        { Id = WarrantId ""
          ClaimId = claim.Id
          Polarity = polarity
          Kind = WarrantKind.Observation
          Rule = "observation/v1"
          Strength = strength
          Scope = scopeEarth
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ witness ]
          DependencyWarrantIds = []
          UltimateSourceIds = sources |> List.map SourceId
          IntroducedBy = "" }

    { body with
        Id = EventCodec.warrantIdOfData body }
    |> Warrant.create
    |> function
        | Ok w -> w
        | Error e -> failwith e

let private mkObsForScope
    (claim: Claim)
    (polarity: Polarity)
    (scope: Scope)
    (witness: VerifierWitness)
    (sources: string list)
    : Warrant =
    let body =
        { Id = WarrantId ""
          ClaimId = claim.Id
          Polarity = polarity
          Kind = WarrantKind.Observation
          Rule = "observation/v1"
          Strength = Strong
          Scope = scope
          Origin = Provenance.create fixedClock "obs" "observation/v1"
          VerifierWitnesses = [ witness ]
          DependencyWarrantIds = []
          UltimateSourceIds = sources |> List.map SourceId
          IntroducedBy = "" }

    { body with
        Id = EventCodec.warrantIdOfData body }
    |> Warrant.create
    |> function
        | Ok w -> w
        | Error e -> failwith e

let run () =
    printfn "== 认识论反例（140 版） =="

    // #1：一条强支持 + 一条弱反对 → Contested（双侧信息存在；强弱不合并，grade 分侧）。
    let c1 = mkClaimLocal "strength-mismatch"
    let w1s = mkObs c1 Supports Strong (mkWitnessLocal "1s") [ "src-1" ]
    let w1o = mkObs c1 Opposes Weak (mkWitnessLocal "1o") [ "src-2" ]

    let l1 =
        foldAll MeditationLedger.Empty [ ClaimFramed c1; ContributionAccepted w1s; ContributionAccepted w1o ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "#1 strong+weak → Contested (information label, not verdict)" (polarityOf l1 c1.Id scopeEarth = Contested)

    check
        "#1 grades stay per-side (no averaging)"
        ((gradeOfWarrant w1s).Reliability = Confirmed
         && (gradeOfWarrant w1o).Reliability = Tentative)

    // #2：一条弱支持 + 十条同源弱支持 → 不错误增强（同 witness 同簇，Independence 不涨）。
    let c2 = mkClaimLocal "ten-copies"
    let sharedW = mkWitnessLocal "shared"
    let w2First = mkObs c2 Supports Weak sharedW [ "src-x" ]
    let w2Copies = [ for i in 1..10 -> mkObs c2 Supports Weak sharedW [ "src-x" ] ]
    let clusters2 = dependencyClusters (w2First :: w2Copies)
    check "#2 ten same-receipt copies stay one cluster (no fake independence)" (List.length clusters2 = 1)

    check
        "#2 one weak observation does not amplify independence"
        ((gradeOfWarrants (w2First :: w2Copies) |> Option.map (fun g -> g.Independence)) = Some(Clusters 1))

    // #3：十条独立中等 + 一条弱观察 → 报告 grade 按引用集（不引用弱 → 中等保证）。
    let c3 = mkClaimLocal "ten-independent"

    let w3Indep =
        [ for i in 1..10 -> mkObs c3 Supports Moderate (mkWitnessLocal ("3-" + string i)) [ "src-ind-" + string i ] ]

    let w3Weak = mkObs c3 Supports Weak (mkWitnessLocal "3w") [ "src-weak" ]

    let l3 =
        foldAll
            MeditationLedger.Empty
            ([ ClaimFramed c3 ]
             @ List.map ContributionAccepted w3Indep
             @ [ ContributionAccepted w3Weak ])
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let indepGrade = gradeOfWarrants w3Indep |> Option.get
    check "#3 ten moderate + unreferenced weak keeps moderate guarantee" (indepGrade.Reliability = Corroborated)
    check "#3 independence reflects the ten sources" (indepGrade.Independence = Clusters 10)

    // #4：支持与反对适用不同时间/人群 → 不错误变成 Contested（不同 scope = 不同 claim 身份，
    // 各自独立极性；fold 纪律：warrant scope 必须等于 claim scope）。
    let scopeEarly: Scope =
        { Content = None
          Time = Some "early"
          Modality = None
          Population = None }

    let scopeLate: Scope =
        { Content = None
          Time = Some "late"
          Modality = None
          Population = None }

    let mkTemporal (sc: Scope) =
        { Id = ClaimId.ofProposition "temporal-claim" sc
          Statement = "temporal-claim"
          Role = Assertion
          Source = ByOracleProposal
          Scope = sc
          IntroducedBy = "" }

    let c4e = mkTemporal scopeEarly
    let c4l = mkTemporal scopeLate
    let w4s = mkObsForScope c4e Supports scopeEarly (mkWitnessLocal "4s") [ "s4" ]
    let w4o = mkObsForScope c4l Opposes scopeLate (mkWitnessLocal "4o") [ "s4b" ]

    let l4 =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed c4e
              ContributionAccepted w4s
              ClaimFramed c4l
              ContributionAccepted w4o ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check
        "#4 support(early) + oppose(late) are separate claims, not Contested"
        (polarityOf l4 c4e.Id scopeEarly = SupportedOnly
         && polarityOf l4 c4l.Id scopeLate = RefutedOnly)

    // #5：单个反例不得反驳统计性命题（rule 注册表）——已在 Properties 锚定，此处汇总断言。
    check
        "#5 statistical refutation is not registered (counterexample cannot refute statistical claim)"
        (not (List.contains RefutationRule.StatisticalRefutation registeredRefutationRules))

    // #6：没搜到反例 ≠ 支持——SearchAttempted(NoHit) 不产生支持 warrant。
    let c6 = mkClaimLocal "no-counterexample-found"

    let l6 =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed c6
              SearchAttempted
                  { ObligationId = "cx"
                    Outcome = NoHit
                    Sequence = 0 } ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    check "#6 no-hit does not manufacture support" (polarityOf l6 c6.Id scopeEarth = Unknown)

    // #7：添加无关证据不改变目标 grade（引用集隔离）——同一引用 proof 在有无无关证据的
    // 账本上得到相同 grade。
    let c7 = mkClaimLocal "grade-isolation"
    let w7 = mkObs c7 Supports Moderate (mkWitnessLocal "7") [ "src-7" ]
    let unrelated = mkClaimLocal "unrelated-7"
    let w7u = mkObs unrelated Supports Strong (mkWitnessLocal "7u") [ "src-7u" ]

    let l7a =
        foldAll MeditationLedger.Empty [ ClaimFramed c7; ContributionAccepted w7 ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let l7b =
        foldAll
            MeditationLedger.Empty
            [ ClaimFramed c7
              ContributionAccepted w7
              ClaimFramed unrelated
              ContributionAccepted w7u ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"

    let proofForW7 (l: MeditationLedger) : StopProof =
        // 从账本读入账后的 IntroducedBy（未入账的测试局部值恒为空串——139 版 review 已抓）。
        let digests =
            l.Warrants
            |> Map.toList
            |> List.choose (fun (_, w) ->
                if Warrant.claimId w = c7.Id then
                    Some((Warrant.data w).IntroducedBy)
                else
                    None)

        match
            StopProof.create
                [ { ObligationId = "cx"
                    DischargeEventDigests = digests } ]
                []
                OpenWorld
                (gradeOfWarrant w7)
                []
                digests
        with
        | Ok p -> p
        | Error e -> failwith e

    let g7a = Meditator.Stop.gradeForStopProof l7a (proofForW7 l7a)
    let g7b = Meditator.Stop.gradeForStopProof l7b (proofForW7 l7b)
    check "#7 unrelated ledger evidence does not change target grade" (g7a = g7b && g7a.Reliability = Corroborated)

    // #8：同一事实经三篇转述 = 一份来源（同 UltimateSourceIds → 同簇）。
    let c8 = mkClaimLocal "three-retellings"
    let w8a = mkObs c8 Supports Moderate (mkWitnessLocal "8a") [ "orig-source" ]
    let w8b = mkObs c8 Supports Moderate (mkWitnessLocal "8b") [ "orig-source" ]
    let w8c = mkObs c8 Supports Moderate (mkWitnessLocal "8c") [ "orig-source" ]
    let clusters8 = dependencyClusters [ w8a; w8b; w8c ]
    check "#8 three retellings of one fact are one cluster" (List.length clusters8 = 1)

    // #9：证据质量提高不使结论变差——同引用集下 grade 随证据质量单调。
    let c9 = mkClaimLocal "monotone-quality"
    let w9weak = mkObs c9 Supports Weak (mkWitnessLocal "9w") [ "src-9" ]
    let w9strong = mkObs c9 Supports Strong (mkWitnessLocal "9s") [ "src-9b" ]
    let weakG = gradeOfWarrant w9weak
    let strongG = gradeOfWarrant w9strong

    check
        "#9 stronger evidence never lowers the guarantee"
        (strongG.Reliability = Confirmed && weakG.Reliability = Tentative)

    // #10：预算变化不改变 truth-like 结论——已确认事实（账本）不因预算耗尽消失；
    // 耗尽只增加 unknowns（Inconclusive + 未决清单）。
    let c10 = mkClaimLocal "budget-truth"
    let w10 = mkObs c10 Supports Moderate (mkWitnessLocal "10") [ "src-10" ]

    let l10 =
        foldAll MeditationLedger.Empty [ ClaimFramed c10; ContributionAccepted w10 ]
        |> function
            | Ok l -> l
            | Error e -> failwith $"%A{e}"
    // 账本本身不含预算状态：budget 是控制层资源，不参与认识事实（§41.6）。
    check
        "#10 ledger truth does not depend on budget"
        (polarityOf l10 c10.Id scopeEarth = SupportedOnly
         && l10.ResourceUsage.CreditsConsumed = 0)
