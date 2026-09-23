namespace Wanxiangshu.Sphinx.V2.Core

[<RequireQualifiedAccess>]
type CertificateGuarantee =
    | EmpiricalSummary of assumptions: string list
    | OrdinalObservation of protocolRef: string
    | ModelEstimate of modelRef: string * approximation: string
    | PosteriorCredible of modelRef: string * mass: float * approximation: string
    | FrequentistCoverage of coverageRef: string * delta: float * scope: string
    | DeterministicBound of theoremRef: string * assumptions: string list
    | ExactWithinModel of modelRef: string * numericError: string
    | ResidualOnly of reason: string

[<RequireQualifiedAccess>]
type CertificateStatus =
    | Current
    | Stale of reason: string
    | Invalidated of sourceRevision: Revision
    | Conflicted

type CertificateSlot =
    { Slot: string
      Producer: string
      Schema: SchemaRef
      CanonicalPayload: string
      /// Revision of the slot's own content; a patch names the base it expects.
      Revision: Revision
      Guarantee: CertificateGuarantee
      Status: CertificateStatus }

type CertificateSlotPatch =
    { CertificateId: CertificateId
      TargetRef: string
      ValueSpaceId: string
      ScopeId: string
      SemanticsModelRef: string
      Slot: CertificateSlot
      /// Base revision the patch expects; a mismatch is a conflict, not a merge.
      ExpectedSlotRevision: Revision }

type CertificateError = { Code: string; Message: string }

module Certificate =
    val validateGuarantee: CertificateGuarantee -> Result<unit, CertificateError>
    val validateSlot: CertificateSlot -> Result<unit, CertificateError>
