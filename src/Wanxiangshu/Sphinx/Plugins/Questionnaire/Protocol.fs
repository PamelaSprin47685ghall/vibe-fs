namespace Wanxiangshu.Sphinx.Plugins.Questionnaire

open System
open Wanxiangshu.Sphinx.Core

module Protocol =
    type Treatment =
        { Name: string
          Wording: string
          Polarity: int
          OpenFirst: bool }

    type AllocationInput =
        { Seed: int
          RootSnapshotHash: string
          Subjects: string list
          Treatments: Treatment list
          Candidates: string list }

    type SubjectEnvelope =
        { Subject: string
          Treatment: string
          TreatmentIndex: int
          Wording: string
          Polarity: int
          LabelPermutation: string list
          OrderPermutation: string list
          BlindToken: string }

    type AllocationError =
        | EmptySubjects
        | EmptyTreatments
        | EmptyCandidates
        | BlankRootSnapshotHash
        | DuplicateSubject of string
        | DuplicateTreatment of string
        | DuplicateCandidate of string
        | InvalidPolarity of string

    type Allocation =
        { RootSnapshotHash: string
          Seed: int
          Envelopes: SubjectEnvelope list
          Exposure: Map<string, int>
          BlockCount: int
          Assumptions: Set<string> }

    type ArmOutcome =
        { Subject: string
          Response: float }

    type ContrastInput =
        { Assignment: Map<string, string>
          Seed: int
          Outcomes: ArmOutcome list
          Control: string
          Treatment: string
          Permutations: int }

    type ContrastError =
        | UnknownTreatment of string
        | SameArm
        | EmptyArm of string
        | DuplicateOutcome of string
        | UnknownOutcomeSubject of string
        | NonFiniteResponse of string
        | NonPositivePermutations

    type Contrast =
        { Treatment: string
          Control: string
          TreatmentMean: float
          ControlMean: float
          Estimate: float
          TreatmentN: int
          ControlN: int
          ExcludedSubjects: string list
          PermutationP: float
          NullPermutations: int
          Estimand: string
          Assumptions: Set<string> }

    type CarryoverInput =
        { Responses: ArmOutcome list
          PriorExposure: Map<string, string>
          CurrentTreatment: Map<string, string>
          FocalCurrent: string
          Control: string
          Treatment: string
          Permutations: int }

    type CarryoverError =
        | UnknownPriorArm of string
        | SamePriorArm
        | MissingPriorExposure of string
        | MissingCurrentTreatment of string
        | DuplicateResponse of string
        | UnknownResponseSubject of string
        | NonFiniteResponse of string
        | NonPositivePermutations
        | EmptyPriorArm of string

    type Carryover =
        { FocalCurrent: string
          Treatment: string
          Control: string
          TreatmentMean: float
          ControlMean: float
          Estimate: float
          TreatmentN: int
          ControlN: int
          ExcludedSubjects: string list
          PermutationP: float
          NullPermutations: int
          Estimand: string
          Assumptions: Set<string> }

    type ResponseCommit =
        { Subject: string
          Digest: string }

    let maxNullPermutations = 1024

    let allocationErrorCode =
        function
        | EmptySubjects -> "empty-subjects"
        | EmptyTreatments -> "empty-treatments"
        | EmptyCandidates -> "empty-candidates"
        | BlankRootSnapshotHash -> "blank-root-snapshot"
        | DuplicateSubject _ -> "duplicate-subject"
        | DuplicateTreatment _ -> "duplicate-treatment"
        | DuplicateCandidate _ -> "duplicate-candidate"
        | InvalidPolarity _ -> "invalid-polarity"

    let contrastErrorCode =
        function
        | UnknownTreatment _ -> "unknown-treatment"
        | SameArm -> "same-arm"
        | EmptyArm _ -> "empty-arm"
        | DuplicateOutcome _ -> "duplicate-outcome"
        | UnknownOutcomeSubject _ -> "unknown-outcome-subject"
        | NonFiniteResponse _ -> "non-finite-response"
        | NonPositivePermutations -> "non-positive-permutations"

    let carryoverErrorCode =
        function
        | UnknownPriorArm _ -> "unknown-prior-arm"
        | SamePriorArm -> "same-prior-arm"
        | MissingPriorExposure _ -> "missing-prior-exposure"
        | MissingCurrentTreatment _ -> "missing-current-treatment"
        | DuplicateResponse _ -> "duplicate-response"
        | UnknownResponseSubject _ -> "unknown-response-subject"
        | NonFiniteResponse _ -> "non-finite-response"
        | NonPositivePermutations -> "non-positive-permutations"
        | EmptyPriorArm _ -> "empty-prior-arm"

    let private modulus = 2147483647
    let private multiplier = 16807
    let private quotient = 127773
    let private remainder = 2836

    let private normalizeSeed (seed: int) =
        let reduced = seed % (modulus - 1)
        if reduced <= 0 then reduced + (modulus - 1) else reduced

    let private advance (state: int) =
        let high = state / quotient
        let low = state % quotient
        let test = multiplier * low - remainder * high
        if test <= 0 then test + modulus else test

    let private shuffleWith (state: int) (items: 'a list) : 'a list * int =
        let array = items |> List.toArray

        let rec loop current i =
            if i < 1 then
                array |> Array.toList, current
            else
                let next = advance current
                let j = next % (i + 1)
                let tmp = array.[i]
                array.[i] <- array.[j]
                array.[j] <- tmp
                loop next (i - 1)

        loop state (array.Length - 1)

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

    // L-4: OpenFirst is recorded per treatment but ordering enforcement is caller-side;
    // Polarity is copied without rebalancing, so the design polarity is labeled.
    // L-5: zero-exposure treatments stay visible in Exposure and are labeled, since
    // positivity callers (contrast/carryover) fail closed on empty arms.
    let private allocationAssumptions (treatments: Treatment list) (exposure: Map<string, int>) =
        let missing =
            treatments
            |> List.exists (fun treatment ->
                exposure |> Map.tryFind treatment.Name |> Option.defaultValue 0 = 0)

        let singlePolarity =
            treatments |> List.map (fun treatment -> treatment.Polarity) |> Set.ofList |> Set.count = 1

        let anyOpenFirst = treatments |> List.exists (fun treatment -> treatment.OpenFirst)

        Set.ofList
            [ "balanced-block-exposure"
              "blind-branch-isolation"
              "common-root-snapshot"
              "seeded-reproducible-randomization"
              "sibling-outcomes-excluded"
              if anyOpenFirst then
                  "open-first-ordering-caller-enforced"
              else
                  "no-open-first-requested"
              if singlePolarity then
                  "single-polarity-design"
              else
                  "polarity-mixed-caller-balanced"
              if missing then
                  "zero-exposure-treatment-present-positivity-violated" ]

    let private contrastAssumptions =
        Set.ofList
            [ "estimand-specified"
              "no-differential-attrition"
              "positivity"
              "same-prefix"
              "sutva-no-interference" ]

    let allocate (input: AllocationInput) =
        if String.IsNullOrWhiteSpace input.RootSnapshotHash then
            Error BlankRootSnapshotHash
        elif List.isEmpty input.Subjects then
            Error EmptySubjects
        elif List.isEmpty input.Treatments then
            Error EmptyTreatments
        elif List.isEmpty input.Candidates then
            Error EmptyCandidates
        else
            match firstDuplicate input.Subjects with
            | Some subject -> Error(DuplicateSubject subject)
            | None ->
                match firstDuplicate (input.Treatments |> List.map (fun treatment -> treatment.Name)) with
                | Some name -> Error(DuplicateTreatment name)
                | None ->
                    match firstDuplicate input.Candidates with
                    | Some candidate -> Error(DuplicateCandidate candidate)
                    | None ->
                        match input.Treatments |> List.tryFind (fun treatment -> treatment.Polarity <> 1 && treatment.Polarity <> -1) with
                        | Some treatment -> Error(InvalidPolarity treatment.Name)
                        | None ->
                            let subjects = input.Subjects |> List.sort
                            let treatmentCount = input.Treatments.Length
                            let blockCount = (subjects.Length + treatmentCount - 1) / treatmentCount
                            let indices = [ 0 .. treatmentCount - 1 ]

                            let rec assignBlocks state block acc =
                                if block >= blockCount then
                                    List.rev acc, state
                                else
                                    let order, next = shuffleWith state indices
                                    assignBlocks next (block + 1) ((block, order) :: acc)

                            let blocks, afterBlocks = assignBlocks (normalizeSeed input.Seed) 0 []

                            let blockOf =
                                blocks
                                |> List.collect (fun (block, order) -> order |> List.mapi (fun slot index -> block * treatmentCount + slot, index))
                                |> Map.ofList

                            let treatmentAt position =
                                blockOf |> Map.find position |> fun index -> input.Treatments.[index]

                            let rec envelop state position acc exposure =
                                if position >= subjects.Length then
                                    List.rev acc, state, exposure
                                else
                                    let subject = subjects.[position]
                                    let treatment = treatmentAt position
                                    let treatmentIndex = blockOf |> Map.find position
                                    let labels, afterLabels = shuffleWith state input.Candidates
                                    let order, afterOrder = shuffleWith afterLabels input.Candidates

                                    let envelope =
                                        { Subject = subject
                                          Treatment = treatment.Name
                                          TreatmentIndex = treatmentIndex
                                          Wording = treatment.Wording
                                          Polarity = treatment.Polarity
                                          LabelPermutation = labels
                                          OrderPermutation = order
                                          BlindToken =
                                            "blind"
                                            + CoreHash.sha256Hex (sprintf "%s|%s|%d|%d" subject treatment.Name treatmentIndex input.Seed) }

                                    let count =
                                        exposure |> Map.tryFind treatment.Name |> Option.defaultValue 0

                                    envelop
                                        afterOrder
                                        (position + 1)
                                        (envelope :: acc)
                                        (exposure |> Map.add treatment.Name (count + 1))

                            let envelopes, _, exposure = envelop afterBlocks 0 [] Map.empty

                            Ok
                                { RootSnapshotHash = input.RootSnapshotHash
                                  Seed = input.Seed
                                  Envelopes = envelopes
                                  Exposure = exposure
                                  BlockCount = blockCount
                                  Assumptions = allocationAssumptions input.Treatments exposure }

    let private finiteResponse value =
        not (Double.IsNaN value || Double.IsInfinity value)

    let private meanOf (values: float list) =
        (values |> List.sum) / float values.Length

    let private permutationP (seed: int) (permutations: int) (treatmentValues: float list) (controlValues: float list) =
        let capped = min permutations maxNullPermutations
        let pooled = treatmentValues @ controlValues
        let treatmentN = treatmentValues.Length
        let observed = abs (meanOf treatmentValues - meanOf controlValues)

        let rec nullLoop state remaining extreme =
            if remaining <= 0 then
                extreme
            else
                let shuffled, next = shuffleWith state pooled
                let redrawn = abs (meanOf (shuffled |> List.take treatmentN) - meanOf (shuffled |> List.skip treatmentN))

                nullLoop
                    next
                    (remaining - 1)
                    (if redrawn >= observed then extreme + 1 else extreme)

        let extreme = nullLoop (normalizeSeed seed) capped 0
        float (extreme + 1) / float (capped + 1), capped

    let contrast (input: ContrastInput) =
        if input.Permutations <= 0 then
            Error NonPositivePermutations
        elif input.Treatment = input.Control then
            Error SameArm
        else
            let arms =
                input.Assignment |> Map.toSeq |> Seq.map snd |> Set.ofSeq

            if not (Set.contains input.Treatment arms) then
                Error(UnknownTreatment input.Treatment)
            elif not (Set.contains input.Control arms) then
                Error(UnknownTreatment input.Control)
            else
                let rec collect rest seen acc =
                    match rest with
                    | [] -> Ok acc
                    | outcome :: tail ->
                        if Set.contains outcome.Subject seen then
                            Error(DuplicateOutcome outcome.Subject)
                        elif not (Map.containsKey outcome.Subject input.Assignment) then
                            Error(UnknownOutcomeSubject outcome.Subject)
                        elif not (finiteResponse outcome.Response) then
                            Error(NonFiniteResponse outcome.Subject)
                        else
                            collect tail (Set.add outcome.Subject seen) ((outcome.Subject, outcome.Response) :: acc)

                match collect input.Outcomes Set.empty [] with
                | Error error -> Error error
                | Ok pairs ->
                    let responses = Map.ofList pairs
                    let armOf name =
                        input.Assignment
                        |> Map.filter (fun _ treatment -> treatment = name)
                        |> Map.toList
                        |> List.choose (fun (subject, _) -> responses |> Map.tryFind subject |> Option.map (fun response -> response))
                    let excluded =
                        input.Assignment
                        |> Map.filter (fun subject _ -> not (Map.containsKey subject responses))
                        |> Map.toList
                        |> List.map fst
                        |> List.sort
                    let treatmentValues = armOf input.Treatment
                    let controlValues = armOf input.Control

                    if List.isEmpty treatmentValues then
                        Error(EmptyArm input.Treatment)
                    elif List.isEmpty controlValues then
                        Error(EmptyArm input.Control)
                    else
                        let treatmentMean = meanOf treatmentValues
                        let controlMean = meanOf controlValues
                        let pValue, nullCount = permutationP input.Seed input.Permutations treatmentValues controlValues

                        Ok
                            { Treatment = input.Treatment
                              Control = input.Control
                              TreatmentMean = treatmentMean
                              ControlMean = controlMean
                              Estimate = treatmentMean - controlMean
                              TreatmentN = treatmentValues.Length
                              ControlN = controlValues.Length
                              ExcludedSubjects = excluded
                              PermutationP = pValue
                              NullPermutations = nullCount
                              Estimand = "difference-in-means"
                              Assumptions = contrastAssumptions }

    let carryover (input: CarryoverInput) =
        if input.Permutations <= 0 then
            Error NonPositivePermutations
        elif input.Treatment = input.Control then
            Error SamePriorArm
        else
            let priorArms =
                input.PriorExposure |> Map.toSeq |> Seq.map snd |> Set.ofSeq

            if not (Set.contains input.Treatment priorArms) then
                Error(UnknownPriorArm input.Treatment)
            elif not (Set.contains input.Control priorArms) then
                Error(UnknownPriorArm input.Control)
            else
                let rec collect rest seen acc =
                    match rest with
                    | [] -> Ok acc
                    | outcome :: tail ->
                        if Set.contains outcome.Subject seen then
                            Error(DuplicateResponse outcome.Subject)
                        elif not (Map.containsKey outcome.Subject input.PriorExposure) then
                            Error(UnknownResponseSubject outcome.Subject)
                        elif not (finiteResponse outcome.Response) then
                            Error(NonFiniteResponse outcome.Subject)
                        else
                            collect tail (Set.add outcome.Subject seen) ((outcome.Subject, outcome.Response) :: acc)

                match collect input.Responses Set.empty [] with
                | Error error -> Error error
                | Ok pairs ->
                    let responses = Map.ofList pairs

                    let rec group subjects missingCurrent treatmentValues controlValues excluded =
                        match subjects with
                        | [] -> Ok(treatmentValues, controlValues, excluded)
                        | subject :: tail ->
                            match Map.tryFind subject input.CurrentTreatment with
                            | None -> Error(MissingCurrentTreatment subject)
                            | Some current ->
                                match Map.tryFind subject responses with
                                | None -> group tail missingCurrent treatmentValues controlValues (subject :: excluded)
                                | Some response when current <> input.FocalCurrent ->
                                    group tail missingCurrent treatmentValues controlValues (subject :: excluded)
                                | Some response ->
                                    match Map.tryFind subject input.PriorExposure with
                                    | None -> Error(MissingPriorExposure subject)
                                    | Some prior when prior = input.Treatment ->
                                        group tail missingCurrent (response :: treatmentValues) controlValues excluded
                                    | Some prior when prior = input.Control ->
                                        group tail missingCurrent treatmentValues (response :: controlValues) excluded
                                    | Some _ -> group tail missingCurrent treatmentValues controlValues (subject :: excluded)

                    match group (input.PriorExposure |> Map.toList |> List.map fst |> List.sort) [] [] [] [] with
                    | Error error -> Error error
                    | Ok (treatmentValues, controlValues, excluded) ->
                        if List.isEmpty treatmentValues then
                            Error(EmptyPriorArm input.Treatment)
                        elif List.isEmpty controlValues then
                            Error(EmptyPriorArm input.Control)
                        else
                            let treatmentMean = meanOf treatmentValues
                            let controlMean = meanOf controlValues
                            let pValue, nullCount = permutationP input.Seed input.Permutations treatmentValues controlValues

                            Ok
                                { FocalCurrent = input.FocalCurrent
                                  Treatment = input.Treatment
                                  Control = input.Control
                                  TreatmentMean = treatmentMean
                                  ControlMean = controlMean
                                  Estimate = treatmentMean - controlMean
                                  TreatmentN = treatmentValues.Length
                                  ControlN = controlValues.Length
                                  ExcludedSubjects = excluded |> List.sort
                                  PermutationP = pValue
                                  NullPermutations = nullCount
                                  Estimand = "carryover-difference-in-means"
                                  Assumptions =
                                    contrastAssumptions |> Set.add "current-arm-held-fixed" }
    // L-5: the two-party digest binds subject|response without a salt (binding without
    // hiding). Callers needing hiding must use the salted variant below.
    let commitResponse (subject: string) (responseText: string) =
        { Subject = subject
          Digest = CoreHash.sha256Hex (sprintf "%s|%s" subject responseText) }

    let verifyResponse (commit: ResponseCommit) (subject: string) (responseText: string) =
        commit.Subject = subject
        && commit.Digest = CoreHash.sha256Hex (sprintf "%s|%s" subject responseText)

    let commitResponseWithSalt (subject: string) (responseText: string) (salt: string) =
        { Subject = subject
          Digest = CoreHash.sha256Hex (sprintf "%s|%s|%s" subject responseText salt) }

    let verifyResponseWithSalt (commit: ResponseCommit) (subject: string) (responseText: string) (salt: string) =
        commit.Subject = subject
        && commit.Digest = CoreHash.sha256Hex (sprintf "%s|%s|%s" subject responseText salt)
