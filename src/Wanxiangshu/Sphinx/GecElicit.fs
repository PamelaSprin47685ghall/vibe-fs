// WHAT[EPI-023,EPI-025,EPI-029]: Gec composition over split-ballot, self-prediction and stop certificates.
namespace Wanxiangshu.Sphinx

open System
open Fable.Core.JsInterop
open Wanxiangshu.Sphinx.Plugins.Questionnaire
open Wanxiangshu.Sphinx.Plugins.Truthful
open Wanxiangshu.Sphinx.Plugins.Stop

module GecElicit =

    let private isFiniteNumber (value: float) : bool =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private isUndefined (value: obj) : bool = emitJsExpr value "$0 === undefined"

    let private isNullish (value: obj) : bool = isNull value || isUndefined value

    let private isJsArray (value: obj) : bool = emitJsExpr value "Array.isArray($0)"

    let private fieldOf (value: obj) (name: string) : obj =
        if isNullish value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private textOf (value: obj) : string =
        if isNullish value then "" else string value

    let private arrayOf (value: obj) : obj array =
        if isNullish value || not (isJsArray value) then
            [||]
        else
            unbox<obj array> value

    let private stringArrayOf (value: obj) : string list =
        arrayOf value |> Array.map textOf |> Array.toList

    let private keysOf (value: obj) : string array = emitJsExpr value "Object.keys($0)"

    let private floatField (value: obj) (name: string) : float option =
        let entry = fieldOf value name

        if isNullish entry then
            None
        else
            let number: float = emitJsExpr entry "$0"

            if isFiniteNumber number then Some number else None

    let private boolField (value: obj) (name: string) (fallback: bool) : bool =
        let entry = fieldOf value name

        if isNullish entry then fallback else unbox<bool> entry

    let private floatMapKeep (value: obj) : Map<string, float> =
        if isNullish value then
            Map.empty
        else
            keysOf value
            |> Array.toList
            |> List.map (fun key ->
                let number: float = emitJsExpr (value, key) "$0[$1]"
                key, number)
            |> Map.ofList

    let private typedError (code: string) (message: string) : obj =
        box
            {| ok = false
               error = box {| code = code; message = message |} |}

    let private stringError (message: string) : obj = box {| ok = false; error = message |}

    let private okResult (fields: (string * obj) list) : obj = ("ok", box true) :: fields |> createObj

    let private treatmentDetailsOf (input: obj) (name: string) : obj =
        fieldOf (fieldOf input "treatmentDetails") name

    let private treatmentPolarityOf (details: obj) : Result<int, string> =
        match floatField details "polarity" with
        | None -> Ok 1
        | Some value when value = 1.0 -> Ok 1
        | Some value when value = -1.0 -> Ok -1
        | Some _ -> Error "treatment polarity must be 1 or -1"

    let private treatmentOf (input: obj) (name: string) : Result<Protocol.Treatment, string> =
        let details = treatmentDetailsOf input name

        let wording =
            let text = textOf (fieldOf details "wording")
            if String.IsNullOrWhiteSpace text then name else text

        treatmentPolarityOf details
        |> Result.map (fun polarity ->
            ({ Name = name
               Wording = wording
               Polarity = polarity
               OpenFirst = boolField details "openFirst" true }
            : Protocol.Treatment))

    let private assignmentWordingOf
        (envelope: Protocol.SubjectEnvelope)
        (treatments: Protocol.Treatment list)
        : string =
        treatments
        |> List.tryFind (fun treatment -> treatment.Name = envelope.Treatment)
        |> Option.map (fun treatment -> treatment.Wording)
        |> Option.defaultValue envelope.Treatment

    let private assignmentPolarityOf (envelope: Protocol.SubjectEnvelope) (treatments: Protocol.Treatment list) : int =
        treatments
        |> List.tryFind (fun treatment -> treatment.Name = envelope.Treatment)
        |> Option.map (fun treatment -> treatment.Polarity)
        |> Option.defaultValue 1

    let private assignmentOpenFirstOf
        (envelope: Protocol.SubjectEnvelope)
        (treatments: Protocol.Treatment list)
        : bool =
        treatments
        |> List.tryFind (fun treatment -> treatment.Name = envelope.Treatment)
        |> Option.map (fun treatment -> treatment.OpenFirst)
        |> Option.defaultValue true

    let private assignmentView (treatments: Protocol.Treatment list) (envelope: Protocol.SubjectEnvelope) : obj =
        box
            {| subject = envelope.Subject
               treatment = envelope.Treatment
               wording = assignmentWordingOf envelope treatments
               polarity = assignmentPolarityOf envelope treatments
               openFirst = assignmentOpenFirstOf envelope treatments
               blindToken = envelope.BlindToken
               labelPermutation = envelope.LabelPermutation |> List.toArray |> box
               orderPermutation = envelope.OrderPermutation |> List.toArray |> box |}

    let private splitContrast
        (seed: int)
        (allocation: Protocol.Allocation)
        (treatment: string)
        (control: string)
        (outcomesRaw: obj)
        (assignments: obj)
        : obj =
        let responses: Protocol.ArmOutcome list =
            keysOf outcomesRaw
            |> Array.toList
            |> List.map (fun subject ->
                { Subject = subject
                  Response =
                    match floatField outcomesRaw subject with
                    | Some number -> number
                    | None -> Double.NaN })

        let request: Protocol.ContrastInput =
            { Assignment =
                allocation.Envelopes
                |> List.map (fun envelope -> envelope.Subject, envelope.Treatment)
                |> Map.ofList
              Seed = seed
              Outcomes = responses
              Control = control
              Treatment = treatment
              Permutations = 1024 }

        match Protocol.contrast request with
        | Error fault ->
            let code = Protocol.contrastErrorCode fault
            typedError code code
        | Ok contrast ->
            let effect =
                box
                    {| estimand = contrast.Estimand
                       estimate = contrast.Estimate
                       treatment = contrast.Treatment
                       control = contrast.Control
                       treatmentMean = contrast.TreatmentMean
                       controlMean = contrast.ControlMean
                       treatmentN = contrast.TreatmentN
                       controlN = contrast.ControlN
                       assumptions = contrast.Assumptions |> Set.toArray |> box
                       uncertainty =
                        box
                            {| kind = "permutation-null"
                               pValue = contrast.PermutationP
                               nullPermutations = contrast.NullPermutations |}
                       permutationNull =
                        box
                            {| pValue = contrast.PermutationP
                               nullPermutations = contrast.NullPermutations |} |}

            okResult [ "assignments", assignments; "effect", effect ]

    let private renderContrast
        (seed: int)
        (allocation: Protocol.Allocation)
        (outcomesRaw: obj)
        (assignments: obj)
        (contrastRaw: obj)
        : obj =
        match arrayOf contrastRaw |> Array.map textOf |> Array.toList with
        | [ treatment; control ] -> splitContrast seed allocation treatment control outcomesRaw assignments
        | _ -> typedError "invalid-contrast" "contrast needs a treatment and a control arm"

    let private splitOutcome
        (input: obj)
        (seed: int)
        (allocation: Protocol.Allocation)
        (treatments: Protocol.Treatment list)
        : obj =
        let assignments =
            allocation.Envelopes
            |> List.map (assignmentView treatments)
            |> List.toArray
            |> box

        let outcomesRaw = fieldOf input "outcomes"
        let contrastRaw = fieldOf input "contrast"

        if isNullish outcomesRaw || isNullish contrastRaw then
            okResult [ "assignments", assignments ]
        else
            renderContrast seed allocation outcomesRaw assignments contrastRaw

    let private buildTreatments (input: obj) : Result<Protocol.Treatment list, string> =
        stringArrayOf (fieldOf input "treatments")
        |> List.map (treatmentOf input)
        |> List.fold
            (fun acc entry ->
                acc
                |> Result.bind (fun built -> entry |> Result.map (fun treatment -> treatment :: built)))
            (Ok [])
        |> Result.map List.rev

    let private runAllocation (input: obj) (seed: int) (snapshot: string) (treatments: Protocol.Treatment list) : obj =
        let request: Protocol.AllocationInput =
            { Seed = seed
              RootSnapshotHash = snapshot
              Subjects = stringArrayOf (fieldOf input "subjects")
              Treatments = treatments
              Candidates = stringArrayOf (fieldOf input "candidates") }

        match Protocol.allocate request with
        | Error fault ->
            let code = Protocol.allocationErrorCode fault
            typedError code code
        | Ok(allocation: Protocol.Allocation) -> splitOutcome input seed allocation treatments

    let private allocateBallot (input: obj) (seed: int) (snapshot: string) : obj =
        match buildTreatments input with
        | Error message -> typedError "invalid-polarity" message
        | Ok treatments -> runAllocation input seed snapshot treatments

    let splitBallot (input: obj) : obj =
        let primary = textOf (fieldOf input "rootSnapshot")

        let snapshot =
            if String.IsNullOrWhiteSpace primary then
                textOf (fieldOf input "rootSnapshotHash")
            else
                primary

        if String.IsNullOrWhiteSpace snapshot then
            stringError "splitBallot needs a rootSnapshot before randomization"
        else
            let seed = floatField input "seed" |> Option.map int |> Option.defaultValue 0

            allocateBallot input seed snapshot

    let selfPrediction (input: obj) : obj =
        let request: SelfPrediction.AssessmentInput =
            { WorkId = textOf (fieldOf input "workId")
              Forecast = floatMapKeep (fieldOf input "predicted")
              Outcome = textOf (fieldOf input "outcome")
              Epsilon = floatField input "epsilon" |> Option.defaultValue 0.0
              CommittedBeforeStimulus = boolField input "committedBeforeStimulus" false
              HeldOut = boolField input "heldOut" false }

        match SelfPrediction.assess request with
        | Error fault -> stringError (SelfPrediction.predictionErrorCode fault)
        | Ok assessment ->
            okResult
                [ "workId", box assessment.WorkId
                  "epsilon", box assessment.Epsilon
                  "logScore", box assessment.LogScore
                  "brierScore", box assessment.BrierScore
                  "calibration",
                  box
                      {| predicted = assessment.Calibration.Predicted
                         resolved = assessment.Calibration.Resolved
                         heldOut = assessment.Calibration.HeldOut |}
                  "sharpness", box assessment.Sharpness
                  "calibrationUpdateAllowed", box assessment.CalibrationUpdateAllowed ]

    let private pairRange (first: obj) (second: obj) : float =
        let lo: float = emitJsExpr first "$0"
        let hi: float = emitJsExpr second "$0"

        if isFiniteNumber lo && isFiniteNumber hi then
            abs (hi - lo)
        else
            0.0

    let private pairWidth (items: obj list) : float =
        match items with
        | [ first; second ] -> pairRange first second
        | _ -> 0.0

    let private bandWidth (stability: obj) (name: string) : float =
        let band = fieldOf stability name

        if not (isJsArray band) then
            0.0
        else
            pairWidth (unbox<obj array> band |> Array.toList)

    let private rangeWidths (stability: obj) : float list =
        keysOf stability |> Array.toList |> List.map (bandWidth stability)

    let private maxRangeWidth (ranges: float list) : float =
        match ranges with
        | [] -> 0.0
        | _ -> ranges |> List.max

    let private reversalBoundOf (stability: obj) : float =
        if isNullish stability then
            0.0
        else
            maxRangeWidth (rangeWidths stability)

    let private stopView (decisions: Certificate.DecisionMass list) (certificate: Certificate.StopCertificate) : obj =
        let minorityView =
            Certificate.answerModes certificate.Answer
            |> List.map (fun mode ->
                box
                    {| decision = mode.Decision
                       mass = mode.Probability |})
            |> List.toArray
            |> box

        let decision =
            if Certificate.answerKind certificate.Answer = "single-winner" then
                box
                    {| kind = "single-winner"
                       winner =
                        Certificate.answerWinner certificate.Answer
                        |> Option.defaultValue certificate.TopDecision |}
            else
                box
                    {| kind = "decision-distribution"
                       modes =
                        decisions
                        |> List.map (fun mode ->
                            box
                                {| decision = mode.Decision
                                   mass = mode.Probability |})
                        |> List.toArray
                        |> box
                       minorityModes = minorityView |}

        let voc =
            match certificate.Voc with
            | Some band ->
                box
                    {| point = band.Point
                       upper = band.Upper
                       threshold = band.Threshold
                       belowCost = band.BelowCost |}
            | None -> null

        okResult
            [ "certificate",
              box
                  {| testedFamily = certificate.TestedFamily |> List.toArray |> box
                     scope = certificate.Scope
                     guarantee = certificate.Guarantee
                     sequentialAlpha = certificate.SequentialAlpha
                     sequentialError =
                      box
                          {| cumulativeError = certificate.CumulativeError
                             method = certificate.SequentialMethod |}
                     checks =
                      certificate.Checks
                      |> List.map (fun check ->
                          box
                              {| check = check.Check
                                 passed = check.Passed |})
                      |> List.toArray
                      |> box |}
              "decision", decision
              "voc", voc
              "recommendation", box (Certificate.verdictName certificate.Verdict) ]

    let private decideStop
        (input: obj)
        (framings: string list)
        (posterior: Map<string, float>)
        (checks: int)
        (alpha: float)
        (voc: Certificate.VocBand option)
        : obj =
        let decisions: Certificate.DecisionMass list =
            posterior
            |> Map.toList
            |> List.sortBy (fun (name, mass) -> -mass, name)
            |> List.map (fun (name, mass) -> { Decision = name; Probability = mass })

        let top =
            decisions
            |> List.tryHead
            |> Option.map (fun decision -> decision.Decision)
            |> Option.defaultValue ""

        let minors =
            if boolField input "minorityStable" false then
                decisions |> List.filter (fun decision -> decision.Decision <> top)
            else
                []

        let request: Certificate.StopInput =
            { Decisions = decisions
              TestedFramings = framings
              ReversalBound = reversalBoundOf (fieldOf input "framingStability")
              Evidence = floatField input "evidence" |> Option.defaultValue 0.0
              ErrorBudget = alpha
              ChecksPerformed = checks
              RequiredCoverage = 0.5
              MinorityThreshold = 0.05
              MinorModes = minors
              Voc = voc }

        match Certificate.decide request with
        | Error fault ->
            let code = Certificate.stopErrorCode fault
            typedError code code
        | Ok certificate -> stopView decisions certificate

    let stopCertificate (input: obj) : obj =
        let framings = stringArrayOf (fieldOf input "testedFramings")
        let posterior = floatMapKeep (fieldOf input "decisionPosterior")

        let checks =
            floatField input "checksSoFar" |> Option.map int |> Option.defaultValue 1

        let alpha = floatField input "alpha" |> Option.defaultValue 0.05
        let vocRaw = fieldOf input "voc"

        let voc: Certificate.VocBand option =
            if isNullish vocRaw then
                None
            else
                Some
                    { Point = floatField vocRaw "point" |> Option.defaultValue Double.NaN
                      Upper = floatField vocRaw "upper" |> Option.defaultValue Double.NaN
                      Threshold = floatField vocRaw "threshold" |> Option.defaultValue Double.NaN }

        match voc with
        | Some band when
            isFiniteNumber band.Point
            && isFiniteNumber band.Upper
            && band.Upper < band.Point
            ->
            typedError "invalid-voc-upper" "voc upper must stay above the point estimate"
        | _ -> decideStop input framings posterior checks alpha voc

    let methods: (string * obj) list =
        [ "splitBallot", box splitBallot
          "selfPrediction", box selfPrediction
          "stopCertificate", box stopCertificate ]
