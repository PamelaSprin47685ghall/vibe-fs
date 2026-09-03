namespace Wanxiangshu.Sphinx.Plugins.Questionnaire

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

    val maxNullPermutations: int
    val allocationErrorCode: AllocationError -> string
    val contrastErrorCode: ContrastError -> string
    val carryoverErrorCode: CarryoverError -> string
    val allocate: input: AllocationInput -> Result<Allocation, AllocationError>
    val contrast: input: ContrastInput -> Result<Contrast, ContrastError>
    val carryover: input: CarryoverInput -> Result<Carryover, CarryoverError>
    /// Binding-without-hiding digest over subject|response (L-5); use the salted variant for hiding.
    val commitResponse: subject: string -> responseText: string -> ResponseCommit
    val verifyResponse: commit: ResponseCommit -> subject: string -> responseText: string -> bool
    /// Salted hiding variant over subject|response|salt.
    val commitResponseWithSalt: subject: string -> responseText: string -> salt: string -> ResponseCommit
    val verifyResponseWithSalt: commit: ResponseCommit -> subject: string -> responseText: string -> salt: string -> bool
