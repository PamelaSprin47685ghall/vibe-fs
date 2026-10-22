// GENERATED FILE — do not edit by hand.
// Source of truth: src/Wanxiangshu/Host/Ports/*.fsi (HNI P-00, P-01, P-02, P-03, P-04, P-05, P-06, P-07, P-08, P-09, P-10, P-11, P-12, P-19), plus
// src/Wanxiangshu/Foundation/{Identity,Outcome}.fsi for the opaque ids and
// Outcome.SendOutcome.
// The port families are discovered from the sources, so a family another lane lands shows up
// here and the mirror check fails until the mirror is regenerated.
// Regenerate:  node proposals/host-neutral-interface/adapters/omp/gen-ports.mjs --write
// Verify:      node proposals/host-neutral-interface/adapters/omp/mirror-check.mjs
//
// Mapping (mechanical, see gen-ports.mjs):
//   F# record -> TS interface; `T option` field -> optional property
//   F# DU (>=2 cases) -> tagged union { tag: "Case", field: T }
//   F# single-case string DU, and Foundation `private X of string` -> opaque string
//   F# abstract member -> TS method; abstract property -> readonly property
//   F# alias with `->` -> TS function type
//   Task -> Promise; HostResult<T> -> HostResult<T>; Result<T,E> -> Result<T,E>;
//   list -> []; Map -> Record; Set -> ReadonlySet; unit -> void; int/float -> number
//
// Host bindings (deliberate, not mechanical):
//   System.Threading.CancellationToken -> AbortSignal
//   System.IDisposable -> IDisposableHandle

export type Result<T, E> = { ok: true; value: T } | { ok: false; error: E };
export interface IDisposableHandle { dispose(): void }
export type SessionId = string & { readonly __brand: "SessionId" };
/** Construct a SessionId from its underlying string. */
export const sessionId = (value: string): SessionId => value as SessionId;
export type ToolCallId = string & { readonly __brand: "ToolCallId" };
/** Construct a ToolCallId from its underlying string. */
export const toolCallId = (value: string): ToolCallId => value as ToolCallId;
export type PromptKey = string & { readonly __brand: "PromptKey" };
/** Construct a PromptKey from its underlying string. */
export const promptKey = (value: string): PromptKey => value as PromptKey;
export type TranscriptMessageAddress = string & { readonly __brand: "TranscriptMessageAddress" };
/** Construct a TranscriptMessageAddress from its underlying string. */
export const transcriptMessageAddress = (value: string): TranscriptMessageAddress => value as TranscriptMessageAddress;
export type TransportReceipt = string & { readonly __brand: "TransportReceipt" };
/** Construct a TransportReceipt from its underlying string. */
export const transportReceipt = (value: string): TransportReceipt => value as TransportReceipt;
export type PhysicalUserMessageId = string & { readonly __brand: "PhysicalUserMessageId" };
/** Construct a PhysicalUserMessageId from its underlying string. */
export const physicalUserMessageId = (value: string): PhysicalUserMessageId => value as PhysicalUserMessageId;

// ---- P-00 Vocabulary
export type HostCapability =
  | "ChildSessions"
  | "SessionDiscovery"
  | "Interrupt"
  | "ToolDefinitionEdit"
  | "SubagentRouting"
  | "ProviderParams"
  | "StreamDeltas"
  | "ToolRegistration"
  | "ModelCatalog"
  | "SignalSubscription"
  | "AgentCatalog";

export type HostError =
  | { tag: "Unsupported"; capability: HostCapability }
  | { tag: "NotFound"; what: string }
  | { tag: "Refused"; reason: string }
  | { tag: "Transport"; reason: string }
  | { tag: "Unknown"; reason: string };

export type HostResult<T> = Promise<Result<T, HostError>>;

export type WorkspacePath = string & { readonly __brand: "WorkspacePath" };
/** Construct a WorkspacePath from its underlying string. */
export const workspacePath = (value: string): WorkspacePath => value as WorkspacePath;

