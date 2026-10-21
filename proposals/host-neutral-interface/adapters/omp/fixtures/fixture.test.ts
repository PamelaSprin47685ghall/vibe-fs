// Offline conformance fixture (HNI-03): a fake omp ExtensionAPI drives the adapter with recorded-shape
// events; a fake product plugin answers. No model, no network, no omp process.
import type { CreateAgentSessionResult } from "@oh-my-pi/pi-coding-agent/sdk";
import type { SessionManager } from "@oh-my-pi/pi-coding-agent/session/session-manager";
import type { ExtensionAPI, ExtensionContext } from "@oh-my-pi/pi-coding-agent/extensibility/extensions/types";
import { activateOnOmp, type AgentMessage, type OmpApi, type OmpChildSessions } from "../adapter";
import { sdkChildSessions } from "../sdk-children";
import * as P from "../ports";
import events from "./events.json" with { type: "json" };

type Handler = (event: any, ctx: ExtensionContext) => unknown;
const handlers = new Map<string, Handler[]>();
const tools: { name: string; execute: Function }[] = [];
const sent: string[] = [];
const entries: unknown[] = [];
const on = ((event: string, handler: Handler) => { handlers.set(event, [...(handlers.get(event) ?? []), handler]); }) as ExtensionAPI["on"];
// Fault injection for the two prompt-port phases: the correlation write is
// part of the PREPARATION phase (the host never saw the prompt), the user
// message is the DELIVERY phase (acceptance becomes unknown).
let sendFault: Error | undefined;
let appendFault: Error | undefined;
// Fault injection for the CHILD delivery path: `children.prompt` resolves
// `false` when the host refuses the prompt (the prompt was never accepted).
let childPromptRefused = false;
const pi: OmpApi = {
  on,
  registerTool: ((t: { name: string; execute: Function }) => { tools.push(t); }) as ExtensionAPI["registerTool"],
  sendUserMessage: (text) => { if (sendFault) throw sendFault; sent.push(String(text)); },
  appendEntry: (_type, data) => { if (appendFault) throw appendFault; entries.push(data); },
  setModel: async () => true,
  setActiveTools: async () => {},
  logger: { debug() {}, info() {}, warn() {}, error() {} } as unknown as ExtensionAPI["logger"],
};
// The recorded omp session ENTRIES: a transcript address IS the entry id, so
// the fake host session exposes entries (with ids), not bare messages.
type FakeEntry = { type: "message"; id: string; message: AgentMessage };
const entryList: FakeEntry[] = (events.history as unknown as { id: string }[] & Record<string, unknown>[]).map((h) => {
  const { id, ...message } = h as { id: string } & Record<string, unknown>;
  return { type: "message", id, message: message as unknown as AgentMessage };
});
// The child's own entries: a child transcript is addressed by entry ids too.
const childEntries: { id: string; message: AgentMessage }[] = [
  { id: "c-1", message: { role: "user", content: "child hello", timestamp: 7 } as unknown as AgentMessage },
  { id: "c-2", message: { role: "assistant", content: "child ok", stopReason: "stop", timestamp: 8 } as unknown as AgentMessage },
];
let recordedParentSession: string | undefined;
// The session id the product sees for the compaction hooks; the omp events
// carry a *provider* session id, so this asserts which id the adapter passes
// on (it must be the persisted one).
let compactionSession: string | undefined;
const ctx = {
  cwd: "/tmp/fixture", model: { provider: "fx", id: "m1" }, isIdle: () => true, abort() {},
  sessionManager: { getSessionId: () => "root", getHeader: () => ({ parentSession: recordedParentSession }),
                    getEntries: () => entryList },
  modelRegistry: { getAvailable: () => [{ provider: "fx", id: "m1" }], find: (p: string, m: string) => (p === "fx" && m === "m1" ? { provider: p, id: m } : undefined) },
} as unknown as ExtensionContext;
const children: OmpChildSessions = {
  create: async (parent) => `child-of-${parent}`, prompt: async () => !childPromptRefused, abort: async () => {}, close: async () => {},
  parentOf: (s) => (s.startsWith("child-of-") ? s.slice(9) : undefined), childrenOf: () => [], findByTitle: async () => [],
  entries: (s) => (s.startsWith("child-of-") ? childEntries : undefined),
  resolveSessionIdByPath: async (path) => (path === "/tmp/sessions/parent.jsonl" ? "parent-session-id" : undefined),
};

