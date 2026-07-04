export interface MuxToolPolicy {
  readonly add?: string[];
  readonly remove?: string[];
}

export type JsonSchema = {
  readonly type: "object";
  readonly properties: Readonly<Record<string, unknown>>;
  readonly required?: readonly string[];
  readonly additionalProperties?: boolean;
};

export interface CapsFileReadEntry {
  path: string;
  callId: string;
  input: { path: string };
  output: {
    success: true;
    file_size: number;
    modifiedTime: string;
    lines_read: number;
    content: string;
  };
}

export interface ToolLike {
  name?: string;
  description?: string;
  parameters?: JsonSchema;
  execute?: (...args: readonly unknown[]) => unknown;
  [key: string]: unknown;
}

export interface PluginToolLike {
  name?: string;
  description?: string;
  parameters?: unknown;
  inputSchema?: unknown;
  execute?: (config: unknown, args: unknown) => Promise<unknown>;
  [key: string]: unknown;
}

export interface PluginToolDefinition {
  name: string;
  description: string;
  parameters: unknown;
  execute: (config: unknown, args: unknown) => Promise<unknown>;
  condition?: (config: unknown) => boolean;
}

export interface PluginToolWrapper {
  targetTool: string;
  wrapper: (tool: PluginToolLike, config: unknown) => PluginToolLike;
}

export interface ParentRuntimeAiSettings {
  readonly modelString?: string;
  readonly thinkingLevel?: string;
}

export interface PluginToolConfiguration {
  readonly cwd: string;
  readonly workspaceId?: string;
  readonly runtime?: RuntimeHandle | null;
  readonly taskService?: TaskServiceLike;
  readonly abortSignal?: AbortSignal;
  readonly muxEnv?: Record<string, string>;
  readonly subagentRole?: string;
}

export interface RuntimeHandle {
  readonly __brand?: "RuntimeHandle";
}

export interface TaskCreateInput {
  readonly parentWorkspaceId: string;
  readonly kind: "agent";
  readonly agentId: string;
  readonly modelString?: string;
  readonly thinkingLevel?: string;
  readonly parentRuntimeAiSettings?: ParentRuntimeAiSettings;
  readonly prompt: string;
  readonly title: string;
  readonly experiments?: {
    readonly subagentRole?: string;
    readonly toolPolicy?: { readonly disabledTools?: readonly string[] };
  };
}

export interface TaskCreateResult {
  readonly success: true;
  readonly data: {
    readonly taskId: string;
    readonly kind: string;
    readonly status: string;
  };
}

export interface TaskCreateError {
  readonly success: false;
  readonly error: string;
}

export interface TaskWaitOptions {
  readonly requestingWorkspaceId: string;
  readonly abortSignal?: AbortSignal;
  readonly backgroundOnMessageQueued?: boolean;
  readonly timeoutMs?: number;
}

export interface TaskWaitResult {
  readonly reportMarkdown: string;
}

export interface TaskServiceLike {
  create(input: TaskCreateInput): Promise<TaskCreateResult | TaskCreateError>;
  waitForAgentReport(taskId: string, opts: TaskWaitOptions): Promise<TaskWaitResult>;
}

export interface LoggerLike {
  debug: (msg: string, data?: unknown) => void;
}

export interface AgentDefinitionPackage {
  readonly id: string;
  readonly scope: string;
  readonly frontmatter: {
    readonly name: string;
    readonly ai?: { readonly model?: string; readonly thinkingLevel?: string };
  };
  readonly body: string;
}

export interface AgentFrontmatterPackage {
  readonly name: string;
  readonly ai?: { readonly model?: string; readonly thinkingLevel?: string };
}

export interface AgentInheritanceEntry {
  readonly id: string;
}

export interface AgentInheritanceRequest {
  readonly runtime: any;
  readonly workspacePath: string;
  readonly agentId: string;
  readonly agentDefinition: AgentDefinitionPackage;
  readonly workspaceId: string;
  readonly maxDepth?: number;
}