export type AgentRole = string & { readonly __brand: "AgentRole" };
/** Construct a AgentRole from its underlying string. */
export const agentRole = (value: string): AgentRole => value as AgentRole;

export type CanonicalJsonText = string & { readonly __brand: "CanonicalJsonText" };
/** Construct a CanonicalJsonText from its underlying string. */
export const canonicalJsonText = (value: string): CanonicalJsonText => value as CanonicalJsonText;

export type JsonSchema = string & { readonly __brand: "JsonSchema" };
/** Construct a JsonSchema from its underlying string. */
export const jsonSchema = (value: string): JsonSchema => value as JsonSchema;

export type HostFailureKind =
  | "RateLimited"
  | "ContextOverflow"
  | "Authentication"
  | "Network"
  | "Provider"
  | "Aborted"
  | "Unknown";

export interface HostFailure {
  Kind: HostFailureKind
  Message: string
  StatusCode?: number
  HostRetryable?: boolean
}

export type TurnTerminal =
  | { tag: "Completed"; finish?: string }
  | { tag: "Cancelled" }
  | { tag: "Interrupted" }
  | { tag: "Failed"; failure: HostFailure };

// ---- P-01 Sessions
export interface SessionSpec {
  Title?: string
  Role?: AgentRole
  Workspace?: WorkspacePath
}

export interface SessionInfo {
  Id: SessionId
  Parent?: SessionId
  Role?: AgentRole
  Title?: string
}

export interface ISessionPort {
  Create(parent: SessionId | undefined, spec: SessionSpec): HostResult<SessionId>
  Parent(session: SessionId): HostResult<SessionId | undefined>
  Children(parent: SessionId): HostResult<SessionInfo[]>
  Abort(session: SessionId): HostResult<void>
  Interrupt(session: SessionId): HostResult<void>
  Close(session: SessionId): HostResult<void>
  FindByTitle(title: string): HostResult<SessionId[]>
}

// ---- P-02 Prompts
export interface PromptRequest {
  Text: string
  Route?: ModelRoute
  Role?: AgentRole
  Workspace?: WorkspacePath
  Correlation?: PromptKey
  ToolPolicy?: Record<string, boolean>
}

export type DeliveryVerdict =
  | { tag: "OwnedSettled" }
  | { tag: "Refused"; reason: string }
  | { tag: "OutcomeUnknown"; reason: string };

export type LateDeliveryListener = (arg: DeliveryVerdict) => Promise<void>;

export interface IPromptPort {
  Send(session: SessionId, request: PromptRequest, late?: LateDeliveryListener): Promise<SendOutcome>
}

// ---- P-03 Transcript
export type TranscriptRole =
  | "User"
  | "Assistant"
  | "System";

export type ToolCallState =
  | "Pending"
  | "Running"
  | "Completed"
  | "Failed";

export interface ToolCallRecord {
  CallId: ToolCallId
  Tool: string
  Input: CanonicalJsonText
  State: ToolCallState
  Output?: string
}

export type TranscriptPart =
  | { tag: "Text"; text: string }
  | { tag: "Reasoning"; text: string }
  | { tag: "ToolUse"; call: ToolCallRecord }
  | { tag: "File"; mime: string; uri: string }
  | { tag: "Other"; kind: string };

export interface TranscriptMessage {
  Id: TranscriptMessageAddress
  Role: TranscriptRole
  Agent?: AgentRole
  Route?: ModelRoute
  Parent?: TranscriptMessageAddress
  CreatedAt?: number
  Completed: boolean
  Terminal?: TurnTerminal
  IsCompaction: boolean
  Correlation?: PromptKey
  Parts: TranscriptPart[]
}

export interface ITranscriptPort {
  Messages(session: SessionId): HostResult<TranscriptMessage[]>
}

// ---- P-04 Tools
export interface ToolInvocation {
  Session: SessionId
  CallId: ToolCallId
  Workspace: WorkspacePath
  Args: CanonicalJsonText
  Cancellation: AbortSignal
  Progress?: (arg: string) => void
}

export interface ToolOutcome {
  Text: string
  Truncated: boolean
  IsError: boolean
  Details?: CanonicalJsonText
}

