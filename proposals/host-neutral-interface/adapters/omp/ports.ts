// TS mirror of the host-neutral ports in src/Wanxiangshu/Host/Ports/*.fsi (HNI P-00..P-12).
// Names must match the F# signatures exactly; mirror-check.mjs fails on drift.
// DUs are `{ tag: "<Case>", ... }` objects; F# option = `T | undefined`.

// ---- P-00 Vocabulary
export type HostCapability =
  | "ChildSessions" | "SessionDiscovery" | "Interrupt" | "ToolDefinitionEdit"
  | "SubagentRouting" | "ProviderParams" | "StreamDeltas";
export type HostError =
  | { tag: "Unsupported"; capability: HostCapability }
  | { tag: "NotFound"; what: string }
  | { tag: "Refused"; reason: string }
  | { tag: "Transport"; reason: string }
  | { tag: "Unknown"; reason: string };
export type Result<T, E> = { ok: true; value: T } | { ok: false; error: E };
export type HostResult<T> = Promise<Result<T, HostError>>;
export type SessionId = string & { readonly __brand: "SessionId" };
export type ToolCallId = string & { readonly __brand: "ToolCallId" };
export type PromptKey = string & { readonly __brand: "PromptKey" };
export type TranscriptMessageAddress = string & { readonly __brand: "TranscriptMessageAddress" };
export type WorkspacePath = string;
export type AgentRole = string;
export type CanonicalJsonText = string;
export type JsonSchema = string;
export type HostFailureKind =
  | "RateLimited" | "ContextOverflow" | "Authentication" | "Network" | "Provider" | "Aborted" | "Unknown";
export interface HostFailure { Kind: HostFailureKind; Message: string; StatusCode?: number; HostRetryable?: boolean }
export type TurnTerminal =
  | { tag: "Completed"; finish?: string } | { tag: "Cancelled" } | { tag: "Interrupted" }
  | { tag: "Failed"; failure: HostFailure };

// ---- P-12 Diagnostics
export interface IDiagnosticSink { Emit(operation: string, fields: [string, string][]): void }
// ---- P-10 Storage
export interface IHostPaths { readonly StateRoot: WorkspacePath; readonly ProjectRoot: WorkspacePath; readonly ProjectId: string }
// ---- P-08 Model routing
export interface ModelRoute { Provider: string; Model: string; Variant?: string }
export interface SpawnRequest { Parent: SessionId; Role: AgentRole; Candidates: ModelRoute[]; SpawnKey?: string }
export type SpawnRoute = { tag: "Keep" } | { tag: "Route"; routes: ModelRoute[] } | { tag: "Refuse"; reason: string };
export interface IModelCatalog {
  Available(): HostResult<ModelRoute[]>;
  Apply(session: SessionId, route: ModelRoute): HostResult<void>;
}
export type SpawnRouting = (request: SpawnRequest) => Promise<SpawnRoute>;
// ---- P-01 Sessions
export interface SessionSpec { Title?: string; Role?: AgentRole; Workspace?: WorkspacePath }
export interface SessionInfo { Id: SessionId; Parent?: SessionId; Role?: AgentRole; Title?: string }
export interface ISessionPort {
  Create(parent: SessionId | undefined, spec: SessionSpec): HostResult<SessionId>;
  Parent(session: SessionId): HostResult<SessionId | undefined>;
  Children(parent: SessionId): HostResult<SessionInfo[]>;
  Abort(session: SessionId): HostResult<void>;
  Interrupt(session: SessionId): HostResult<void>;
  Close(session: SessionId): HostResult<void>;
  FindByTitle(title: string): HostResult<SessionId[]>;
}
// ---- P-02 Prompts
export interface PromptRequest {
  Text: string; Route?: ModelRoute; Role?: AgentRole; Workspace?: WorkspacePath;
  Correlation?: PromptKey; ToolPolicy?: Record<string, boolean>;
}
export type DeliveryVerdict =
  | { tag: "OwnedSettled" } | { tag: "Refused"; reason: string } | { tag: "OutcomeUnknown"; reason: string };
export type LateDeliveryListener = (verdict: DeliveryVerdict) => Promise<void>;
/** Foundation.Outcome.SendOutcome */
export type SendOutcome =
  | { tag: "AdmittedWithReceipt"; receipt: string } | { tag: "AdmittedWithPhysicalMessage"; message: string }
  | { tag: "Retryable"; reason: string } | { tag: "AcceptanceUnknown"; reason: string } | { tag: "Fatal"; reason: string };
export interface IPromptPort {
  Send(session: SessionId, request: PromptRequest, late: LateDeliveryListener | undefined): Promise<SendOutcome>;
}
// ---- P-03 Transcript
export type TranscriptRole = "User" | "Assistant" | "System";
export type ToolCallState = "Pending" | "Running" | "Completed" | "Failed";
export interface ToolCallRecord { CallId: ToolCallId; Tool: string; Input: CanonicalJsonText; State: ToolCallState; Output?: string }
export type TranscriptPart =
  | { tag: "Text"; text: string } | { tag: "Reasoning"; text: string } | { tag: "ToolUse"; call: ToolCallRecord }
  | { tag: "File"; mime: string; uri: string } | { tag: "Other"; kind: string };
