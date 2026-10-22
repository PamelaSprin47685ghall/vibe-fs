namespace Wanxiangshu.Execution.Fission

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

/// Same-participant physical replacement, host-neutral (HNI fission lane).
/// Fission never calls the host session fork endpoint: it creates fresh
/// sibling sessions, starts them from the owner's current session context
/// (when there is one) + exact lane input, then physically interrupts the old
/// present without terminating the logical owner. Every host capability
/// arrives through FissionHostPort.
module FissionLaunch =

    type FissionLaunchRequest =
        {
            /// The calling physical session as the host names it (blank = unknown).
            CallerSessionId: string
            ToolCallId: ToolCallId option
            ProviderRun: ProviderRunIdentity option
            /// One prompt per lane, already decoded by the host adapter.
            Prompts: string list
        }

    type FissionLaunched =
        {
            GroupId: string
            LaneCount: int
            /// Started lanes ordered by lane index.
            Lanes: FissionStartedLane list
        }

    /// Why a launch was refused, in the order the checks run. Each host adapter
    /// encodes these into its own wire vocabulary.
    [<RequireQualifiedAccess>]
    type FissionLaunchRejection =
        | MissingCaller
        | ParentLookupFailed of string
        | NotSubsession
        | LanePromptsInvalid
        | PromptsRejected of FissionRejectReason
        | ContextUnavailable
        | CallerIsLane
        | OwnerAlreadyFissioned
        | AuthorityUnavailable
        | RoleNotPermitted
        | ObservationUnavailable
        | AdmissionRejected of FissionRejectReason

    val launch:
        port: FissionHostPort ->
        journal: Wanxiangshu.Persistence.Journal.AgentJournal option ->
        request: FissionLaunchRequest ->
            Task<Result<FissionLaunched, FissionLaunchRejection>>
