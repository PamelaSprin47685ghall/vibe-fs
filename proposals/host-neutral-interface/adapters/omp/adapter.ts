// omp (oh-my-pi 18.12.4) adapter skeleton for the host-neutral ports (HNI-03).
// Thin by construction: decode omp payloads into port shapes, encode port results into omp results,
// register handlers. No product decision is taken here: every verdict comes from ProductHooks.
import type { ExtensionAPI, ExtensionContext, ContextEvent } from "@oh-my-pi/pi-coding-agent/extensibility/extensions/types";
import type * as P from "./ports";

export type AgentMessage = ContextEvent["messages"][number];
export type OmpApi = Pick<ExtensionAPI, "on" | "registerTool" | "sendUserMessage" | "appendEntry" | "setModel" | "setActiveTools" | "logger">;

/** Wire seam to the omp SDK for sessions this plugin owns (bound in sdk-children.ts). */
export interface OmpChildSessions {
  create(parent: string | undefined, title: string | undefined, cwd: string | undefined): Promise<string>;
  prompt(session: string, text: string): Promise<boolean>;
  abort(session: string): Promise<void>;
  close(session: string): Promise<void>;
  parentOf(session: string): string | undefined;
  childrenOf(parent: string): { id: string; title?: string }[];
  findByTitle(title: string): Promise<string[]>;
  messages(session: string): AgentMessage[] | undefined;
}

const ok = <T>(value: T): P.Result<T, P.HostError> => ({ ok: true, value });
const err = <T>(error: P.HostError): P.Result<T, P.HostError> => ({ ok: false, error });
const sid = (s: string) => s as P.SessionId;
const transport = (e: unknown): P.HostError => ({ tag: "Transport", reason: e instanceof Error ? e.message : String(e) });
async function guard<T>(f: () => Promise<T>): P.HostResult<T> {
  try { return ok(await f()); } catch (e) { return err(transport(e)); }
}

// ---------------------------------------------------------------- decode (omp -> ports)
export function decodeParts(m: AgentMessage): P.TranscriptPart[] {
  const content = (m as { content?: unknown }).content;
  if (typeof content === "string") return [{ tag: "Text", text: content }];
  if (!Array.isArray(content)) return [];
  return content.map((c: { type: string; [k: string]: unknown }): P.TranscriptPart => {
    switch (c.type) {
      case "text": return { tag: "Text", text: String(c.text) };
      case "thinking": return { tag: "Reasoning", text: String(c.thinking) };
      case "toolCall": return { tag: "ToolUse", call: {
        CallId: String(c.id) as P.ToolCallId, Tool: String(c.name), Input: JSON.stringify(c.arguments ?? {}), State: "Pending" } };
      case "image": return { tag: "File", mime: String(c.mimeType ?? "image/*"), uri: "inline:" };
      default: return { tag: "Other", kind: c.type };
    }
  });
}

export function decodeRole(m: AgentMessage): P.TranscriptRole | undefined {
  const role = (m as { role?: string }).role;
  return role === "user" ? "User" : role === "assistant" ? "Assistant" : role === "system" ? "System" : undefined;
}

export function decodeFailure(message: string | undefined, aborted: boolean): P.HostFailure {
  // Wire-level kind only; whether this is retryable or budgeted is product logic.
  return { Kind: aborted ? "Aborted" : "Provider", Message: message ?? "" };
}

export function decodeTerminal(m: AgentMessage): P.TurnTerminal | undefined {
  const a = m as { role?: string; stopReason?: string; errorMessage?: string };
  if (a.role !== "assistant" || a.stopReason === undefined) return undefined;
  if (a.stopReason === "aborted") return { tag: "Cancelled" };
  if (a.stopReason === "error") return { tag: "Failed", failure: decodeFailure(a.errorMessage, false) };
  return { tag: "Completed", finish: a.stopReason };
}

