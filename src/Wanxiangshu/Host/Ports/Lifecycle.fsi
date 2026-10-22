namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks

/// HNI P-11: the product is a function from host ports to product hooks.
/// An adapter builds HostPorts from its host API and registers ProductHooks with it.
type HostCapabilities =
    { HostName: string
      Supports: Set<HostCapability> }

type AgentRoleSpec =
    { Role: AgentRole
      Description: string option
      Route: ModelRoute option
      ToolPolicy: Map<string, bool> option }

/// The catalog of agent roles the host offers. A host that cannot enumerate roles answers
/// `HostError.Unsupported` rather than an empty list: absence is typed, not an empty roster.
type IAgentCatalog =
    abstract Roles: unit -> HostResult<AgentRoleSpec list>

type HostPorts =
    { Sessions: ISessionPort
      Prompts: IPromptPort
      Transcript: ITranscriptPort
      Models: IModelCatalog
      Signals: ISignalSource
      Tools: IToolRegistrar
      Paths: IHostPaths
      Config: IHostConfigPort
      Diagnostics: IDiagnosticSink
      Agents: IAgentCatalog
      Capabilities: HostCapabilities }

/// What the product decides, handed to the host at activation.
///
/// `option` means "the product does not make this decision yet". Absence is declared, not
/// faked: the adapter must not substitute a pass-through implementation (an always-Allow
/// gate or an identity transform) for a member the product left `None`, because the core
/// could then no longer tell "no decision" from "decided nothing".
type ProductHooks =
    { ToolGate: IToolGate option
      Turn: ITurnTransform option
      Compaction: ICompactionGate
      Spawn: SpawnRouting option
      OnSignal: (HostSignal -> Task) option
      Dispose: unit -> Task }

type IHostPlugin =
    abstract Activate: ports: HostPorts -> HostResult<ProductHooks>