export interface ToolSpec {
  Name: string
  Description: string
  Schema: JsonSchema
  Execute: (arg: ToolInvocation) => Promise<ToolOutcome>
}

export interface IToolRegistrar {
  Register(tools: ToolSpec[]): Result<void, HostError>
}

// ---- P-05 ToolGates
export interface ToolCallView {
  Session: SessionId
  CallId: ToolCallId
  Tool: string
  Args: CanonicalJsonText
  Role?: AgentRole
}

export type ToolCallVerdict =
  | { tag: "Allow" }
  | { tag: "Rewrite"; args: CanonicalJsonText }
  | { tag: "Refuse"; reason: string };

export type ToolResultEdit =
  | { tag: "Keep" }
  | { tag: "Replace"; result: ToolOutcome };

export interface ToolDefinitionView {
  Tool: string
  Description: string
  Schema: JsonSchema
}

export type ToolDefinitionEdit =
  | { tag: "Keep" }
  | { tag: "Describe"; description: string; schema: JsonSchema };

export interface IToolGate {
  BeforeTool(call: ToolCallView): Promise<ToolCallVerdict>
  AfterTool(call: ToolCallView, result: ToolOutcome): Promise<ToolResultEdit>
  DefineTool(definition: ToolDefinitionView): ToolDefinitionEdit
}

// ---- P-06 TurnTransforms
export interface TurnContext {
  Session: SessionId
  Role?: AgentRole
  Route?: ModelRoute
  Workspace?: WorkspacePath
}

export interface TurnRequest {
  Context: TurnContext
  Message?: TranscriptMessageAddress
  Text: string
  Correlation?: PromptKey
}

export type TurnAdmission =
  | { tag: "Admit"; route?: ModelRoute }
  | { tag: "Refuse"; reason: string };

export interface ContextMessage {
  Address?: TranscriptMessageAddress
  Role: TranscriptRole
  Parts: TranscriptPart[]
}

export type ContextEdit =
  | { tag: "Keep" }
  | { tag: "Replace"; messages: ContextMessage[] };

export interface ProviderParams {
  Temperature?: number
  TopP?: number
  MaxOutputTokens?: number
  Reasoning?: string
  Extra?: CanonicalJsonText
}

export type ParamsVerdict =
  | { tag: "Accept" }
  | { tag: "Adjust"; parameters: ProviderParams }
  | { tag: "Refuse"; reason: string };

export interface ITurnTransform {
  AdmitTurn(request: TurnRequest): Promise<TurnAdmission>
  SystemPrompt(context: TurnContext, system: string[]): Promise<string[]>
  ContextMessages(context: TurnContext, messages: ContextMessage[]): Promise<ContextEdit>
  ProviderParams(context: TurnContext, parameters: ProviderParams): Promise<ParamsVerdict>
}

// ---- P-07 Signals
export interface AttemptRef {
  Session: SessionId
  Message?: TranscriptMessageAddress
}

export interface ToolCallRef {
  Session: SessionId
  CallId: ToolCallId
  Tool: string
}

export interface RetryNotice {
  Session: SessionId
  Attempt: number
  Failure: HostFailure
}

export type StreamChannel =
  | "Text"
  | "Reasoning"
  | "ToolInput";

export interface StreamChunk {
  Session: SessionId
  Message?: TranscriptMessageAddress
  Channel: StreamChannel
  Delta: string
}

export type HostSignal =
  | { tag: "SessionCreated"; info: SessionInfo }
  | { tag: "SessionDeleted"; session: SessionId; parent?: SessionId }
  | { tag: "SessionIdle"; session: SessionId }
  | { tag: "AttemptStarted"; attempt: AttemptRef }
  | { tag: "AttemptFinished"; attempt: AttemptRef; terminal: TurnTerminal }
  | { tag: "ProviderRetry"; notice: RetryNotice }
  | { tag: "ProviderFailed"; session: SessionId; failure: HostFailure }
  | { tag: "AttemptAborted"; session: SessionId; failure: HostFailure }
  | { tag: "ToolStarted"; call: ToolCallRef }
  | { tag: "ToolFinished"; call: ToolCallRef; result: ToolOutcome }
  | { tag: "Streamed"; chunk: StreamChunk }
  | { tag: "MessageCommitted"; session: SessionId; message: TranscriptMessageAddress };

