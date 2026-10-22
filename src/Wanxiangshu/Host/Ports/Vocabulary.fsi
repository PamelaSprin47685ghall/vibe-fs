namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks

/// Host-neutral vocabulary shared by every port family (HNI P-00, proposals/host-neutral-interface).
/// No host wire terms here: adapters translate host payloads into these shapes.
/// A host that cannot perform a family's verb does not claim the matching capability, and the
/// adapter answers `HostError.Unsupported` for it: the degradation is declared, never faked as success.
[<RequireQualifiedAccess>]
type HostCapability =
    | ChildSessions
    | SessionDiscovery
    | Interrupt
    | ToolDefinitionEdit
    | SubagentRouting
    | ProviderParams
    | StreamDeltas
    | ToolRegistration
    | ModelCatalog
    | SignalSubscription
    | AgentCatalog

/// Error channel of every host round trip. Unknown means the outcome is not observable:
/// callers must not resend or assume loss.
[<RequireQualifiedAccess>]
type HostError =
    | Unsupported of capability: HostCapability
    | NotFound of what: string
    | Refused of reason: string
    | Transport of reason: string
    | Unknown of reason: string

type HostResult<'T> = Task<Result<'T, HostError>>

type WorkspacePath = WorkspacePath of string

/// Product role name of a managed agent (participant / seat), not a host agent object.
type AgentRole = AgentRole of string

/// Canonical JSON text produced and parsed by the product (Host/CanonicalJson), opaque to ports.
type CanonicalJsonText = CanonicalJsonText of string

/// JSON-schema document text owned by the product.
type JsonSchema = JsonSchema of string

/// Raw failure facts reported by a host. Classifying them into an execution failure,
/// retry or budget decision is product logic (Execution/Failure), never adapter logic.
[<RequireQualifiedAccess>]
type HostFailureKind =
    | RateLimited
    | ContextOverflow
    | Authentication
    | Network
    | Provider
    | Aborted
    | Unknown

type HostFailure =
    { Kind: HostFailureKind
      Message: string
      StatusCode: int option
      HostRetryable: bool option }

/// How one provider attempt (turn) ended, as observed on the host.
[<RequireQualifiedAccess>]
type TurnTerminal =
    | Completed of finish: string option
    | Cancelled
    | Interrupted
    | Failed of failure: HostFailure
