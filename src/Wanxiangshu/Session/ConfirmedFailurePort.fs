namespace Wanxiangshu.Session

open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Wanxiangshu.Kernel.Identity

/// rabbit §13.1 / S9.1: admission after a confirmed provider failure.
///
/// Callers (EnforcerHost) only learn whether automatic recovery may continue —
/// not cursor shape, budget arithmetic, or which module writes the facts.
[<RequireQualifiedAccess>]
type RecoveryAdmission =
    | ContinueRecovery
    | RecoveryExhausted

/// Injected capability: record one confirmed failure and return admission.
///
/// Journal + auto-recovery budget are closed at the wiring site so Session hosts
/// stay free of FallbackController.recordConfirmedFailure (dependency inversion;
/// controller itself stays in Session until §13.2).
type ConfirmedFailurePort =
    SessionId -> ProviderRunIdentity -> string -> Result<RecoveryAdmission, string>

module ConfirmedFailurePort =

    /// Map the one writer's AdvanceOutcome to the host-facing admission.
    /// Only Exhausted forbids the next automatic recovery attempt; Advanced /
    /// AlreadyRecorded / NoActiveRun all allow the caller's local repair path
    /// (matches EnforcerHost AABB bridge semantics).
    let private toAdmission (outcome: FallbackController.AdvanceOutcome) : RecoveryAdmission =
        match outcome with
        | FallbackController.AdvanceOutcome.Exhausted _ -> RecoveryAdmission.RecoveryExhausted
        | FallbackController.AdvanceOutcome.Advanced _
        | FallbackController.AdvanceOutcome.AlreadyRecorded _
        | FallbackController.AdvanceOutcome.NoActiveRun -> RecoveryAdmission.ContinueRecovery

    /// Adapter: FallbackController remains the FALLBACK-003 writer; this closes
    /// journal + budget into ConfirmedFailurePort for injection.
    let bind (journal: AgentJournal) (budget: int) : ConfirmedFailurePort =
        fun sessionId providerRun reason ->
            FallbackController.recordConfirmedFailure journal budget sessionId providerRun reason
            |> Result.map toAdmission
