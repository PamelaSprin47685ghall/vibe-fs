namespace Wanxiangshu.Session

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
/// stay free of Application FallbackLedger details (dependency inversion).
type ConfirmedFailurePort = SessionId -> ProviderRunIdentity -> string -> Result<RecoveryAdmission, string>
