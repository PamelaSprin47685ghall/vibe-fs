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

type IAgentCatalog =
    abstract Roles: unit -> AgentRoleSpec list

type HostPorts =
    { Sessions: ISessionPort
      Prompts: IPromptPort
      Transcript: ITranscriptPort
      Models: IModelCatalog
      Signals: ISignalSource
      Tools: IToolRegistrar
      Paths: IHostPaths
      Diagnostics: IDiagnosticSink
      Agents: IAgentCatalog
      Capabilities: HostCapabilities }

type ProductHooks =
    { ToolGate: IToolGate
      Turn: ITurnTransform
      Compaction: ICompactionGate
      Spawn: SpawnRouting option
      OnSignal: HostSignal -> Task
      Dispose: unit -> Task }

type IHostPlugin =
    abstract Activate: ports: HostPorts -> HostResult<ProductHooks>
