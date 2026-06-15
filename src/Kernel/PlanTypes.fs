module VibeFs.Kernel.PlanTypes

type PlanLens =
    | DirectDelivery
    | ArchitectureFirst
    | RiskFirst
    | SimplificationFirst
    | CounterexampleFirst
    | CrossDomainFirst
    | ConstraintFirst

type PlanRequest =
    { requestId: string
      rawRequirement: string
      normalizedRequirement: string
      branchCount: int
      branchModelName: string
      judgeModelName: string
      outputFileName: string
      workspaceRoot: string
      existingContext: string option }

type PlanHypothesis =
    { hypothesisId: string
      text: string
      targetBranchIds: string list }

type PlanBranchCandidate =
    { branchId: string
      lens: PlanLens
      title: string
      candidatePlanMarkdown: string
      candidatePlanSummary: string
      keyAssumptions: string list
      keyRisks: string list
      validationChecks: string list
      selfCritique: string
      confidence: float }

type PlanBranchCritique =
    { branchId: string
      critiqueMarkdown: string
      criticalIssues: string list
      missingRequirements: string list
      counterexamples: string list
      improvementDirections: string list }

type PlanPoolEntry =
    { entryId: string
      branchId: string
      title: string
      contentMarkdown: string
      approachSummary: string
      confidence: float }

type PlanBranchRevisionData =
    { branchId: string
      lens: PlanLens
      title: string
      revisedPlanMarkdown: string
      revisedPlanSummary: string
      keyAssumptions: string list
      keyRisks: string list
      validationChecks: string list
      implementationSteps: string list
      selfCritique: string
      confidence: float }

type PlanBranchRevision =
    { data: PlanBranchRevisionData
      originalCandidate: PlanBranchCandidate
      critique: PlanBranchCritique
      pool: PlanPoolEntry list }

type PlanJudgeDecision =
    { winnerBranchId: string
      keptBranchIds: string list
      rejectedBranchIds: string list
      judgeReasoning: string
      mergeNotes: string list }

type PlanRunResult =
    { request: PlanRequest
      hypotheses: PlanHypothesis list
      revisions: PlanBranchRevision list
      decision: PlanJudgeDecision
      finalMarkdown: string
      finalFileName: string }

type PlanToolSchema =
    { name: string
      description: string
      parameters: obj }

type PlanToolCall = { toolName: string; arguments: obj }

type PlanModelCaller = string -> PlanToolSchema list -> Async<PlanToolCall list>
