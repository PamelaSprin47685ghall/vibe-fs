namespace Wanxiangshu.Sphinx.V2.Plugins

type Assignment =
    { Seed: string
      /// Name and version of the shuffle actually used.
      ShuffleAlgorithm: string
      /// Opaque label -> real identity. Host-private; never sent to a worker.
      LabelMap: Map<string, string>
      /// The presented order, as shown.
      Order: string list }

type RoundDesign =
    { ScopeId: string
      SnapshotId: string
      Purpose: string
      QuestionId: string
      TemplateRef: string
      TemplateHash: string
      PresentedSet: string list
      OmittedSet: string list
      Assignment: Assignment
      /// The independence unit: responses in the same cluster are not independent votes.
      ClusterId: string
      ExpectedResponses: int
      MaxAttempts: int
      /// How a missing or excluded response is recorded.
      MissingnessPolicy: string }

type DesignError = { Code: string; Message: string }

module Design =
    val buildAssignment: string -> string list -> Assignment
    val tryValidate: RoundDesign -> Result<RoundDesign, DesignError>
    val workerView: RoundDesign -> (string * string) list
