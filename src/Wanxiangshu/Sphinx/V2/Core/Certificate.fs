namespace Wanxiangshu.Sphinx.V2.Core

/// Certificate semantics are address, not a node. The same candidate can carry an
/// ordinal constraint under one scope, a model posterior under another, and nothing
/// at all in a third; those are different statements, not three versions of one truth.
///
/// WHAT[sphinx-v2-006]: the guarantee type states what a producer may claim. A plugin
/// holding pairwise/ranking data cannot write `DeterministicBound`, and a Bayes
/// posterior cannot be presented as frequentist coverage. The reducer checks the
/// shape; the operator states the conditions under which its own guarantee holds.

[<RequireQualifiedAccess>]
type CertificateGuarantee =
    /// An empirical summary with no coverage claim. Honest default for sample output.
    | EmpiricalSummary of assumptions: string list
    /// A recorded ordinal observation under a stated protocol.
    | OrdinalObservation of protocolRef: string
    /// A parameter fitted under a declared model, with its approximation named.
    | ModelEstimate of modelRef: string * approximation: string
    /// A posterior credible statement: model-relative, not frequentist.
    | PosteriorCredible of modelRef: string * mass: float * approximation: string
    /// A frequentist coverage claim: a different object with a different meaning.
    | FrequentistCoverage of coverageRef: string * delta: float * scope: string
    /// A proven bound inside a stated theorem.
    | DeterministicBound of theoremRef: string * assumptions: string list
    /// A numeric result exact within its declared model; no claim about reality.
    | ExactWithinModel of modelRef: string * numericError: string
    /// A residual with no guarantee at all.
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
      Status: CertificateStatus
    }

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

    /// A guarantee is only legal if its own numerics are sane. A credible mass of 1.4
    /// or a negative delta is not a small modeling detail; it is a certificate that
    /// claims more than any probability statement can.
    let validateGuarantee (guarantee: CertificateGuarantee) : Result<unit, CertificateError> =
        let finite value =
            not (System.Double.IsNaN value) && not (System.Double.IsInfinity value)

        match guarantee with
        | CertificateGuarantee.PosteriorCredible(_, mass, _) when not (finite mass) || mass <= 0.0 || mass >= 1.0 ->
            Error
                { Code = "invalid-guarantee"
                  Message = "posterior credible mass must be a finite number strictly between 0 and 1" }
        | CertificateGuarantee.FrequentistCoverage(_, delta, _) when not (finite delta) || delta <= 0.0 || delta >= 1.0 ->
            Error
                { Code = "invalid-guarantee"
                  Message = "frequentist coverage delta must be a finite number strictly between 0 and 1" }
        | _ -> Ok()

    let validateSlot (slot: CertificateSlot) : Result<unit, CertificateError> =
        if System.String.IsNullOrWhiteSpace slot.Slot then
            Error { Code = "invalid-slot"; Message = "certificate slot name must not be blank" }
        elif System.String.IsNullOrWhiteSpace slot.Producer then
            Error { Code = "invalid-slot"; Message = "certificate slot producer must not be blank" }
        else
            validateGuarantee slot.Guarantee
