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
