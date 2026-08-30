namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Strength

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type ConfirmedFailureOutcome =
    | RecoveryAdvanced of RecoveryOpportunity
    | RecoveryExhausted
    | AlreadyRecorded
    | NoActiveRun

/// Injected capability: record one confirmed failure and preserve its exact outcome.
///
/// Journal + auto-recovery budget are closed at the wiring site so Session hosts
/// stay free of Application FallbackLedger details (dependency inversion).
type ConfirmedFailurePort = SessionId -> ProviderRunIdentity -> string -> Task<Result<ConfirmedFailureOutcome, string>>
