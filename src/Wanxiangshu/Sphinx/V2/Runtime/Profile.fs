namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Wanxiangshu.Sphinx.V2.Core

/// The default profile.
///
/// WHAT[sphinx-v2-002]: these are engineering constants, not semantic weights. They
/// enter the config hash and the tests, and they must never sit in the same record as
/// anything that decides whether one plan is better than another. Changing a numerical
/// regularization is a model revision, not something to hot-tune inside an inquiry.
///
/// WHAT[sphinx-v2-007]: the v2 profile carries every probe by name, but probes are
/// proposed on demand. Nothing here is a per-round checklist.
[<RequireQualifiedAccess>]
type ExecutionMode =
    | Delegated
    | Independent

type DefaultProfile =
    {
        ProfileRef: string
        /// Finite meta-level investigation. An approximation, not an optimality claim.
        MetaDepth: int
        /// Engineering ceiling on concurrently held plan cards.
        MaxActivePlanCards: int
        /// Numerical iteration ceiling for a fit. Not an epistemic convergence threshold.
        FitMaxIterations: int
        /// Numerical gradient tolerance. Needs a scaled test, not a quality reading.
        FitGradientTolerance: float
        /// Declared starting regularization for the ordinal penalty.
        ThetaL2: float
        /// Declared starting regularization for the position term, when identifiable.
        OrderL2: float
        /// Working prior for the tie mechanism's kappa.
        TieKappaPriorMean: float
        TieKappaPriorVariance: float
        /// One original call plus one retry.
        MaxWorkAttempts: int
        /// Ceiling on pure plugin steps in one advance.
        MaxPureStepsPerAdvance: int
        ExecutionMode: ExecutionMode
        /// The questions this profile activates; others can be activated by a plan.
        QuestionTemplates: string list
        /// Every registered probe is available; none is mandatory per round.
        ProbeIds: string list
    }

type ProfileError = { Code: string; Message: string }

module Profile =

    /// Sphinx's own declared default. Real models and real quotas have no implicit
    /// default: they come from an authorized Host/provider configuration.
    let defaultProfile: DefaultProfile =
        { ProfileRef = "sphinx.default@2"
          MetaDepth = 1
          MaxActivePlanCards = 8
          FitMaxIterations = 100
          FitGradientTolerance = 1e-7
          ThetaL2 = 1.0
          OrderL2 = 1.0
          TieKappaPriorMean = 0.0
          TieKappaPriorVariance = 4.0
          MaxWorkAttempts = 2
          MaxPureStepsPerAdvance = 128
          ExecutionMode = ExecutionMode.Delegated
          QuestionTemplates =
            [ "sphinx.question.plan-contribution-pair@2"
              "sphinx.question.ranking-reversal@2"
              "sphinx.question.combination-sequence@2"
              "sphinx.question.investigation-value@2"
              "sphinx.question.answer-now-versus-investigate@2" ]
          ProbeIds = [] }

    /// A profile is admissible only if its engineering constants stay nonnegative and
    /// its meta depth stays finite. A negative iteration ceiling would silently disable
    /// every fit, and an unbounded meta depth would recurse forever.
    let validate (profile: DefaultProfile) : Result<DefaultProfile, ProfileError> =
        let invalidIteration () = profile.FitMaxIterations < 1
        let invalidMetaDepth () = profile.MetaDepth < 1
        let invalidAttempts () = profile.MaxWorkAttempts < 1
        let invalidSteps () = profile.MaxPureStepsPerAdvance < 1
        let invalidCards () = profile.MaxActivePlanCards < 1

        let invalidRegularization () =
            profile.ThetaL2 < 0.0
            || profile.OrderL2 < 0.0
            || profile.TieKappaPriorVariance < 0.0
            || Double.IsNaN profile.ThetaL2
            || Double.IsNaN profile.OrderL2

        let invalidTolerance () =
            Double.IsNaN profile.FitGradientTolerance
            || Double.IsInfinity profile.FitGradientTolerance
            || profile.FitGradientTolerance <= 0.0

        match
            invalidIteration ()
            || invalidMetaDepth ()
            || invalidAttempts ()
            || invalidSteps ()
            || invalidCards ()
            || invalidRegularization ()
            || invalidTolerance ()
        with
        | true ->
            Error
                { Code = "invalid-profile"
                  Message = "profile engineering constants must be positive and finite" }
        | false -> Ok profile

    /// The config hash input. Deliberately contains no plan ranking, no probe utility
    /// and no answer quality weight — those are not engineering constants.
    let configHashInput (profile: DefaultProfile) : string =
        String.concat
            "|"
            [ profile.ProfileRef
              string profile.MetaDepth
              string profile.MaxActivePlanCards
              string profile.FitMaxIterations
              string profile.FitGradientTolerance
              string profile.ThetaL2
              string profile.OrderL2
              string profile.TieKappaPriorMean
              string profile.TieKappaPriorVariance
              string profile.MaxWorkAttempts
              string profile.MaxPureStepsPerAdvance
              string profile.ExecutionMode ]

    /// Which execution mode this profile authorizes. Delegated does not claim blinding,
    /// independence, or a shared-prefix experiment; independent is an explicit choice.
    let claimsIndependence (profile: DefaultProfile) : bool =
        match profile.ExecutionMode with
        | ExecutionMode.Independent -> true
        | ExecutionMode.Delegated -> false
