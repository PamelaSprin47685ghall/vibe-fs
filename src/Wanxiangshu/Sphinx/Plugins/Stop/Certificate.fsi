namespace Wanxiangshu.Sphinx.Plugins.Stop

module Certificate =
    type DecisionMass =
        { Decision: string; Probability: float }

    type VocBand =
        { Point: float
          Upper: float
          Threshold: float }

    type StopInput =
        { Decisions: DecisionMass list
          TestedFramings: string list
          ReversalBound: float
          Evidence: float
          ErrorBudget: float
          ChecksPerformed: int
          RequiredCoverage: float
          MinorityThreshold: float
          MinorModes: DecisionMass list
          Voc: VocBand option }

    type StopError =
        | EmptyDecisions
        | DuplicateDecision of string
        | InvalidDecisionMass of string
        | SimplexViolation of float
        | EmptyTestedFramings
        | InvalidReversalBound
        | InvalidEvidence
        | InvalidErrorBudget
        | InvalidChecks
        | InvalidRequiredCoverage
        | InvalidMinorityThreshold
        | InvalidMinorMass of string
        | UnknownMinorMode of string
        | InvalidVocPoint
        | InvalidVocUpper
        | InvalidVocThreshold
        | InvertedVocBand

    type Verdict =
        | Stop
        | Continue

    type DecisionAnswer =
        | SingleWinner of string
        | DecisionDistribution of DecisionMass list

    type CheckOutcome = { Check: string; Passed: bool }

    type VocOutcome =
        { Point: float
          Upper: float
          Threshold: float
          BelowCost: bool }

    type StopCertificate =
        { Verdict: Verdict
          Checks: CheckOutcome list
          Answer: DecisionAnswer
          TopDecision: string
          TopMass: float
          TestedFamily: string list
          Scope: string
          Guarantee: string
          RequiredCoverage: float
          MinorityThreshold: float
          SequentialAlpha: float
          CumulativeError: float
          SequentialMethod: string
          Voc: VocOutcome option
          Assumptions: Set<string> }

    val verdictName: Verdict -> string
    val answerKind: DecisionAnswer -> string
    val answerWinner: DecisionAnswer -> string option
    val answerModes: DecisionAnswer -> DecisionMass list
    val stopErrorCode: StopError -> string
    val decide: input: StopInput -> Result<StopCertificate, StopError>
