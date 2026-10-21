// omp (oh-my-pi 18.12.4) adapter skeleton for the host-neutral ports (HNI-03).
// Thin by construction: decode omp payloads into port shapes, encode port results into omp results,
// register handlers. No product decision is taken here: every verdict comes from ProductHooks.
//
// Honesty rules this file follows:
//   * A port request the host cannot honour is answered with a typed `HostError`
//     (usually `Unsupported`), never with a silent success and never by dropping
//     part of the caller's request.
//   * Port shapes are taken from the GENERATED mirror (ports.ts), which is
//     derived from the F# signatures; mismatches are fixed here, not in the
//     generator.
import * as P from "./ports";
import type { ExtensionAPI, ExtensionContext, ContextEvent } from "@oh-my-pi/pi-coding-agent/extensibility/extensions/types";

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
  /**
   * The child's transcript ENTRIES, not bare messages: a transcript address
   * is the omp entry id, and `AgentMessage` carries no id. `undefined` means
   * the session is unknown to the host.
   */
  entries(session: string): { id: string; message: AgentMessage }[] | undefined;
  /**
   * Resolve a parent-session *file path* to the session id recorded in that
   * file. omp stores `SessionHeader.parentSession` verbatim and its own callers
   * sometimes pass a file path (see the omp feasibility evidence), so a path
   * shaped value is not a `SessionId` and must not be returned as one.
   */
  resolveSessionIdByPath(path: string): Promise<string | undefined>;
}

const ok = <T>(value: T): P.Result<T, P.HostError> => ({ ok: true, value });
const err = <T>(error: P.HostError): P.Result<T, P.HostError> => ({ ok: false, error });
const sid = (s: string) => s as P.SessionId;
const transport = (e: unknown): P.HostError => ({ tag: "Transport", reason: e instanceof Error ? e.message : String(e) });
/** The human-readable cause of a thrown value, kept verbatim. */
const why = (e: unknown): string => (e instanceof Error ? e.message : String(e));
/**
 * The transport receipt the adapter hands back when the host accepted a
 * prompt. The F# contract says a receipt is ADMISSION-shaped: the Host
 * prompt-send endpoint hands back an `accepted-*` admission receipt
 * (`src/Wanxiangshu/Foundation/Identity.fs`), and every host the product
 * itself drives emits that shape (`accepted-<session>` for OpenCode,
 * `accepted-detached-<promptKey>` for a detached send). A receipt that
 * does not look like one means the Host returned something else and the
 * caller must decide, so the adapter emits the same shape its peers emit
 * rather than a private `omp` namespace.
 *
 * The receipt is a DETACHED admission receipt: it says the host took the
 * prompt, not that a physical message id exists. It is deliberately NOT
 * usable as a message identity.
 */
const admissionReceipt = (s: string): P.TransportReceipt => P.transportReceipt(`accepted-omp-${s}`);
const notFound = (what: string): P.HostError => ({ tag: "NotFound", what });
const unsupported = (capability: P.HostCapability): P.HostError => ({ tag: "Unsupported", capability });
/**
 * True when a recorded `parentSession` value is a session FILE PATH rather than
 * a session id. omp's own session ids are UUIDs (`Bun.randomUUIDv7`) and its
 * session files are `<name>.jsonl` files, so a value carrying a path
 * separator or the `.jsonl` suffix is a path. Such a value is resolved through
 * the host's own lookup — never guessed.
 */
export function looksLikePath(value: string): boolean {
  return value.includes("/") || value.includes("\\") || value.endsWith(".jsonl");
}

/**
 * Raised when the host recorded a parent session it cannot express as a
 * session id. The reason carries the RAW recorded value verbatim.
 */
export class ParentUnresolvedError extends Error {
  readonly reason: string;
  constructor(reason: string) { super(reason); this.reason = reason; }
}
/**
 * Raised when a session entry has no usable id. A transcript address IS the
 * omp entry id, so an entry without one cannot be addressed: the read fails
 * with `HostError.Transport` instead of fabricating an address. The reason
 * names the entry's POSITION so the failure is diagnosable.
 */
export class EntryIdMissingError extends Error {
  readonly reason: string;
  constructor(position: number) {
    const reason = `omp session entry at position ${position} has no id`;
    super(reason);
    this.reason = reason;
  }
}
/** Raised when a session id is unknown to the host; mapped to `HostError.NotFound`. */
export class UnknownSessionError extends Error {
  readonly what: string;
  constructor(what: string) { super(`unknown session ${what}`); this.what = what; }
}

