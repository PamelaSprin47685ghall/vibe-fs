// Binds OmpChildSessions to the omp SDK (`createAgentSession`, `SessionManager`) — type-checked only (HNI-03).
// The id map below is wire bookkeeping (which omp AgentSession object backs which session id), not product state.
import type { CreateAgentSessionOptions, CreateAgentSessionResult } from "@oh-my-pi/pi-coding-agent/sdk";
import type { SessionManager } from "@oh-my-pi/pi-coding-agent/session/session-manager";
import type { AgentMessage, OmpChildSessions } from "./adapter";

type AgentSession = CreateAgentSessionResult["session"];

export function sdkChildSessions(
  createAgentSession: (o: CreateAgentSessionOptions) => Promise<CreateAgentSessionResult>,
  createManager: (cwd: string) => SessionManager,
  listSessions: (cwd: string) => Promise<{ id: string; title?: string }[]>,
  defaultCwd: string,
): OmpChildSessions {
  const live = new Map<string, { session: AgentSession; parent?: string; title?: string }>();
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
    prompt: async (id, text) => { const s = live.get(id); if (!s) throw new Error(`unknown child ${id}`); return s.session.prompt(text); },
    abort: async (id) => { await live.get(id)?.session.abort(); },
    close: async (id) => { const s = live.get(id); live.delete(id); await s?.session.dispose(); },
    parentOf: (id) => live.get(id)?.parent,
    childrenOf: (parent) => [...live].filter(([, v]) => v.parent === parent).map(([id, v]) => ({ id, title: v.title })),
    findByTitle: async (title) => (await listSessions(defaultCwd)).filter((s) => s.title === title).map((s) => s.id),
    messages: (id) => live.get(id)?.session.messages as AgentMessage[] | undefined,
  };
}
