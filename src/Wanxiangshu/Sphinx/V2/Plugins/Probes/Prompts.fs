namespace Wanxiangshu.Sphinx.V2.Plugins

open System

[<RequireQualifiedAccess>]
type QuestionTemplate =
    | PlanContributionPair
    | SmallSetRanking
    | RankingReversal
    | CombinationAndSequence
    | InvestigationValue
    | ImprovementMagnitude
    | CandidateRelations
    | AnswerNowVersusInvestigate

/// The versioned question templates.
///
/// WHAT[sphinx-v2-023]: a template's rendered text is hashed and persisted, so a
/// wording change is visible as a protocol change. Two templates that differ only in
/// text are different experiments, and the system may not declare them equivalent
/// because a string diff looked small.
///
/// WHAT[sphinx-v2-001]: the prompt tells the model to judge contribution to the
/// original goal, not name, length or invested cost. It never asks for probabilities,
/// confidence values or invented numeric weights.
module Prompts =

    /// The shared prefix every protocol compiler assembles. A probe never rewrites the
    /// goal: the compiler owns it, so no probe can silently substitute its own target.
    let goalPrefix
        (goalText: string)
        (visibleMaterials: string)
        (remainingBudget: string)
        (comparedPlans: string list)
        : string =
        String.concat
            "\n"
            [ "原目标：" + goalText
              "当前可用材料：" + visibleMaterials
              "后续可用资源：" + remainingBudget
              "本次比较的对象：" + String.concat "、" comparedPlans
              "请根据对原目标的预计贡献判断，而不是按名称、篇幅或已经投入的成本判断。"
              "请同时考虑执行消耗和执行之后仍能开展的工作。"
              "可以并列、暂不判断、给出改变结论的条件，或提出没有列出的更好路径。"
              "不要填写概率、置信度或自行编造数值权重。"
              "只返回本次响应 schema；理由可以用自然语言放在相应字段内。" ]

    /// The eight question primitives, as template ids. Each one asks for a relation,
    let templateId (template: QuestionTemplate) : string =
        match template with
        | QuestionTemplate.PlanContributionPair -> "sphinx.question.plan-contribution-pair@2"
        | QuestionTemplate.SmallSetRanking -> "sphinx.question.small-set-ranking@2"
        | QuestionTemplate.RankingReversal -> "sphinx.question.ranking-reversal@2"
        | QuestionTemplate.CombinationAndSequence -> "sphinx.question.combination-sequence@2"
        | QuestionTemplate.InvestigationValue -> "sphinx.question.investigation-value@2"
        | QuestionTemplate.ImprovementMagnitude -> "sphinx.question.improvement-magnitude@2"
        | QuestionTemplate.CandidateRelations -> "sphinx.question.candidate-relations@2"
        | QuestionTemplate.AnswerNowVersusInvestigate -> "sphinx.question.answer-now-versus-investigate@2"

    /// The default profile's question set. The others can be activated by a plan; they
    /// are not required of every inquiry.
    let defaultTemplates: QuestionTemplate list =
        [ QuestionTemplate.PlanContributionPair
          QuestionTemplate.RankingReversal
          QuestionTemplate.CombinationAndSequence
          QuestionTemplate.InvestigationValue
          QuestionTemplate.AnswerNowVersusInvestigate ]

    let private goalPrefixBody (template: QuestionTemplate) : string =
        match template with
        | QuestionTemplate.PlanContributionPair -> "优先执行哪条路径，预计更有助于达成原目标？不要仅比较眼前一步的产出；把各自完成后还可以做的工作算在内。"
        | QuestionTemplate.SmallSetRanking -> "在这些路径中，哪项预计贡献最大，哪项最小？无法区分的项可以并列；材料不足以判断的项单独列出。"
        | QuestionTemplate.RankingReversal -> "哪项当前尚未确定的信息，最可能改变当前先后？说明在条件成立和不成立时，各自更值得做什么。没有这样的具体条件也可以明确说明。"
        | QuestionTemplate.CombinationAndSequence -> "比较两条使用相同总预算的路径，P 与 Q 的先后恰好相反。哪些结果会改变后续选择？"
        | QuestionTemplate.InvestigationValue -> "比较：立即推进候选路径；先回答拟议问题再决定。考虑提问成本，以及回答有可能改变的实际行动，哪条路径更有贡献？"
        | QuestionTemplate.ImprovementMagnitude -> "比较两段改善：哪一段更能推进原目标？哪些条件会让两者顺序改变？"
        | QuestionTemplate.CandidateRelations -> "这些候选哪些是在相同条件下表达同一方案，哪些只是共享一部分机制？指出可以区分它们的条件；不能确定时保留分开。"
        | QuestionTemplate.AnswerNowVersusInvestigate ->
            "在当前剩余资源下，比较：现在用已有材料组织回答；执行具体计划后再回答。哪条路径预计更有助于原目标？指出继续工作能够改变答案的具体部分。"

    /// The rendered question under the shared prefix. `questionHash` is what a manifest
    /// persists, so a reworded question is a new experiment.
    let render (template: QuestionTemplate) (question: string) : string =
        String.concat "\n\n" [ goalPrefixBody template; question ]
