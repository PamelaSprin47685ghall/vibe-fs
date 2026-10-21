// Offline conformance fixture (HNI-03): a fake omp ExtensionAPI drives the adapter with recorded-shape
// events; a fake product plugin answers. No model, no network, no omp process.
import type { ExtensionAPI, ExtensionContext } from "@oh-my-pi/pi-coding-agent/extensibility/extensions/types";
import { activateOnOmp, type AgentMessage, type OmpApi, type OmpChildSessions } from "../adapter";
import * as P from "../ports";
import events from "./events.json" with { type: "json" };

type Handler = (event: any, ctx: ExtensionContext) => unknown;
const handlers = new Map<string, Handler[]>();
const tools: { name: string; execute: Function }[] = [];
const sent: string[] = [];
const entries: unknown[] = [];
const on = ((event: string, handler: Handler) => { handlers.set(event, [...(handlers.get(event) ?? []), handler]); }) as ExtensionAPI["on"];
const pi: OmpApi = {
  on,
  registerTool: ((t: { name: string; execute: Function }) => { tools.push(t); }) as ExtensionAPI["registerTool"],
  sendUserMessage: (text) => { sent.push(String(text)); },
  appendEntry: (_type, data) => { entries.push(data); },
  setModel: async () => true,
  setActiveTools: async () => {},
  logger: { debug() {}, info() {}, warn() {}, error() {} } as unknown as ExtensionAPI["logger"],
};
const history: AgentMessage[] = events.history as unknown as AgentMessage[];
const ctx = {
  cwd: "/tmp/fixture", model: { provider: "fx", id: "m1" }, isIdle: () => true, abort() {},
  sessionManager: { getSessionId: () => "root", getHeader: () => ({ parentSession: undefined }),
                    getEntries: () => history.map((message) => ({ type: "message", message })) },
  modelRegistry: { getAvailable: () => [{ provider: "fx", id: "m1" }], find: (p: string, m: string) => (p === "fx" && m === "m1" ? { provider: p, id: m } : undefined) },
} as unknown as ExtensionContext;
const children: OmpChildSessions = {
  create: async (parent) => `child-of-${parent}`, prompt: async () => true, abort: async () => {}, close: async () => {},
  parentOf: (s) => (s.startsWith("child-of-") ? s.slice(9) : undefined), childrenOf: () => [], findByTitle: async () => [],
  messages: () => [],
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
        ContextMessages: async () => ({ tag: "Keep" }),
        ProviderParams: async () => ({ tag: "Accept" }),
      },
      Compaction: { OnCompacting: async () => ({ tag: "Context", lines: ["keep ledger"] }), AutoContinue: async () => ({ tag: "Deny", reason: "product owns continuation" }) },
      OnSignal: async (s) => { signals.push(s); },
      Dispose: async () => {},
    } };
  },
};

const fire = async (name: string, event: unknown) => {
  let last: unknown;
  for (const h of handlers.get(name) ?? []) last = await h(event, ctx);
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
expect("tool_call refuse -> block", await fire("tool_call", events.toolCallBash), { block: true, reason: "product says no" });
expect("tool_call allow -> undefined", await fire("tool_call", events.toolCallRead), undefined);
expect("tool_result replace", await fire("tool_result", events.toolResultSecret), { content: [{ type: "text", text: "[redacted]" }], isError: false });
expect("input refuse -> handled", await fire("input", { type: "input", text: "forbidden", source: "interactive" }), { handled: true });
expect("system prompt append", await fire("before_agent_start", { type: "before_agent_start", prompt: "hi", systemPrompt: ["base"] }), { systemPrompt: ["base", "PRODUCT-SYSTEM"] });
expect("compacting context", await fire("session.compacting", { type: "session.compacting", sessionId: "root", messages: [] }), { context: ["keep ledger"] });
expect("auto-continue deny", await fire("session_stop", events.sessionStop), { continue: false, reason: "product owns continuation" });
await fire("turn_end", events.turnEndError);
expect("turn_end error -> AttemptFinished Failed", signals.at(-1), { tag: "AttemptFinished", attempt: { Session: "root" },
  terminal: { tag: "Failed", failure: { Kind: "Provider", Message: "overloaded" } } });
const tr = await portsSeen!.Transcript.Messages("root" as P.SessionId);
expect("transcript decode", tr.ok ? tr.value.map((m) => [m.Role, m.Parts.map((p) => p.tag), m.Terminal?.tag]) : tr,
  [["User", ["Text"], null], ["Assistant", ["Reasoning", "Text", "ToolUse"], "Completed"]]);
expect("child session create", await portsSeen!.Sessions.Create("root" as P.SessionId, { Title: "t" }), { ok: true, value: "child-of-root" });
expect("model apply unknown -> Transport error", (await portsSeen!.Models.Apply("root" as P.SessionId, { Provider: "x", Model: "y" })).ok, false);
expect("prompt correlation echoed as entry", [await portsSeen!.Prompts.Send("root" as P.SessionId, { Text: "go", Correlation: P.promptKey("pk-1") }, undefined), entries, sent],
  [{ tag: "AdmittedWithReceipt", value: "omp:root" }, [{ session: "root", key: "pk-1" }], ["go"]]);
console.log(failures === 0 ? "# fixture: all ok" : `# fixture: ${failures} failed`);
if (failures) process.exit(1);
