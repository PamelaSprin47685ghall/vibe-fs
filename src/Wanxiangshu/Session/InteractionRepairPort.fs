namespace Wanxiangshu.Session

open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
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
        -> ProviderRunIdentity
        -> string
        -> Task<Result<PromptKey, string>>