async function guard<T>(f: () => Promise<T>): P.HostResult<T> {
  try {
    return ok(await f());
  } catch (e) {
    if (e instanceof UnknownSessionError) return err(notFound(e.what));
    // An unresolvable path-shaped parent is a MISSING session, not a refusal:
    // the host cannot find what the recorded value points at. The reason
    // carries the raw recorded value verbatim.
    if (e instanceof ParentUnresolvedError) return err(notFound(e.reason));
    if (e instanceof EntryIdMissingError) return err({ tag: "Transport", reason: e.reason });
    return err(transport(e));
  }
}
export function decodeParts(m: AgentMessage): P.TranscriptPart[] {
  const content = (m as { content?: unknown }).content;
  if (typeof content === "string") return [{ tag: "Text", text: content }];
  if (!Array.isArray(content)) return [];
  return content.map((c: { type: string; [k: string]: unknown }): P.TranscriptPart => {
    switch (c.type) {
      case "text": return { tag: "Text", text: String(c.text) };
      case "thinking": return { tag: "Reasoning", text: String(c.thinking) };
      case "toolCall": return { tag: "ToolUse", call: {
        CallId: String(c.id) as P.ToolCallId, Tool: String(c.name), Input: P.canonicalJsonText(JSON.stringify(c.arguments ?? {})), State: "Pending" } };
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

/**
 * Decode one omp session entry into a transcript message.
 *
 * The address IS the omp entry id (`SessionEntryBase.id`): the adapter never
 * synthesises a positional address, because any append would shift the later
 * messages' positions and the address would stop meaning the same message.
 */
export function decodeMessage(m: AgentMessage, id: string): P.TranscriptMessage | undefined {
  const role = decodeRole(m);
  if (role === undefined) return undefined;
  const a = m as { provider?: string; model?: string; timestamp?: number };
  return {
    Id: P.transcriptMessageAddress(id), Role: role,
    Route: a.provider && a.model ? { Provider: a.provider, Model: a.model } : undefined,
    CreatedAt: a.timestamp, Completed: role !== "Assistant" || decodeTerminal(m) !== undefined,
    Terminal: decodeTerminal(m), IsCompaction: false, Parts: decodeParts(m),
  };
}

/**
 * Encode port parts back into omp message content.
 * Returns `undefined` when a part has no omp representation: the caller then
 * leaves the original message untouched rather than silently dropping content.
 */
export function encodeParts(parts: P.TranscriptPart[]): { type: "text"; text: string }[] | undefined {
  const out: { type: "text"; text: string }[] = [];
  for (const p of parts) {
    if (p.tag === "Text") { out.push({ type: "text", text: p.text }); continue; }
    // Reasoning, tool calls and file parts have no plain-text omp encoding here.
    return undefined;
  }
  return out;
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
    Create: (parent, spec) => guard(async () => {
      // The host owns the workspace of the session it creates; a request for a
      // different workspace is refused rather than silently ignored.
      if (spec.Workspace !== undefined && spec.Workspace !== P.workspacePath(ctx().cwd)) {
        throw new Error(`omp cannot create a session in workspace ${spec.Workspace}`);
      }
      return sid(await children.create(parent, spec.Title, spec.Workspace));
    }),
    Parent: (s) => guard(async () => {
      const raw = isOwnChild(s) ? children.parentOf(s) : ctx().sessionManager.getHeader()?.parentSession;
      if (!raw) return undefined;
      // omp records `parentSession` verbatim: its own callers pass a session id
      // in some paths and a session FILE path in others. A path is not a
      // session id, so it is resolved through the host's own lookup; when the
      // host cannot resolve it, the request fails instead of returning a path
      // masquerading as a `SessionId`.
      if (looksLikePath(raw)) {
        const resolved = await children.resolveSessionIdByPath(raw);
        if (!resolved) throw new ParentUnresolvedError(`omp recorded the parent session as a file path that does not resolve to a session id: ${raw}`);
        return sid(resolved);
      }
      return sid(raw);
    }),
    Children: (s) => guard(async () => children.childrenOf(s).map((c) => ({ Id: sid(c.id), Parent: s, Title: c.title }))),
    Abort: (s) => guard(async () => { if (isOwnChild(s)) await children.abort(s); else ctx().abort(); }),
    Interrupt: (s) => guard(async () => { if (isOwnChild(s)) await children.abort(s); else ctx().abort(); }),
    Close: (s) => isOwnChild(s)
      ? guard(() => children.close(s))
      : Promise.resolve(err({ tag: "Refused", reason: "cannot close the host's own session" })),
    FindByTitle: (t) => guard(async () => (await children.findByTitle(t)).map(sid)),
  };

  const prompts: P.IPromptPort = {
    async Send(s, request, late) {
      // The unsupported-field check is scoped to the delivery path actually
      // used, and the two paths are NOT the same:
      //   - `children.prompt(id, text)` takes a TEXT only. It carries no
      //     role, route, workspace or tool policy either, so a child prompt
      //     asking for them is refused rather than silently dropped.
      //   - `pi.sendUserMessage(text, { deliverAs })` is the host's OWN
      //     session; role, route, workspace and tool policy are not
      //     expressible through it.
      // Both refuse, but the reason names the path that cannot carry the
      // field, so a child refusal is never read as a host-session refusal.
      //
      // The check runs BEFORE anything is written to the session log: a
      // prompt that is refused was never sent, so a `"wxs.prompt-key"`
      // entry written for it would be a phantom correlation for a prompt
      // that does not exist.
      const unsupportedFields: string[] = [];
      if (request.Role !== undefined) unsupportedFields.push("Role");
      if (request.Route !== undefined) unsupportedFields.push("Route");
      if (request.Workspace !== undefined) unsupportedFields.push("Workspace");
      if (request.ToolPolicy !== undefined) unsupportedFields.push("ToolPolicy");
      if (unsupportedFields.length > 0) {
        const reason = isOwnChild(s)
          ? `omp child prompts carry text only: cannot carry ${unsupportedFields.join(", ")}`
          : `omp cannot carry ${unsupportedFields.join(", ")}`;
        if (late) await late({ tag: "Refused", reason });
        return { tag: "Fatal", reason };
      }
      // Phase 1 (preparation): everything that happens BEFORE the host is
      // asked to accept the prompt. A failure here means the prompt was never
      // offered to the host, so acceptance is not unknown: it is a definite
      // failure (`Fatal` = definitely not sent).
      try {
        if (request.Correlation !== undefined) pi.appendEntry("wxs.prompt-key", { session: s, key: request.Correlation });
      } catch (e) {
        // Our own correlation write failed: the host never saw the prompt.
        // Reporting this as `AcceptanceUnknown` would hide an adapter bug
        // behind an ambiguous host state.
        return { tag: "Fatal", reason: why(e) };
      }
      // Phase 2 (delivery): the point of no return. The host may already have
      // taken the prompt when this throws, so acceptance is genuinely unknown.
      // The `late` callback is deliberately OUTSIDE this `try`: once the host
      // has accepted the prompt, delivery is settled, and a throw from the
      // caller's own callback cannot make host acceptance unknown. Reporting
      // `AcceptanceUnknown` there would lie about a settled delivery.
      const deliver = async (): Promise<{ tag: "AdmittedWithReceipt"; value: P.TransportReceipt } | { tag: "Retryable"; reason: string }> => {
        if (isOwnChild(s)) {
          // The CHILD path is observable: `children.prompt` resolves to a
          // boolean that says whether the host took the prompt, and it
          // rejects if the call itself failed. A `false` is a refusal, so
          // the prompt was never accepted.
          const accepted = await children.prompt(s, request.Text);
          if (!accepted) return { tag: "Retryable", reason: "omp refused prompt" };
          return { tag: "AdmittedWithReceipt", value: admissionReceipt(s) };
        }
        // The HOST path is NOT observable. `ExtensionAPI.sendUserMessage`
        // returns `void`: omp hands the text to the host session and does
        // not report back. A rejected delivery is swallowed by omp itself
        // (it logs it internally), so the adapter cannot distinguish
        // "enqueued" from "enqueued and then failed". The receipt therefore
        // asserts that the host took the prompt for delivery, NOT that a
        // message was delivered. This limit is stated in the adapter README
        // as well; it is a property of the omp extension API, not of this
        // adapter, and it is not papered over with a synthetic check.
        pi.sendUserMessage(request.Text, { deliverAs: "followUp" });
        return { tag: "AdmittedWithReceipt", value: admissionReceipt(s) };
      };
      let outcome: { tag: "AdmittedWithReceipt"; value: P.TransportReceipt } | { tag: "Retryable"; reason: string };
      try {
        outcome = await deliver();
      } catch (e) {
        return { tag: "AcceptanceUnknown", reason: why(e) };
      }
      // `OwnedSettled` means the send settled with a receipt, so it is only
      // correct for an ADMITTED prompt. A `Retryable` outcome was never
      // accepted (`children.prompt` returned false): announcing it as
      // settled would tell the caller its prompt was delivered when the
      // host refused it.
      if (outcome.tag === "AdmittedWithReceipt" && late) await late({ tag: "OwnedSettled" });
      return outcome;
    },
  };

  const transcript: P.ITranscriptPort = {
    Messages: (s) => guard(async () => {
      // The address of a transcript message IS the omp entry id, so both
      // paths read ENTRIES. The host path reads the host session's own
      // `sessionManager.getEntries()`; the child path reads the CHILD's own
      // session manager (via the child seam), never `session.messages` —
      // `AgentMessage` carries no entry id, so a message-only path could
      // not produce an entry-id address at all.
      const raw = isOwnChild(s)
        ? children.entries(s)
        : ctx().sessionManager.getEntries()
            .flatMap((e) => (e.type === "message" ? [{ id: e.id, message: e.message as AgentMessage }] : []));
      // An unknown session is a MISSING session, not a transport failure:
      // `UnknownSessionError` is what the guard maps to `HostError.NotFound`
      // (the same shape `Abort`/`Close` produce for an unknown session).
      if (raw === undefined) throw new UnknownSessionError(s);
      return raw.flatMap((e, position) => {
        // An entry with no usable id cannot be addressed: the read fails
        // with `HostError.Transport` naming the entry's position, rather
        // than fabricating an address.
        if (typeof e.id !== "string" || e.id.length === 0) throw new EntryIdMissingError(position);
        const d = decodeMessage(e.message, e.id);
        return d ? [d] : [];
      });
    }),
  };

  const models: P.IModelCatalog = {
    Available: () => guard(async () => ctx().modelRegistry.getAvailable().map((m) => ({ Provider: m.provider, Model: m.id }))),
    Apply: (s, route) => guard(async () => {
      // omp can only change the model of the session the extension runs in, so a
      // request for a different session is refused rather than applied to the
      // wrong session.
      if (s !== current()) throw new Error(`omp cannot set the model of session ${s}`);
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
              Session: sid(c.sessionManager.getSessionId()), CallId: toolCallId as P.ToolCallId,
              Workspace: P.workspacePath(c.cwd),
              Args: P.canonicalJsonText(JSON.stringify(params)),
              Cancellation: signal ?? new AbortController().signal,
              Progress: onUpdate ? (line) => onUpdate({ content: [{ type: "text", text: line }], details: undefined }) : undefined,
            });
            return { content: [{ type: "text", text: r.Text }],
                     details: r.Details === undefined ? undefined : JSON.parse(r.Details), isError: r.IsError };
          },
        });
      }
      return ok(undefined);
    },
  };

  const ports: P.HostPorts = {
    Sessions: sessions, Prompts: prompts, Transcript: transcript, Models: models, Tools: tools,
    Signals: { Subscribe: (l) => { listeners.add(l); return { dispose: () => void listeners.delete(l) }; } },
    Paths: { get StateRoot() { return P.workspacePath(`${ctx().cwd}/.wanxiangshu`); },
             get ProjectRoot() { return P.workspacePath(ctx().cwd); },
             get ProjectId() { return ctx().cwd; } },
    Diagnostics: { Emit: (op, fields) => pi.logger.debug(op, Object.fromEntries(fields)) },
    Agents: { Roles: () => agents },
    // Only capabilities this adapter actually wires are advertised.
    Capabilities: { HostName: "omp", Supports: new Set<P.HostCapability>(["ChildSessions", "SessionDiscovery", "Interrupt", "SubagentRouting"]) },
  };
  return { ports, emit };
}