const signals: P.HostSignal[] = [];
let portsSeen: P.HostPorts | undefined;
const product: P.IHostPlugin = {
  async Activate(ports) {
    portsSeen = ports;
    ports.Tools.Register([{ Name: "wxs_echo", Description: "echo", Schema: P.jsonSchema(JSON.stringify({ type: "object" })),
      Execute: async (call) => ({ Text: `echo ${call.Args}`, Truncated: false, IsError: false }) }]);
    return { ok: true, value: {
      ToolGate: {
        BeforeTool: async (c) => (c.Tool === "bash" ? { tag: "Refuse", reason: "product says no" } : { tag: "Allow" }),
        AfterTool: async (_c, r) => (r.Text.includes("SECRET") ? { tag: "Replace", result: { ...r, Text: "[redacted]" } } : { tag: "Keep" }),
        DefineTool: () => ({ tag: "Keep" }),
      },
      Turn: {
        AdmitTurn: async (r) => (r.Text === "forbidden" ? { tag: "Refuse", reason: "no" } : { tag: "Admit" }),
        SystemPrompt: async (_c, s) => [...s, "PRODUCT-SYSTEM"],
        ContextMessages: async (_c, msgs) => (msgs.some((m) => m.Parts.some((p) => p.tag === "Text" && p.text === "REPLACE-ME"))
          ? { tag: "Replace", messages: [
              { Role: "User", Parts: [{ tag: "Text", text: "synth-user" }] },
              { Role: "Assistant", Parts: [{ tag: "Text", text: "synth-assistant" }] }] }
          : { tag: "Keep" }),
        ProviderParams: async () => ({ tag: "Accept" }),
      },
      Compaction: { OnCompacting: async (s) => { compactionSession = s; return { tag: "Context", lines: ["keep ledger"] }; },
                    AutoContinue: async (s) => { compactionSession = s; return { tag: "Deny", reason: "product owns continuation" }; } },
      OnSignal: async (s) => { signals.push(s); },
      Dispose: async () => {},
    } };
  },
};

const fire = async (name: string, event: unknown) => {
  // omp snapshots the listener list per event pass: a listener registered
  // DURING the emission of an event is not called for that pass.
  let last: unknown;
  for (const h of [...(handlers.get(name) ?? [])]) last = await h(event, ctx);
  return last;
};
let failures = 0;
const expect = (label: string, actual: unknown, expected: unknown) => {
  const a = JSON.stringify(actual), e = JSON.stringify(expected);
  if (a !== e) { failures++; console.log(`not ok - ${label}\n  actual:   ${a}\n  expected: ${e}`); } else console.log(`ok - ${label}`);
};

activateOnOmp(pi, product, children, [{ Role: P.agentRole("coder") }]);
await fire("session_start", { type: "session_start" });
expect("product tool registered", tools.map((t) => t.name), ["wxs_echo"]);
expect("tool execute encodes ToolOutcome", await tools[0].execute("call-1", { a: 1 }, undefined, undefined, ctx),
  { content: [{ type: "text", text: 'echo {"a":1}' }], isError: false });
expect("SessionCreated is delivered for the first session", signals[0], { tag: "SessionCreated", info: { Id: "root" } });
expect("before_subagent_spawn is registered even without a product Spawn hook",
  [handlers.has("before_subagent_spawn"), await fire("before_subagent_spawn", { type: "before_subagent_spawn", agent: "coder", spawnKey: "sk-1", patterns: [] })],
  [true, undefined]);