export interface ISignalSource {
  Subscribe(listener: (arg: HostSignal) => Promise<void>): IDisposableHandle
}

// ---- P-08 ModelRouting
export interface ModelRoute {
  Provider: string
  Model: string
  Variant?: string
}

export interface SpawnRequest {
  Parent: SessionId
  Role: AgentRole
  Candidates: ModelRoute[]
  SpawnKey?: string
}

export type SpawnRoute =
  | { tag: "Keep" }
  | { tag: "Route"; routes: ModelRoute[] }
  | { tag: "Refuse"; reason: string };

export interface IModelCatalog {
  Available(): HostResult<ModelRoute[]>
  Apply(session: SessionId, route: ModelRoute): HostResult<void>
}

export type SpawnRouting = (arg: SpawnRequest) => Promise<SpawnRoute>;

// ---- P-09 Compaction
export type CompactionContribution =
  | { tag: "Nothing" }
  | { tag: "Context"; lines: string[] }
  | { tag: "ReplacePrompt"; prompt: string };

export type ContinueVerdict =
  | { tag: "Allow" }
  | { tag: "Deny"; reason: string };

export interface ICompactionGate {
  OnCompacting(session: SessionId): Promise<CompactionContribution>
  AutoContinue(session: SessionId): Promise<ContinueVerdict>
}

// ---- P-10 Storage
export interface IHostPaths {
  readonly StateRoot: WorkspacePath
  readonly ProjectRoot: WorkspacePath
  readonly ProjectId: string
}

// ---- P-11 Lifecycle
export interface HostCapabilities {
  HostName: string
  Supports: ReadonlySet<HostCapability>
}

export interface AgentRoleSpec {
  Role: AgentRole
  Description?: string
  Route?: ModelRoute
  ToolPolicy?: Record<string, boolean>
}

export interface IAgentCatalog {
  Roles(): HostResult<AgentRoleSpec[]>
}

export interface HostPorts {
  Sessions: ISessionPort
  Prompts: IPromptPort
  Transcript: ITranscriptPort
  Models: IModelCatalog
  Signals: ISignalSource
  Tools: IToolRegistrar
  Paths: IHostPaths
  Config: IHostConfigPort
  Diagnostics: IDiagnosticSink
  Agents: IAgentCatalog
  Capabilities: HostCapabilities
}

export interface ProductHooks {
  ToolGate?: IToolGate
  Turn?: ITurnTransform
  Compaction: ICompactionGate
  Spawn?: SpawnRouting
  OnSignal?: (arg: HostSignal) => Promise<void>
  Dispose: () => Promise<void>
}

export interface IHostPlugin {
  Activate(ports: HostPorts): HostResult<ProductHooks>
}

export type SendOutcome =
  | { tag: "AdmittedWithReceipt"; value: TransportReceipt }
  | { tag: "AdmittedWithPhysicalMessage"; value: PhysicalUserMessageId }
  | { tag: "Retryable"; reason: string }
  | { tag: "AcceptanceUnknown"; reason: string }
  | { tag: "Fatal"; reason: string };

// ---- P-12 Diagnostics
export interface IDiagnosticSink {
  Emit(operation: string, fields: [string, string][]): void
}

// ---- P-19 HostConfig
export type HostSettingValue =
  | { tag: "Bool"; value: boolean }
  | { tag: "Text"; value: string };

export interface RequiredSetting {
  Path: string[]
  Value: HostSettingValue
}

export interface SettingGap {
  Path: string[]
  Reason: string
}

export interface IHostConfigPort {
  Read(path: string[]): Result<HostSettingValue | undefined, HostError>
  Apply(settings: RequiredSetting[]): Result<SettingGap | undefined, HostError>
}