export function decodeMessage(m: AgentMessage, index: number): P.TranscriptMessage | undefined {
  const role = decodeRole(m);
  if (role === undefined) return undefined;
  const a = m as { provider?: string; model?: string; timestamp?: number };
  return {
    Id: `omp:${index}` as P.TranscriptMessageAddress, Role: role,
    Route: a.provider && a.model ? { Provider: a.provider, Model: a.model } : undefined,
    CreatedAt: a.timestamp, Completed: role !== "Assistant" || decodeTerminal(m) !== undefined,
    Terminal: decodeTerminal(m), IsCompaction: false, Parts: decodeParts(m),
  };
}

function encodeParts(parts: P.TranscriptPart[]): { type: "text"; text: string }[] {
  return parts.flatMap((p) => (p.tag === "Text" ? [{ type: "text" as const, text: p.text }] : []));
}

// ---------------------------------------------------------------- HostPorts over omp
export function createOmpHostPorts(pi: OmpApi, ctx: () => ExtensionContext, children: OmpChildSessions,
                                   agents: P.AgentRoleSpec[]): { ports: P.HostPorts; emit: (s: P.HostSignal) => Promise<void> } {
  const listeners = new Set<(s: P.HostSignal) => Promise<void>>();
  const emit = async (s: P.HostSignal) => { for (const l of listeners) await l(s); };
  const current = () => ctx().sessionManager.getSessionId();
  const isOwnChild = (s: string) => s !== current();
  const registered: P.ToolSpec[] = [];

  const sessions: P.ISessionPort = {
    Create: (parent, spec) => guard(async () => sid(await children.create(parent, spec.Title, spec.Workspace))),
    Parent: (s) => guard(async () => { const p = isOwnChild(s) ? children.parentOf(s) : ctx().sessionManager.getHeader()?.parentSession; return p ? sid(p) : undefined; }),
    Children: (s) => guard(async () => children.childrenOf(s).map((c) => ({ Id: sid(c.id), Parent: s, Title: c.title }))),
    Abort: (s) => guard(async () => { if (isOwnChild(s)) await children.abort(s); else ctx().abort(); }),
    Interrupt: (s) => guard(async () => { if (isOwnChild(s)) await children.abort(s); else ctx().abort(); }),
    Close: (s) => isOwnChild(s) ? guard(() => children.close(s)) : Promise.resolve(err({ tag: "Refused", reason: "cannot close the host's own session" })),
    FindByTitle: (t) => guard(async () => (await children.findByTitle(t)).map(sid)),
  };

  const prompts: P.IPromptPort = {
    async Send(s, request, late) {
      try {
        if (request.Correlation !== undefined) pi.appendEntry("wxs.prompt-key", { session: s, key: request.Correlation });
        if (isOwnChild(s)) {
          const accepted = await children.prompt(s, request.Text);
          return accepted ? { tag: "AdmittedWithReceipt", receipt: `omp:${s}` } : { tag: "Retryable", reason: "omp refused prompt" };
        }
        pi.sendUserMessage(request.Text, { deliverAs: "followUp" });
        if (late) await late({ tag: "OwnedSettled" });
        return { tag: "AdmittedWithReceipt", receipt: `omp:${s}` };
      } catch (e) {
        return { tag: "AcceptanceUnknown", reason: transport(e).tag };
      }
    },
  };

  const transcript: P.ITranscriptPort = {
    Messages: (s) => guard(async () => {
      const raw = isOwnChild(s) ? children.messages(s) : (ctx().sessionManager.getEntries()
        .flatMap((e) => (e.type === "message" ? [e.message as AgentMessage] : [])));
      if (raw === undefined) throw new Error(`unknown session ${s}`);
      return raw.flatMap((m, i) => { const d = decodeMessage(m, i); return d ? [d] : []; });
    }),
  };

  const models: P.IModelCatalog = {
    Available: () => guard(async () => ctx().modelRegistry.getAvailable().map((m) => ({ Provider: m.provider, Model: m.id }))),
    Apply: (_s, route) => guard(async () => {
      const model = ctx().modelRegistry.find(route.Provider, route.Model);
      if (!model) throw new Error(`model not found: ${route.Provider}/${route.Model}`);
      if (!(await pi.setModel(model))) throw new Error("omp rejected model");
    }),
  };

  const tools: P.IToolRegistrar = {
    Register(specs) {
      for (const spec of specs) {
        registered.push(spec);
        pi.registerTool({
          name: spec.Name, label: spec.Name, description: spec.Description,
          parameters: JSON.parse(spec.Schema),
          async execute(toolCallId, params, signal, onUpdate, c) {
            const r = await spec.Execute({
              Session: sid(c.sessionManager.getSessionId()), CallId: toolCallId as P.ToolCallId, Workspace: c.cwd,
              Args: JSON.stringify(params), Cancellation: signal ?? new AbortController().signal,
              Progress: onUpdate ? (line) => onUpdate({ content: [{ type: "text", text: line }], details: undefined }) : undefined,
            });
            return { content: [{ type: "text", text: r.Text }], details: r.Details === undefined ? undefined : JSON.parse(r.Details), isError: r.IsError };
          },
        });
      }
      return ok(undefined);
    },
  };

  const ports: P.HostPorts = {
    Sessions: sessions, Prompts: prompts, Transcript: transcript, Models: models, Tools: tools,
    Signals: { Subscribe: (l) => { listeners.add(l); return { dispose: () => void listeners.delete(l) }; } },
    Paths: { get StateRoot() { return `${ctx().cwd}/.wanxiangshu`; }, get ProjectRoot() { return ctx().cwd; }, get ProjectId() { return ctx().cwd; } },
    Diagnostics: { Emit: (op, fields) => pi.logger.debug(op, Object.fromEntries(fields)) },
    Agents: { Roles: () => agents },
    Capabilities: { HostName: "omp", Supports: new Set<P.HostCapability>(["ChildSessions", "SessionDiscovery", "Interrupt", "SubagentRouting", "ProviderParams", "StreamDeltas"]) },
  };
  return { ports, emit };
}