// ---------------------------------------------------------------- ProductHooks -> omp registrations
/**
 * The slot holding the currently active product hooks.
 *
 * The omp handlers are registered exactly ONCE per extension and read the
 * hooks out of this slot on every event, so the handler list never grows
 * (omp's `on` only appends and offers no removal) while the hooks behind
 * it can still be replaced by a later activation.
 */
export interface ProductHookSlot {
  current?: P.ProductHooks;
}

/**
 * The neutral signal fan-out: the host port subscribers first, then the
 * product's own `OnSignal` hook. With no active product the signal is
 * dropped (there is nothing to deliver it to).
 */
export function productSignal(slot: ProductHookSlot, emit: (s: P.HostSignal) => Promise<void>): (s: P.HostSignal) => Promise<void> {
  return (s: P.HostSignal) => {
    const hooks = slot.current;
    if (!hooks) return Promise.resolve();
    return Promise.all([emit(s), hooks.OnSignal(s)]).then(() => undefined);
  };
}

export function registerProductHooks(pi: OmpApi, slot: ProductHookSlot, emit: (s: P.HostSignal) => Promise<void>): void {
  const turnContext = (c: ExtensionContext): P.TurnContext => ({
    Session: sid(c.sessionManager.getSessionId()), Workspace: P.workspacePath(c.cwd),
    Route: c.model ? { Provider: c.model.provider, Model: c.model.id } : undefined,
  });
  const view = (c: ExtensionContext, id: string, tool: string, input: unknown): P.ToolCallView =>
    ({ Session: sid(c.sessionManager.getSessionId()), CallId: id as P.ToolCallId, Tool: tool,
       Args: P.canonicalJsonText(JSON.stringify(input ?? {})) });
  const signal = productSignal(slot, emit);

  pi.on("input", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    const verdict = await hooks.Turn.AdmitTurn({ Context: turnContext(c), Text: e.text });
    if (verdict.tag === "Refuse") return { handled: true };
    // An admitted turn may carry a route; apply it to this session's model.
    if (verdict.route) {
      const model = c.modelRegistry.find(verdict.route.Provider, verdict.route.Model);
      if (!model) return { handled: true };
      if (!(await pi.setModel(model))) return { handled: true };
    }
    return undefined;
  });
  pi.on("before_agent_start", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    return { systemPrompt: await hooks.Turn.SystemPrompt(turnContext(c), e.systemPrompt) };
  });
  pi.on("context", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    const edit = await hooks.Turn.ContextMessages(turnContext(c), e.messages.flatMap((m) => {
      const role = decodeRole(m); return role ? [{ Role: role, Parts: decodeParts(m) }] : [];
    }));
    if (edit.tag === "Keep") return undefined;
    // One stamp for the whole edit: every message this handler produces
    // belongs to the same replacement, so mixed provenance (some stamped,
    // some not) would make the transcript order ambiguous to the product.
    const stamp = Date.now();
    const messages: AgentMessage[] = [];
    for (const m of edit.messages) {
      const content = encodeParts(m.Parts);
      // A message that cannot be represented in omp is left alone: dropping it
      // would silently lose content the product asked to keep.
      if (content === undefined) return undefined;
      messages.push(m.Role === "User"
        ? { role: "user", content, timestamp: stamp } as AgentMessage
        : { role: "assistant", content, timestamp: stamp } as unknown as AgentMessage);
    }
    return { messages };
  });
  pi.on("tool_call", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    const a = await hooks.ToolGate.BeforeTool(view(c, e.toolCallId, e.toolName, e.input));
    if (a.tag === "Refuse") return { block: true, reason: a.reason };
    if (a.tag === "Rewrite") return { input: JSON.parse(a.args) };
    return undefined;
  });
  pi.on("tool_result", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    const text = e.content.flatMap((x) => (x.type === "text" ? [x.text] : [])).join("\n");
    const edit = await hooks.ToolGate.AfterTool(view(c, e.toolCallId, e.toolName, e.input),
      { Text: text, Truncated: false, IsError: e.isError });
    if (edit.tag === "Keep") return undefined;
    return { content: [{ type: "text", text: edit.result.Text }], isError: edit.result.IsError };
  });
  pi.on("session.compacting", async (_e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    // The event's `sessionId` is omp's *provider* session id, which is not
    // necessarily the persisted one. The neutral `SessionId` is always the
    // persisted id (`sessionManager.getSessionId()`), the one `parentSession`
    // and the session listings refer to.
    const r = await hooks.Compaction.OnCompacting(sid(c.sessionManager.getSessionId()));
    return r.tag === "Context" ? { context: r.lines } : r.tag === "ReplacePrompt" ? { prompt: r.prompt } : undefined;
  });
  pi.on("session_stop", async (_e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    // Same as above: the event's `session_id` is the provider session id.
    const v = await hooks.Compaction.AutoContinue(sid(c.sessionManager.getSessionId()));
    return v.tag === "Deny" ? { continue: false, reason: v.reason } : undefined;
  });
  // `before_subagent_spawn` is ALWAYS registered: the adapter advertises the
  // `SubagentRouting` capability, so the host must be able to reach the
  // product's routing decision even when the product later drops the hook.
  // Without a hook there is nothing to route, so the spawn is allowed through
  // unchanged (`undefined`).
  pi.on("before_subagent_spawn", async (e, c) => {
    const hooks = slot.current;
    if (!hooks) return undefined;
    const spawn = hooks.Spawn;
    if (!spawn) return undefined;
    const r = await spawn({ Parent: sid(c.sessionManager.getSessionId()), Role: P.agentRole(e.agent), SpawnKey: e.spawnKey,
      Candidates: e.patterns.flatMap((p) => { const [prov, ...rest] = p.split("/"); return rest.length ? [{ Provider: prov, Model: rest.join("/") }] : []; }) });
    if (r.tag === "Refuse") return { block: true, reason: r.reason };
    if (r.tag === "Route") return { model: r.routes.map((x) => `${x.Provider}/${x.Model}`) };
    return undefined;
  });
  pi.on("turn_start", async (_e, c) => signal({ tag: "AttemptStarted", attempt: { Session: sid(c.sessionManager.getSessionId()) } }));
  pi.on("turn_end", async (e, c) => {
    const t = decodeTerminal(e.message);
    if (t) await signal({ tag: "AttemptFinished", attempt: { Session: sid(c.sessionManager.getSessionId()) }, terminal: t });
  });
  pi.on("agent_end", async (e, c) => { if (!e.willContinue && c.isIdle()) await signal({ tag: "SessionIdle", session: sid(c.sessionManager.getSessionId()) }); });
  pi.on("auto_retry_start", async (e, c) => signal({ tag: "ProviderRetry", notice: {
    Session: sid(c.sessionManager.getSessionId()), Attempt: e.attempt, Failure: decodeFailure(e.errorMessage, false) } }));
  pi.on("tool_execution_start", async (e, c) => signal({ tag: "ToolStarted", call: { Session: sid(c.sessionManager.getSessionId()), CallId: e.toolCallId as P.ToolCallId, Tool: e.toolName } }));
  // Disposal happens EXACTLY ONCE per activation: the slot is emptied first,
  // so a second `session_shutdown` finds no hooks and disposes nothing.
  pi.on("session_shutdown", async () => {
    const hooks = slot.current;
    if (!hooks) return;
    slot.current = undefined;
    await hooks.Dispose();
  });
}

