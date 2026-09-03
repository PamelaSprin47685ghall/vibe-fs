namespace Wanxiangshu.Sphinx.Plugins.Stop

open System

module Certificate =
    type DecisionMass =
        { Decision: string
          Probability: float }

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

    type CheckOutcome =
        { Check: string
          Passed: bool }

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
        let rec loop seen rest =
            match rest with
            | [] -> None
            | head :: tail ->
                if Set.contains head seen then
                    Some head
                else
                    loop (Set.add head seen) tail

        loop Set.empty items

    let decide (input: StopInput) =
        if List.isEmpty input.Decisions then
            Error EmptyDecisions
        elif List.isEmpty input.TestedFramings then
            Error EmptyTestedFramings
        else
            match firstDuplicate (input.Decisions |> List.map (fun decision -> decision.Decision)) with
            | Some decision -> Error(DuplicateDecision decision)
            | None ->
                match
                    input.Decisions
                    |> List.tryFind (fun decision -> not (finite decision.Probability) || decision.Probability < 0.0 || decision.Probability > 1.0)
                with
                | Some decision -> Error(InvalidDecisionMass decision.Decision)
                | None ->
                    let total =
                        input.Decisions |> List.sumBy (fun decision -> decision.Probability)

                    if abs (total - 1.0) > simplexTolerance then
                        Error(SimplexViolation total)
                    elif not (finite input.ReversalBound) || input.ReversalBound < 0.0 || input.ReversalBound > 1.0 then
                        Error InvalidReversalBound
                    elif not (finite input.Evidence) || input.Evidence < 0.0 then
                        Error InvalidEvidence
                    elif not (finite input.ErrorBudget) || input.ErrorBudget <= 0.0 || input.ErrorBudget >= 1.0 then
                        Error InvalidErrorBudget
                    elif input.ChecksPerformed < 1 then
                        Error InvalidChecks
                    elif not (finite input.RequiredCoverage) || input.RequiredCoverage <= 0.0
                         || input.RequiredCoverage >= 1.0 then
                        Error InvalidRequiredCoverage
                    elif not (finite input.MinorityThreshold) || input.MinorityThreshold <= 0.0
                         || input.MinorityThreshold >= 1.0 then
                        Error InvalidMinorityThreshold
                    else
                        let known =
                            input.Decisions |> List.map (fun decision -> decision.Decision) |> Set.ofList

                        match
                            input.MinorModes
                            |> List.tryFind (fun mode ->
                                not (finite mode.Probability) || mode.Probability < 0.0 || mode.Probability > 1.0)
                        with
                        | Some mode -> Error(InvalidMinorMass mode.Decision)
                        | None ->
                            match input.MinorModes |> List.tryFind (fun mode -> not (Set.contains mode.Decision known)) with
                            | Some mode -> Error(UnknownMinorMode mode.Decision)
                            | None ->
                                let vocValid =
                                    match input.Voc with
                                    | None -> Ok None
                                    | Some band ->
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
                                                Some
                                                    { Point = band.Point
                                                      Upper = band.Upper
                                                      Threshold = band.Threshold
                                                      BelowCost = band.Upper <= band.Threshold }
                                            )

                                match vocValid with
                                | Error error -> Error error
                                | Ok voc ->
                                    let ranked =
                                        input.Decisions
                                        |> List.sortBy (fun decision -> -decision.Probability, decision.Decision)

                                    let top = ranked |> List.head
                                    let sequentialAlpha = input.ErrorBudget / float input.ChecksPerformed
                                    let cumulativeError = sequentialAlpha * float input.ChecksPerformed
                                    let evidenceThreshold = 1.0 / sequentialAlpha

                                    let vocPasses =
                                        voc |> Option.forall (fun band -> band.BelowCost)

                                    let checks =
                                        [ { Check = "sequential-evidence"
                                            Passed = input.Evidence >= evidenceThreshold }
                                          { Check = "major-mode-coverage"
                                            Passed = top.Probability >= input.RequiredCoverage }
                                          { Check = "framing-reversal"
                                            Passed = input.ReversalBound <= 1.0 - input.RequiredCoverage }
                                          { Check = "voc-below-cost"
                                            Passed = vocPasses } ]

                                    let verdict =
                                        if checks |> List.forall (fun check -> check.Passed) then
                                            Stop
                                        else
                                            Continue

                                    let stableMinority =
                                        input.MinorModes
                                        |> List.filter (fun mode -> mode.Probability >= input.MinorityThreshold)
                                        |> List.sortBy (fun mode -> -mode.Probability, mode.Decision)

                                    let answer =
                                        match stableMinority with
                                        | [] -> SingleWinner top.Decision
                                        | modes -> DecisionDistribution modes

                                    // H-1a: caller Evidence is range-checked only; without producer
                                    // attestation of an e-value/e-process construction the
                                    // anytime-valid claim is unverified, so record the downgraded label.
                                    let assumptions =
                                        [ "sequential-evidence-claimed-unverified"
                                          "conservative-upper-voc"
                                          "minority-modes-preserved"
                                          "no-universal-framing-claim"
                                          "sequential-error-budget"
                                          "tested-framing-family-only"
                                          if Option.isNone voc then
                                              "no-voc-evidence-provided" ]
                                        |> Set.ofList

                                    Ok
                                        { Verdict = verdict
                                          Checks = checks
                                          Answer = answer
                                          TopDecision = top.Decision
                                          TopMass = top.Probability
                                          TestedFamily = input.TestedFramings
                                          Scope =
                                            "tested-framing-family:" + String.concat "," input.TestedFramings
                                          Guarantee = "decision-stability-bounded-within-tested-framing-family"
                                          SequentialAlpha = sequentialAlpha
                                          CumulativeError = cumulativeError
                                          SequentialMethod = "alpha-spending-sequential"
                                          Voc = voc
                                          Assumptions = assumptions }
