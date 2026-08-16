namespace Wanxiangshu.Interaction.Repair

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
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal

/// Durable InteractionRepair send (ENFORCER-066).
///
/// Defined in Session so EnforcerHost can depend on the port without referencing
/// Infrastructure/OpenCode/Host (HostSessionNudge compiles later). Wiring injects
/// HostSessionNudge.trySendInteractionRepair after that module is available.
///
/// Signature matches HostSessionNudge.trySendInteractionRepair with sessionPort
/// closed at the injection site.
type InteractionRepairNudge =
    SessionId
        -> string
        -> string option
        -> AgentJournal option
        -> BloggerRequestId
        -> ProviderRunIdentity
        -> string
        -> Task<Result<PromptKey, string>>