// ---------------------------------------------------------------- ProductHooks -> omp registrations
export function registerProductHooks(pi: OmpApi, hooks: P.ProductHooks, emit: (s: P.HostSignal) => Promise<void>): void {
  const turnContext = (c: ExtensionContext): P.TurnContext => ({
    Session: sid(c.sessionManager.getSessionId()), Workspace: c.cwd,
    Route: c.model ? { Provider: c.model.provider, Model: c.model.id } : undefined,
  });
  const view = (c: ExtensionContext, id: string, tool: string, input: unknown): P.ToolCallView =>
    ({ Session: sid(c.sessionManager.getSessionId()), CallId: id as P.ToolCallId, Tool: tool, Args: JSON.stringify(input ?? {}) });
  const signal = (s: P.HostSignal) => Promise.all([emit(s), hooks.OnSignal(s)]).then(() => undefined);

  pi.on("input", async (e, c) => {
    const verdict = await hooks.Turn.AdmitTurn({ Context: turnContext(c), Text: e.text });
    return verdict.tag === "Refuse" ? { handled: true } : undefined;
  });
  pi.on("before_agent_start", async (e, c) => ({ systemPrompt: await hooks.Turn.SystemPrompt(turnContext(c), e.systemPrompt) }));
  pi.on("context", async (e, c) => {
    const edit = await hooks.Turn.ContextMessages(turnContext(c), e.messages.flatMap((m) => {
      const role = decodeRole(m); return role ? [{ Role: role, Parts: decodeParts(m) }] : [];
    }));
    if (edit.tag === "Keep") return undefined;
    return { messages: edit.messages.map((m): AgentMessage => m.Role === "User"
      ? { role: "user", content: encodeParts(m.Parts), timestamp: Date.now() } as AgentMessage
      : { role: "assistant", content: encodeParts(m.Parts) } as unknown as AgentMessage) };
  });
  pi.on("tool_call", async (e, c) => {
    const a = await hooks.ToolGate.BeforeTool(view(c, e.toolCallId, e.toolName, e.input));
    if (a.tag === "Refuse") return { block: true, reason: a.reason };
    if (a.tag === "Rewrite") return { input: JSON.parse(a.args) };
    return undefined;
  });
  pi.on("tool_result", async (e, c) => {
    const text = e.content.flatMap((x) => (x.type === "text" ? [x.text] : [])).join("\n");
    const edit = await hooks.ToolGate.AfterTool(view(c, e.toolCallId, e.toolName, e.input), { Text: text, Truncated: false, IsError: e.isError });
    if (edit.tag === "Keep") return undefined;
    return { content: [{ type: "text", text: edit.result.Text }], isError: edit.result.IsError };
  });
  pi.on("session.compacting", async (e) => {
    const r = await hooks.Compaction.OnCompacting(sid(e.sessionId));
    return r.tag === "Context" ? { context: r.lines } : r.tag === "ReplacePrompt" ? { prompt: r.prompt } : undefined;
  });
  pi.on("session_stop", async (e) => {
    const v = await hooks.Compaction.AutoContinue(sid(e.session_id));
    return v.tag === "Deny" ? { continue: false, reason: v.reason } : undefined;
  });
  if (hooks.Spawn) {
    const spawn = hooks.Spawn;
    pi.on("before_subagent_spawn", async (e, c) => {
      const r = await spawn({ Parent: sid(c.sessionManager.getSessionId()), Role: e.agent, SpawnKey: e.spawnKey,
        Candidates: e.patterns.flatMap((p) => { const [prov, ...rest] = p.split("/"); return rest.length ? [{ Provider: prov, Model: rest.join("/") }] : []; }) });
      if (r.tag === "Refuse") return { block: true, reason: r.reason };
      if (r.tag === "Route") return { model: r.routes.map((x) => `${x.Provider}/${x.Model}`) };
      return undefined;
    });
  }
  pi.on("session_start", async (_e, c) => signal({ tag: "SessionCreated", info: { Id: sid(c.sessionManager.getSessionId()) } }));
  pi.on("turn_start", async (_e, c) => signal({ tag: "AttemptStarted", attempt: { Session: sid(c.sessionManager.getSessionId()) } }));
  pi.on("turn_end", async (e, c) => {
    const t = decodeTerminal(e.message);
    if (t) await signal({ tag: "AttemptFinished", attempt: { Session: sid(c.sessionManager.getSessionId()) }, terminal: t });
  });
  pi.on("agent_end", async (e, c) => { if (!e.willContinue && c.isIdle()) await signal({ tag: "SessionIdle", session: sid(c.sessionManager.getSessionId()) }); });
  pi.on("auto_retry_start", async (e, c) => signal({ tag: "ProviderRetry", notice: {
    Session: sid(c.sessionManager.getSessionId()), Attempt: e.attempt, Failure: decodeFailure(e.errorMessage, false) } }));
  pi.on("tool_execution_start", async (e, c) => signal({ tag: "ToolStarted", call: { Session: sid(c.sessionManager.getSessionId()), CallId: e.toolCallId as P.ToolCallId, Tool: e.toolName } }));
  pi.on("session_shutdown", async () => { await hooks.Dispose(); });
}

/** omp extension entry: `omp -e <bundle>` loads `export default (pi) => …`. */
export function activateOnOmp(pi: OmpApi, plugin: P.IHostPlugin, children: OmpChildSessions, agents: P.AgentRoleSpec[]) {
  let latest: ExtensionContext | undefined;
  const ctx = () => { if (!latest) throw new Error("omp context not yet available"); return latest; };
  const { ports, emit } = createOmpHostPorts(pi, ctx, children, agents);
  pi.on("session_start", async (_e, c) => {
    latest = c;
    const r = await plugin.Activate(ports);
    if (!r.ok) { pi.logger.error("wanxiangshu activation failed", { error: r.error }); return; }
    registerProductHooks(pi, r.value, emit);
  });
}