export interface WorkspaceAiSettings {
  readonly model: string;
  readonly thinkingLevel?: string;
}

export interface WorkspaceEntry {
  readonly id: string;
  readonly aiSettings?: WorkspaceAiSettings;
  readonly aiSettingsByAgent?: Record<string, WorkspaceAiSettings>;
}

export interface ProjectEntry {
  readonly workspaces: readonly WorkspaceEntry[];
}

export interface ConfigFile {
  readonly projects?: Map<string, ProjectEntry>;
  readonly agentAiDefaults?: Record<
    string,
    { readonly modelString?: string; readonly thinkingLevel?: string } | undefined
  >;
  readonly subagentAiDefaults?: Record<
    string,
    { readonly modelString?: string; readonly thinkingLevel?: string } | undefined
  >;
}

export interface FindWorkspaceEntryResult {
  readonly workspace: WorkspaceEntry;
}

/** Partial MUX_* overlay from the parent conversation (MUX_MODEL_STRING, MUX_THINKING_LEVEL). */
export type ParentRuntimeMuxEnvOverlay = {
  readonly MUX_MODEL_STRING?: string;
  readonly MUX_THINKING_LEVEL?: string;
};

export interface WorkspacePluginContext {
  readonly cwd: string;
  readonly runtime: RuntimeHandle | null;
  readonly muxEnv: Record<string, string>;
}

export interface HostDependencies {
  readonly log: LoggerLike;
  readonly taskService?: TaskServiceLike;
  readonly resolveWorkspacePluginContext?: (
    workspaceId: string,
    parentRuntime?: ParentRuntimeMuxEnvOverlay | null,
  ) => Promise<WorkspacePluginContext | null>;
  readonly loadConfigOrDefault: () => unknown;
  readonly readAgentDefinition: (
    runtime: any,
    workspacePath: string,
    agentId: string,
  ) => Promise<AgentDefinitionPackage>;
  readonly resolveAgentFrontmatter: (
    runtime: any,
    workspacePath: string,
    agentId: string,
  ) => Promise<AgentFrontmatterPackage>;
  readonly resolveAgentInheritanceChain: (
    request: AgentInheritanceRequest,
  ) => Promise<readonly AgentInheritanceEntry[]>;
  readonly findWorkspaceEntry: (
    configFile: any,
    workspaceId: string,
  ) => FindWorkspaceEntryResult | undefined | null;
  readonly getChatHistory?: (workspaceId: string) => Promise<unknown[]>;
}

export interface PluginEvent {
  readonly type: "stream-end" | "stream-abort" | "error";
  readonly workspaceId?: string;
  readonly properties?: object;
}

export interface PluginEventHelpers {
  nudge: (workspaceId: string, message: string) => Promise<boolean>;
  getTodos: (workspaceId: string) => Promise<readonly string[]>;
}

export interface PluginSlashCommandDefinition {
  key: string;
  description: string;
  inputHint?: string;
  execute: (workspaceId: string, args: string) => Promise<string | null>;
}

/** Payload the Mux host passes after tool execution (mirrors `MuxHookInputCodec.decodeMuxToolExecuteAfterInput`). */
export interface PluginToolExecuteAfterInput {
  /** Normalized tool name (e.g. `write`, `coder`, `executor`). */
  tool: string;
  sessionID?: string;
  workspaceId?: string;
  /** Workspace root; falls back to deps.directory when omitted. */
  directory?: string;
  /** Tool arguments as executed. */
  args?: unknown;
  callID?: string;
}

/** Mutable tool result envelope; `output` may be rewritten in-place on success. */
export interface PluginToolExecuteAfterOutput {
  output?: string;
  /** When non-empty, the hook treats the call as failed. */
  error?: string;
}