appendFault = new Error("appendEntry exploded");
expect("a failure in the preparation phase is Fatal, with the original message",
  await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go", Correlation: P.promptKey("pk-2") }, undefined),
  { tag: "Fatal", reason: "appendEntry exploded" });
appendFault = undefined;
sendFault = new Error("host refused the message");
expect("a failure in the delivery phase is AcceptanceUnknown, with the original message",
  await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go" }, undefined),
  { tag: "AcceptanceUnknown", reason: "host refused the message" });
sendFault = undefined;
// A child prompt must NOT be refused for a field the child path cannot
// carry: `children.prompt` takes text only, so `Role` on a child is refused
// for the CHILD path's reason, not the host session's. The child prompt is
// delivered, not refused.
expect("a child prompt carrying Role is refused with the child path's reason, not the host's",
  await portsSeen!.Prompts.Send("child-of-root" as P.SessionId, { Text: "go", Role: P.agentRole("coder") }, undefined),
  { tag: "Fatal", reason: "omp child prompts carry text only: cannot carry Role" });
// A refused prompt was never sent, so it must not leave a phantom
// correlation entry in the session log. The unsupported-field check runs
// BEFORE the correlation write.
expect("a prompt refused for an unsupported field does not append a correlation entry",
  [await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go", Role: P.agentRole("coder"), Correlation: P.promptKey("pk-refused") }, undefined), entries],
  [{ tag: "Fatal", reason: "omp cannot carry Role" }, []]);
// `children.prompt` resolving false means the host REFUSED the prompt: it
// was never accepted. The outcome is `Retryable`, and the `late` callback
// must NOT be told the send settled.
childPromptRefused = true;
const lateVerdicts: P.DeliveryVerdict[] = [];
expect("a child prompt the host refused is Retryable",
  await portsSeen!.Prompts.Send("child-of-root" as P.SessionId, { Text: "go" }, async (v) => { lateVerdicts.push(v); }),
  { tag: "Retryable", reason: "omp refused prompt" });
expect("a refused child prompt does not fire late({ OwnedSettled })", lateVerdicts, []);
childPromptRefused = false;
const ctxEdit = await fire("context", { type: "context", messages: [{ role: "user", content: [{ type: "text", text: "REPLACE-ME" }], timestamp: 1 }] });
const stamps = (ctxEdit as { messages: { timestamp?: number }[] }).messages.map((m) => m.timestamp);
expect("both synthesized messages carry the same numeric timestamp",
  [stamps.length, stamps.every((t) => typeof t === "number"), new Set(stamps).size],
  [2, true, 1]);
expect("tool_call refuse -> block", await fire("tool_call", events.toolCallBash), { block: true, reason: "product says no" });
expect("tool_call allow -> undefined", await fire("tool_call", events.toolCallRead), undefined);
expect("tool_result replace", await fire("tool_result", events.toolResultSecret), { content: [{ type: "text", text: "[redacted]" }], isError: false });
expect("input refuse -> handled", await fire("input", { type: "input", text: "forbidden", source: "interactive" }), { handled: true });
expect("system prompt append", await fire("before_agent_start", { type: "before_agent_start", prompt: "hi", systemPrompt: ["base"] }), { systemPrompt: ["base", "PRODUCT-SYSTEM"] });
expect("compacting context", await fire("session.compacting", { type: "session.compacting", sessionId: "provider-session-id", messages: [] }), { context: ["keep ledger"] });
expect("compacting passes the persisted session id, not the provider one", compactionSession, "root");
expect("auto-continue deny", await fire("session_stop", { ...events.sessionStop, session_id: "provider-session-id" }), { continue: false, reason: "product owns continuation" });
expect("auto-continue passes the persisted session id, not the provider one", compactionSession, "root");
await fire("turn_end", events.turnEndError);
expect("turn_end error -> AttemptFinished Failed", signals.at(-1), { tag: "AttemptFinished", attempt: { Session: "root" },
  terminal: { tag: "Failed", failure: { Kind: "Provider", Message: "overloaded" } } });
const tr = await portsSeen!.Transcript.Messages("root" as P.SessionId);
expect("transcript decode", tr.ok ? tr.value.map((m) => [m.Role, m.Parts.map((p) => p.tag), m.Terminal?.tag]) : tr,
  [["User", ["Text"], null], ["Assistant", ["Reasoning", "Text", "ToolUse"], "Completed"]]);
// O-1: a transcript address IS the omp entry id, not a positional id.
expect("transcript addresses are the omp entry ids",
  tr.ok ? tr.value.map((m) => m.Id) : tr, ["e-1", "e-2"]);
// O-1 acceptance 1: appending an entry must not move the earlier
// addresses. A positional id would shift them.
const firstTwo = tr.ok ? tr.value.map((m) => m.Id) : [];
entryList.push({ type: "message", id: "e-3", message: { role: "user", content: "again", timestamp: 3 } as unknown as AgentMessage });
const tr2 = await portsSeen!.Transcript.Messages("root" as P.SessionId);
expect("appending an entry leaves the earlier addresses unchanged",
  tr2.ok ? tr2.value.slice(0, 2).map((m) => m.Id) : tr2, firstTwo);
expect("the appended entry is addressed by its own id",
  tr2.ok ? tr2.value.map((m) => m.Id) : tr2, ["e-1", "e-2", "e-3"]);
// O-1 acceptance 2: an entry with no usable id fails the read with a typed
// `HostError.Transport` naming the entry's position — never a fabricated
// address.
entryList.push({ type: "message", id: "", message: { role: "user", content: "no id", timestamp: 4 } as unknown as AgentMessage });
const trBad = await portsSeen!.Transcript.Messages("root" as P.SessionId);
expect("an entry without an id fails the read with Transport, naming the position",
  trBad.ok ? trBad.value : trBad,
  { ok: false, error: { tag: "Transport", reason: "omp session entry at position 3 has no id" } });
entryList.pop();
entryList.pop();
// O-1 acceptance 4: a CHILD session's transcript is addressed by entry ids
// too (the child's own session manager entries).
const ctr = await portsSeen!.Transcript.Messages("child-of-root" as P.SessionId);
expect("a child session's transcript addresses are entry ids",
  ctr.ok ? ctr.value.map((m) => m.Id) : ctr, ["c-1", "c-2"]);
expect("child session create", await portsSeen!.Sessions.Create("root" as P.SessionId, { Title: "t" }), { ok: true, value: "child-of-root" });
expect("model apply unknown -> Transport error", (await portsSeen!.Models.Apply("root" as P.SessionId, { Provider: "x", Model: "y" })).ok, false);
expect("prompt correlation echoed as entry", [await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go", Correlation: P.promptKey("pk-1") }, undefined), entries, sent],
  [{ tag: "AdmittedWithReceipt", value: "accepted-omp-root" }, [{ session: "root", key: "pk-1" }], ["go"]]);
// The transport receipt must be ADMISSION-shaped, i.e. it must look like an
// `accepted-*` admission receipt. The F# contract says so in
// `src/Wanxiangshu/Foundation/Identity.fs` ("Host admission ids are
// `accepted-*`"), and every host the product itself drives emits that shape
// (`accepted-<session>` for OpenCode, `accepted-detached-<promptKey>` for a
// detached send). A receipt that does not look like one means the Host
// returned something else and the caller must decide, so the adapter must
// not invent a private `omp:` namespace.
const admissionShaped = await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go" }, undefined);
expect("the transport receipt is admission-shaped",
  admissionShaped.tag === "AdmittedWithReceipt" && String(admissionShaped.value).startsWith("accepted-"), true);
expect("the admission receipt names the session it was issued for",
  admissionShaped.tag === "AdmittedWithReceipt" ? String(admissionShaped.value) : "", "accepted-omp-root");
// The `late` callback runs AFTER delivery settled. A throw from the caller's
// own callback must not be reported as `AcceptanceUnknown`: the host already
// accepted the prompt, so acceptance is NOT unknown. The throw propagates to
// the caller unchanged instead of being mapped to a delivery verdict.
let lateThrew = false;
const lateOutcome = await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go" }, async () => { lateThrew = true; throw new Error("late exploded"); })
  .then((r) => ({ tag: "resolved", outcome: r }), (e) => ({ tag: "rejected", reason: e instanceof Error ? e.message : String(e) }));
expect("a throw from the late callback after a settled delivery is not mapped to AcceptanceUnknown",
  lateOutcome, { tag: "rejected", reason: "late exploded" });
expect("the late callback did throw, so the assertion above really exercised the path", lateThrew, true);
recordedParentSession = "parent-session-id";
expect("parent recorded as a session id", await portsSeen!.Sessions.Parent("root" as P.SessionId), { ok: true, value: "parent-session-id" });
recordedParentSession = "/tmp/sessions/parent.jsonl";
expect("parent recorded as a file path resolves to the session id",
  await portsSeen!.Sessions.Parent("root" as P.SessionId), { ok: true, value: "parent-session-id" });
recordedParentSession = "/tmp/sessions/gone.jsonl";
// O-4: an unresolvable path-shaped parent is a MISSING session, not a
// refusal, and the reason carries the raw recorded value verbatim.
expect("parent recorded as an unresolvable file path is NotFound, with the raw path",
  await portsSeen!.Sessions.Parent("root" as P.SessionId),
  { ok: false, error: { tag: "NotFound", what: "omp recorded the parent session as a file path that does not resolve to a session id: /tmp/sessions/gone.jsonl" } });
recordedParentSession = undefined;

// The neutral `SessionId` of a child is the PERSISTED id, not omp's provider
// session id: `AgentSession.sessionId` prefers a fresh or inherited provider
// id, so it can differ from the id the session file, `parentSession` and the
// session listings use.
// The stub is typed, not `any`: the fake session manager is the only part
// `sdk-children.ts` reads besides the session's own methods.
const fakeSessionManager = {
  getSessionId: () => "persisted-session-id",
  setSessionName: async (_name: string) => {},
  // The child's own entries: a child transcript is addressed by entry ids,
  // read from the child's own session manager.
  getEntries: () => [
    { type: "message", id: "c-1", message: { role: "user", content: "child hello", timestamp: 7 } },
    { type: "message", id: "c-2", message: { role: "assistant", content: "child ok", stopReason: "stop", timestamp: 8 } },
  ],
};
const fakeAgentSession = {
  sessionId: "provider-session-id",
  sessionManager: fakeSessionManager,
  prompt: async (_text: string) => true,
  abort: async () => {},
  dispose: async () => {},
};
const sdkChildren = sdkChildSessions(
  async () => ({ session: fakeAgentSession } as unknown as CreateAgentSessionResult),
  () => ({ newSession: async () => {} } as unknown as SessionManager),
  async () => [],
  async (file) => (file === "/tmp/sessions/parent.jsonl" ? { id: "parent-session-id" } : undefined),
  "/tmp/fixture",
);
expect("child session id is the persisted id, not the provider one",
  await sdkChildren.create("root", "t", "/tmp/fixture"), "persisted-session-id");
expect("the provider session id is not registered as the child",
  await sdkChildren.parentOf("provider-session-id"), undefined);
// The real child seam reads the CHILD's own session manager entries, so the
// addresses it hands back are entry ids — never `session.messages`.
expect("the sdk child seam exposes entries with entry ids",
  sdkChildren.entries("persisted-session-id")?.map((e) => e.id), ["c-1", "c-2"]);
expect("an unknown child session has no entries",
  sdkChildren.entries("no-such-session"), undefined);
console.log(failures === 0 ? "# fixture: all ok" : `# fixture: ${failures} failed`);
if (failures) process.exit(1);
