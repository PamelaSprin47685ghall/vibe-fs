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
}

export interface RuntimeHandle {
  readonly __brand: "RuntimeHandle";
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
  readonly runtime: RuntimeHandle | null;
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
  readonly loadConfigOrDefault: () => ConfigFile;
  readonly readAgentDefinition: (
    runtime: RuntimeHandle | null,
    workspacePath: string,
    agentId: string,
  ) => Promise<AgentDefinitionPackage>;
  readonly resolveAgentFrontmatter: (
    runtime: RuntimeHandle | null,
    workspacePath: string,
    agentId: string,
  ) => Promise<AgentFrontmatterPackage>;
  readonly resolveAgentInheritanceChain: (
    request: AgentInheritanceRequest,
  ) => Promise<readonly AgentInheritanceEntry[]>;
  readonly findWorkspaceEntry: (
    configFile: ConfigFile,
    workspaceId: string,
  ) => FindWorkspaceEntryResult | undefined;
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

export interface PluginRegistration {
  toolNames: string[];
  tools: PluginToolDefinition[];
  wrappers: PluginToolWrapper[];
  mcpServers: Readonly<Record<string, string>>;
  contextInjector: { inject: (projectPath: string) => Promise<unknown> };
  eventHook: (event: PluginEvent, helpers: PluginEventHelpers) => Promise<void>;
  slashCommands: PluginSlashCommandDefinition[];
  getToolPolicy: (agentId: string, role?: string) => MuxToolPolicy | null;
}

export function createRegistration(deps: unknown): PluginRegistration;
export function buildCapsFileReadData(projectRoot: string): Promise<CapsFileReadEntry[]>;