export interface TranscriptMessage {
  Id: TranscriptMessageAddress; Role: TranscriptRole; Agent?: AgentRole; Route?: ModelRoute;
  Parent?: TranscriptMessageAddress; CreatedAt?: number; Completed: boolean; Terminal?: TurnTerminal;
  IsCompaction: boolean; Correlation?: PromptKey; Parts: TranscriptPart[];
}
export interface ITranscriptPort { Messages(session: SessionId): HostResult<TranscriptMessage[]> }
// ---- P-04 Tools
export interface ToolInvocation {
  Session: SessionId; CallId: ToolCallId; Workspace: WorkspacePath; Args: CanonicalJsonText;
  Cancellation: AbortSignal; Progress?: (line: string) => void;
}
export interface ToolOutcome { Text: string; Truncated: boolean; IsError: boolean; Details?: CanonicalJsonText }
export interface ToolSpec { Name: string; Description: string; Schema: JsonSchema; Execute: (call: ToolInvocation) => Promise<ToolOutcome> }
export interface IToolRegistrar { Register(tools: ToolSpec[]): Result<void, HostError> }
// ---- P-05 Tool gates
export interface ToolCallView { Session: SessionId; CallId: ToolCallId; Tool: string; Args: CanonicalJsonText; Role?: AgentRole }
export type ToolCallVerdict = { tag: "Allow" } | { tag: "Rewrite"; args: CanonicalJsonText } | { tag: "Refuse"; reason: string };
export type ToolResultEdit = { tag: "Keep" } | { tag: "Replace"; result: ToolOutcome };
export interface ToolDefinitionView { Tool: string; Description: string; Schema: JsonSchema }
export type ToolDefinitionEdit = { tag: "Keep" } | { tag: "Describe"; description: string; schema: JsonSchema };
export interface IToolGate {
  BeforeTool(call: ToolCallView): Promise<ToolCallVerdict>;
  AfterTool(call: ToolCallView, result: ToolOutcome): Promise<ToolResultEdit>;
  DefineTool(definition: ToolDefinitionView): ToolDefinitionEdit;
}
// ---- P-07 Signals
export interface AttemptRef { Session: SessionId; Message?: TranscriptMessageAddress }
export interface ToolCallRef { Session: SessionId; CallId: ToolCallId; Tool: string }
export interface RetryNotice { Session: SessionId; Attempt: number; Failure: HostFailure }
export type StreamChannel = "Text" | "Reasoning" | "ToolInput";
export interface StreamChunk { Session: SessionId; Message?: TranscriptMessageAddress; Channel: StreamChannel; Delta: string }
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
export interface ISignalSource { Subscribe(listener: (signal: HostSignal) => Promise<void>): { dispose(): void } }
// ---- P-06 Turn transforms
export interface TurnContext { Session: SessionId; Role?: AgentRole; Route?: ModelRoute; Workspace?: WorkspacePath }
export interface TurnRequest { Context: TurnContext; Message?: TranscriptMessageAddress; Text: string; Correlation?: PromptKey }
export type TurnAdmission = { tag: "Admit"; route?: ModelRoute } | { tag: "Refuse"; reason: string };
export interface ContextMessage { Address?: TranscriptMessageAddress; Role: TranscriptRole; Parts: TranscriptPart[] }
export type ContextEdit = { tag: "Keep" } | { tag: "Replace"; messages: ContextMessage[] };
export interface ProviderParams { Temperature?: number; TopP?: number; MaxOutputTokens?: number; Reasoning?: string; Extra?: CanonicalJsonText }
export type ParamsVerdict = { tag: "Accept" } | { tag: "Adjust"; parameters: ProviderParams } | { tag: "Refuse"; reason: string };
export interface ITurnTransform {
  AdmitTurn(request: TurnRequest): Promise<TurnAdmission>;
  SystemPrompt(context: TurnContext, system: string[]): Promise<string[]>;
  ContextMessages(context: TurnContext, messages: ContextMessage[]): Promise<ContextEdit>;
  ProviderParams(context: TurnContext, parameters: ProviderParams): Promise<ParamsVerdict>;
}
// ---- P-09 Compaction
export type CompactionContribution = { tag: "Nothing" } | { tag: "Context"; lines: string[] } | { tag: "ReplacePrompt"; prompt: string };
export type ContinueVerdict = { tag: "Allow" } | { tag: "Deny"; reason: string };
export interface ICompactionGate {
  OnCompacting(session: SessionId): Promise<CompactionContribution>;
  AutoContinue(session: SessionId): Promise<ContinueVerdict>;
}
// ---- P-11 Lifecycle
export interface HostCapabilities { HostName: string; Supports: ReadonlySet<HostCapability> }
export interface AgentRoleSpec { Role: AgentRole; Description?: string; Route?: ModelRoute; ToolPolicy?: Record<string, boolean> }
export interface IAgentCatalog { Roles(): AgentRoleSpec[] }
export interface HostPorts {
  Sessions: ISessionPort; Prompts: IPromptPort; Transcript: ITranscriptPort; Models: IModelCatalog;
  Signals: ISignalSource; Tools: IToolRegistrar; Paths: IHostPaths; Diagnostics: IDiagnosticSink;
  Agents: IAgentCatalog; Capabilities: HostCapabilities;
}
export interface ProductHooks {
  ToolGate: IToolGate; Turn: ITurnTransform; Compaction: ICompactionGate; Spawn?: SpawnRouting;
  OnSignal: (signal: HostSignal) => Promise<void>; Dispose: () => Promise<void>;
}
export interface IHostPlugin { Activate(ports: HostPorts): HostResult<ProductHooks> }
