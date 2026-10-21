// Binds OmpChildSessions to the omp SDK (`createAgentSession`, `SessionManager`) — type-checked only (HNI-03).
// The id map below is wire bookkeeping (which omp AgentSession object backs which session id), not product state.
//
// Unknown ids are errors, not silent no-ops: `abort`/`close` on an unknown id raise
// `UnknownSessionError`, which the adapter maps to the typed `HostError.NotFound`.
// A mutation that reports success for a session that does not exist would let a
// caller believe a child was stopped when it never was.
import type { CreateAgentSessionOptions, CreateAgentSessionResult } from "@oh-my-pi/pi-coding-agent/sdk";
import type { SessionManager } from "@oh-my-pi/pi-coding-agent/session/session-manager";
import { readSessionInfo } from "@oh-my-pi/pi-coding-agent/session/session-listing";
import { UnknownSessionError, type AgentMessage, type OmpChildSessions } from "./adapter";

type AgentSession = CreateAgentSessionResult["session"];

export function sdkChildSessions(
  createAgentSession: (o: CreateAgentSessionOptions) => Promise<CreateAgentSessionResult>,
  createManager: (cwd: string) => SessionManager,
  listSessions: (cwd: string) => Promise<{ id: string; title?: string }[]>,
  defaultCwd: string,
): OmpChildSessions {
  const live = new Map<string, { session: AgentSession; parent?: string; title?: string }>();
  const require_ = (id: string) => {
    const entry = live.get(id);
    if (!entry) throw new UnknownSessionError(id);
    return entry;
  };
  return {
    async create(parent, title, cwd) {
      const dir = cwd ?? defaultCwd;
      const sessionManager = createManager(dir);
      await sessionManager.newSession({ parentSession: parent });
      const { session } = await createAgentSession({ cwd: dir, sessionManager });
      if (title) await session.sessionManager.setSessionName?.(title);
      live.set(session.sessionId, { session, parent, title });
      return session.sessionId;
    },
    prompt: async (id, text) => require_(id).session.prompt(text),
    abort: async (id) => { await require_(id).session.abort(); },
    close: async (id) => { const entry = require_(id); live.delete(id); await entry.session.dispose(); },
    parentOf: (id) => live.get(id)?.parent,
    childrenOf: (parent) => [...live].filter(([, v]) => v.parent === parent).map(([id, v]) => ({ id, title: v.title })),
    findByTitle: async (title) => (await listSessions(defaultCwd)).filter((s) => s.title === title).map((s) => s.id),
    // omp's own callers record the parent session either as a session id or as
    // a session FILE path. A path is resolved through the host's session
    // listing (which knows the id of a session file); when the file cannot be
    // read, the value is not guessable and the caller is told so.
    resolveSessionIdByPath: async (path) => {
      const info = await readSessionInfo(path);
      return info?.id;
    },
    messages: (id) => live.get(id)?.session.messages as AgentMessage[] | undefined,
  };
}