export interface PluginRegistration {
  toolNames: string[];
  tools: PluginToolDefinition[];
  wrappers: PluginToolWrapper[];
  mcpServers: Readonly<Record<string, string>>;
  contextInjector: { inject: (projectPath: string) => Promise<unknown> };
  eventHook: (event: PluginEvent, helpers: PluginEventHelpers) => Promise<void>;
  slashCommands: PluginSlashCommandDefinition[];
  messagesTransform?: (
    input: { workspacePath?: string; workspaceId?: string; effectiveAgentId?: string } & Record<string, unknown>,
    output: { messages: unknown[] },
  ) => Promise<void>;
  /**
   * Post-tool hook for output hints and side effects.
   * F# `createRegistration` also exposes the same handler under the wire key `tool.execute.after`.
   */
  toolExecuteAfter?: (
    input: PluginToolExecuteAfterInput,
    output: PluginToolExecuteAfterOutput,
  ) => Promise<void>;
  /**
   * Runs backlog projection on the message list after session compaction (Opencode: `experimental.session.compacting`).
   */
  compactingTransform?: (
    input: { sessionID?: string; workspaceId?: string; workspacePath?: string } & Record<string, unknown>,
    output: { messages: unknown[] },
  ) => Promise<void>;
  /** Mux plugin hook for post-tool bookkeeping (runtime key `tool.execute.after`). */
  readonly ["tool.execute.after"]?: (
    input: {
      tool: string;
      sessionID?: string;
      workspaceId?: string;
      directory?: string;
      callID?: string;
      args?: unknown;
    } & Record<string, unknown>,
    output: { output: string; error: string; args?: unknown },
  ) => Promise<void>;
  /**
   * Pre-tool hook (runtime key `tool.execute.before`). Sets the `_ui` label on
   * `output.args` in place; the host keeps the same args reference it passed in.
   */
  readonly ["tool.execute.before"]?: (
    input: {
      tool: string;
      sessionID?: string;
      workspaceId?: string;
      args?: unknown;
      callID?: string;
    } & Record<string, unknown>,
    output: { args?: unknown } & Record<string, unknown>,
  ) => Promise<void>;
  /**
   * System prompt transform (runtime key `systemTransform`). Clears the system
   * output by setting `system.length = 0`.
   */
  readonly ["systemTransform"]?: (
    input: { system?: { length?: number } | null } & Record<string, unknown>,
    output: { system?: { length: number; content?: unknown } | null } & Record<string, unknown>,
  ) => Promise<void>;
  getToolPolicy: (agentId: string, role?: string) => MuxToolPolicy | null;
}

export function createRegistration(deps: unknown): PluginRegistration;
export function buildCapsFileReadData(projectRoot: string): Promise<CapsFileReadEntry[]>;

export function getPluginToolPolicy(agentId: string, role?: string | null): MuxToolPolicy;
export function collectReadOutputs(messages: ReadonlyArray<unknown>): string[];
export function deduplicateReadOutputsWithSeen(
  seenOutputs: ReadonlyArray<string>,
  messages: ReadonlyArray<unknown>,
): unknown[];

export function deduplicateModelReadOutputsWithSeen(
  seenOutputs: ReadonlyArray<string>,
  messages: ReadonlyArray<unknown>,
): [string[], unknown[]];

declare module "wanxiangshu/omp" {
  export interface OmpReviewStore {
    activateReview(sessionId: string, task: string, createdAt: number): void;
    deactivateReview(sessionId: string): void;
    tryLockReview(sessionId: string): boolean;
    unlockReview(sessionId: string): void;
    setPendingReview(sessionId: string, resolve: (result: unknown) => void): void;
    resolvePendingReview(sessionId: string, result: unknown): boolean;
    getReviewTask(sessionId: string): string | undefined;
    addChild(parentId: string, childId: string): void;
    clearReviewSessions(): void;
  }

  export const reviewStore: OmpReviewStore;

  export default function wanxiangshuExtension(pi: unknown): Promise<void>;
}
