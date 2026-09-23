namespace Wanxiangshu.Sphinx.V2.Runtime

open System
open Wanxiangshu.Sphinx.V2.Core

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
    val defaultProfile: DefaultProfile
    val validate: DefaultProfile -> Result<DefaultProfile, ProfileError>

    /// The config hash input. Contains no plan ranking, probe utility or quality weight.
    val configHashInput: DefaultProfile -> string

    /// Delegated does not claim blinding, independence or a shared-prefix experiment.
    val claimsIndependence: DefaultProfile -> bool
