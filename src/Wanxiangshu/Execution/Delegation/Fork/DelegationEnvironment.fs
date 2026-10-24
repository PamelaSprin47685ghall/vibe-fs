// HNI P-14 / P-15 (domain seam): what every delegation-family command
// needs in order to run one call. This record is product state: it carries
// the capabilities the product consumes, not a host's opinion about them.
//
// The one host contribution is `Caller` (`Host/Domain/DelegationCaller.fs(i)`),
// a tier-B neutral record; every other member is either a product fact
// (`Journal`, `LogicalOwnerFor`) or a product decision the adapter cannot
// make (`ForkRuntime`, `OpenManagerJob`, `ContinueManagerJob`).
//
// A member the calling host does not provide is `None`, and the product
// refuses explicitly rather than substituting a value.
namespace Wanxiangshu.Execution.Delegation.Fork

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Domain
open Wanxiangshu.Host.Ports
open Wanxiangshu.Persistence.Journal

/// HNI P-14 / P-15: the environment one delegation-family call runs in.
/// A host adapter fills it once per tool call; the product reads it and
/// decides nothing about how it was filled.
type DelegationEnvironment =
    {
        /// The host's contribution about the caller: which session, which
        /// provider run, which tool call.
        Caller: DelegationCaller

        /// The durable journal the product reads and writes, when one exists.
        Journal: AgentJournal option

        /// The caller's fork runtime. `Error` when the caller cannot own children.
        ForkRuntime: unit -> Result<HostForkRuntime, HostError>

        /// The logical owner of a physical session (the session itself when it has none).
        LogicalOwnerFor: SessionId -> SessionId

        /// Bind the DevOps work of one owner session, when the caller acts for it.
        EnsureWorkDevOpsBound: SessionId -> Task<unit>

        /// The working directory the caller's own session runs in, when known.
        WorkspaceDirectory: WorkspacePath option

        /// The manager capabilities of a session, as the product sees them.
        ManagerCapabilityFactsFor: (SessionId -> Task<ManagerCapabilityFacts>) option

        /// The caller's role from its authority root; `None` when it has none.
        CallerRole: (unit -> Task<Role option>) option

        /// A fresh handle id for a new child or manager job.
        NewHandleId: (unit -> string) option

        /// Open a Manager job for the calling Orchestrator.
        OpenManagerJob: ((ManagerJobId * string * string * string * int option) -> HostResult<string>) option

        /// Carry a new charge to an existing Manager job for this exact tool call.
        ContinueManagerJob:
            ((ManagerJobId * string * ProviderRunIdentity * ToolCallId * int option) -> HostResult<string>) option

    }