/** omp extension entry: `omp -e <bundle>` loads `export default (pi) => …`. */
export function activateOnOmp(pi: OmpApi, plugin: P.IHostPlugin, children: OmpChildSessions, agents: P.AgentRoleSpec[]) {
  let latest: ExtensionContext | undefined;
  const ctx = () => { if (!latest) throw new Error("omp context not yet available"); return latest; };
  const { ports, emit } = createOmpHostPorts(pi, ctx, children, agents);
  // The activation latch (O-3 rule 6). The product is activated ONCE per
  // session lifetime and the omp handlers are registered ONCE per extension.
  // omp's `ExtensionAPI.on` only APPENDS to the extension's handler list
  // (`loader.ts:210-214`) and offers no removal, so re-registering on a
  // later `session_start` would make every product hook fire twice per
  // event. The handlers therefore read the live hooks from `slot`, and the
  // slot is emptied by `session_shutdown` so that a later `session_start`
  // activates a FRESH product without registering anything again.
  const slot: ProductHookSlot = {};
  let registered = false;
  const signal = productSignal(slot, emit);
  pi.on("session_start", async (_e, c) => {
    latest = c;
    // The handlers are registered exactly ONCE per extension: they read the
    // live hooks out of `slot`, so a later activation after a shutdown
    // re-uses them instead of appending a second copy.
    if (!registered) {
      registered = true;
      registerProductHooks(pi, slot, emit);
    }
    if (!slot.current) {
      const r = await plugin.Activate(ports);
      if (!r.ok) { pi.logger.error("wanxiangshu activation failed", { error: r.error }); return; }
      slot.current = r.value;
    }
    // The `SessionCreated` signal is emitted HERE, not from a `session_start`
    // listener registered by `registerProductHooks`: omp snapshots the
    // listener list when it starts emitting `session_start`, so a listener
    // registered during that emission is never called for this session.
    // A session was really created, so the signal is emitted for EVERY
    // `session_start`; only the activation is latched.
    await signal({ tag: "SessionCreated", info: { Id: sid(c.sessionManager.getSessionId()) } });
  });
}

export { unsupported, notFound };
