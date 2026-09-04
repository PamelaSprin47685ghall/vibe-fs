namespace Wanxiangshu.Sphinx.Plugins.Stop

open System
open FsToolkit.ErrorHandling

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
          SequentialAlpha: float
          CumulativeError: float
          SequentialMethod: string
          Voc: VocOutcome option
          Assumptions: Set<string> }

    let verdictName =
        function
        | Stop -> "stop"
        | Continue -> "continue"

    let answerKind =
        function
        | SingleWinner _ -> "single-winner"
        | DecisionDistribution _ -> "decision-distribution"

    let answerWinner =
        function
        | SingleWinner winner -> Some winner
        | DecisionDistribution _ -> None

    let answerModes =
        function
        | SingleWinner _ -> []
        | DecisionDistribution modes -> modes

    let stopErrorCode =
        function
        | EmptyDecisions -> "empty-decisions"
        | DuplicateDecision _ -> "duplicate-decision"
        | InvalidDecisionMass _ -> "invalid-decision-mass"
        | SimplexViolation _ -> "simplex-violation"
        | EmptyTestedFramings -> "empty-tested-framings"
        | InvalidReversalBound -> "invalid-reversal-bound"
        | InvalidEvidence -> "invalid-evidence"
        | InvalidErrorBudget -> "invalid-error-budget"
        | InvalidChecks -> "invalid-checks"
        | InvalidRequiredCoverage -> "invalid-required-coverage"
        | InvalidMinorityThreshold -> "invalid-minority-threshold"
        | InvalidMinorMass _ -> "invalid-minor-mass"
        | UnknownMinorMode _ -> "unknown-minor-mode"
        | InvalidVocPoint -> "invalid-voc-point"
        | InvalidVocUpper -> "invalid-voc-upper"
        | InvalidVocThreshold -> "invalid-voc-threshold"
        | InvertedVocBand -> "inverted-voc-band"

    let private simplexTolerance = 1e-9

    let private finite value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private firstDuplicate (items: string list) : string option =
        let rec loop (seen: Set<string>) (rest: string list) : string option =
            match rest with
            | [] -> None
            | head :: tail when Set.contains head seen -> Some head
            | head :: tail -> loop (Set.add head seen) tail

        loop Set.empty items

    let private checkNonEmptyDecisions (input: StopInput) : Result<unit, StopError> =
        if List.isEmpty input.Decisions then
            Error EmptyDecisions
        else
            Ok()

    let private checkNonEmptyFramings (input: StopInput) : Result<unit, StopError> =
        if List.isEmpty input.TestedFramings then
            Error EmptyTestedFramings
        else
            Ok()

    let private checkDuplicateDecision (input: StopInput) : Result<unit, StopError> =
        match firstDuplicate (input.Decisions |> List.map (fun decision -> decision.Decision)) with
        | Some decision -> Error(DuplicateDecision decision)
        | None -> Ok()

    let private checkDecisionMass (input: StopInput) : Result<unit, StopError> =
        match
            input.Decisions
            |> List.tryFind (fun decision ->
                not (finite decision.Probability)
                || decision.Probability < 0.0
                || decision.Probability > 1.0)
        with
        | Some decision -> Error(InvalidDecisionMass decision.Decision)
        | None -> Ok()

    let private checkSimplexTotal (input: StopInput) : Result<unit, StopError> =
        let total = input.Decisions |> List.sumBy (fun decision -> decision.Probability)

        if abs (total - 1.0) > simplexTolerance then
            Error(SimplexViolation total)
        else
            Ok()

    let private checkInputBounds (input: StopInput) : Result<unit, StopError> =
        if
            not (finite input.ReversalBound)
            || input.ReversalBound < 0.0
            || input.ReversalBound > 1.0
        then
            Error InvalidReversalBound
        elif not (finite input.Evidence) || input.Evidence < 0.0 then
            Error InvalidEvidence
        elif
            not (finite input.ErrorBudget)
            || input.ErrorBudget <= 0.0
            || input.ErrorBudget >= 1.0
        then
            Error InvalidErrorBudget
        elif input.ChecksPerformed < 1 then
            Error InvalidChecks
        elif
            not (finite input.RequiredCoverage)
            || input.RequiredCoverage <= 0.0
            || input.RequiredCoverage >= 1.0
        then
            Error InvalidRequiredCoverage
        elif
            not (finite input.MinorityThreshold)
            || input.MinorityThreshold <= 0.0
            || input.MinorityThreshold >= 1.0
        then
            Error InvalidMinorityThreshold
        else
            Ok()

    let private checkMinorMass (input: StopInput) : Result<unit, StopError> =
        match
            input.MinorModes
            |> List.tryFind (fun mode ->
                not (finite mode.Probability)
                || mode.Probability < 0.0
                || mode.Probability > 1.0)
        with
        | Some mode -> Error(InvalidMinorMass mode.Decision)
        | None -> Ok()

    let private checkUnknownMinor (input: StopInput) : Result<unit, StopError> =
        let known =
            input.Decisions |> List.map (fun decision -> decision.Decision) |> Set.ofList

        match
            input.MinorModes
            |> List.tryFind (fun mode -> not (Set.contains mode.Decision known))
        with
        | Some mode -> Error(UnknownMinorMode mode.Decision)
        | None -> Ok()

    let private checkVocBand (band: VocBand) : Result<VocOutcome, StopError> =
        if not (finite band.Point) then
            Error InvalidVocPoint
        elif not (finite band.Upper) then
            Error InvalidVocUpper
        elif not (finite band.Threshold) || band.Threshold < 0.0 then
            Error InvalidVocThreshold
        elif band.Upper < band.Point then
            Error InvertedVocBand
        else
            Ok(
                ({ Point = band.Point
                   Upper = band.Upper
                   Threshold = band.Threshold
                   BelowCost = band.Upper <= band.Threshold }
                : VocOutcome)
            )

    let private validateVoc (input: StopInput) : Result<VocOutcome option, StopError> =
        match input.Voc with
        | None -> Ok None
        | Some band -> checkVocBand band |> Result.map Some

    let private decideVerdict (checks: CheckOutcome list) : Verdict =
        if checks |> List.forall (fun check -> check.Passed) then
            Stop
        else
            Continue

    let private decideAnswer (top: DecisionMass) (stableMinority: DecisionMass list) : DecisionAnswer =
        match stableMinority with
        | [] -> SingleWinner top.Decision
        | modes -> DecisionDistribution modes

    // H-1a: caller Evidence is range-checked only; without producer
    // attestation of an e-value/e-process construction the
    // anytime-valid claim is unverified, so record the downgraded label.
    let private certificateAssumptions (voc: VocOutcome option) : Set<string> =
        [ "sequential-evidence-claimed-unverified"
          "conservative-upper-voc"
          "minority-modes-preserved"
          "no-universal-framing-claim"
          "sequential-error-budget"
          "tested-framing-family-only"
          if Option.isNone voc then
              "no-voc-evidence-provided" ]
        |> Set.ofList

    let private buildCertificate (input: StopInput) (voc: VocOutcome option) : StopCertificate =
        let ranked =
            input.Decisions
            |> List.sortBy (fun decision -> -decision.Probability, decision.Decision)

        let top = ranked |> List.head
        let sequentialAlpha = input.ErrorBudget / float input.ChecksPerformed
        let cumulativeError = sequentialAlpha * float input.ChecksPerformed
        let evidenceThreshold = 1.0 / sequentialAlpha

        let vocPasses = voc |> Option.forall (fun band -> band.BelowCost)

        let checks: CheckOutcome list =
            [ { Check = "sequential-evidence"
                Passed = input.Evidence >= evidenceThreshold }
              { Check = "major-mode-coverage"
                Passed = top.Probability >= input.RequiredCoverage }
              { Check = "framing-reversal"
                Passed = input.ReversalBound <= 1.0 - input.RequiredCoverage }
              { Check = "voc-below-cost"
                Passed = vocPasses } ]

        let stableMinority =
            input.MinorModes
            |> List.filter (fun mode -> mode.Probability >= input.MinorityThreshold)
            |> List.sortBy (fun mode -> -mode.Probability, mode.Decision)

        { Verdict = decideVerdict checks
          Checks = checks
          Answer = decideAnswer top stableMinority
          TopDecision = top.Decision
          TopMass = top.Probability
          TestedFamily = input.TestedFramings
          Scope = "tested-framing-family:" + String.concat "," input.TestedFramings
          Guarantee = "decision-stability-bounded-within-tested-framing-family"
          SequentialAlpha = sequentialAlpha
          CumulativeError = cumulativeError
          SequentialMethod = "bonferroni-fixed-split"
          Voc = voc
          Assumptions = certificateAssumptions voc }

    let decide (input: StopInput) =
        result {
            do! checkNonEmptyDecisions input
            do! checkNonEmptyFramings input
            do! checkDuplicateDecision input
            do! checkDecisionMass input
            do! checkSimplexTotal input
            do! checkInputBounds input
            do! checkMinorMass input
            do! checkUnknownMinor input
            let! voc = validateVoc input
            return buildCertificate input voc
        }
