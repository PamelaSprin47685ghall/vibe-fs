/**
 * strict-mock-matches.js — Body matchers and synthetic/title detection
 * for StrictMockProvider. Pure functions, no I/O.
 */

export const NUDGE_MARKERS = [
  'There are still incomplete todos. Continue working through the remaining items.',
  'You are in loop mode. You must call the submit_review tool',
  'A background runner task is still active',
  'command: with-review',
  'the system context is about to be suspended',
  'You must immediately force an emergency stop to all work',
];

const TITLE_GENERATION_MARKER = 'Generate a title for this conversation:';

function isZWSPContent(text) {
  if (typeof text !== 'string') return false;
  if (text === '') return true;
  return text.replace(/[\u200B\u200C\u200D\uFEFF]/g, '').trim() === '';
}

function matchMarker(text) {
  if (isZWSPContent(text)) return 'zwsp';
  if (text.includes('There are still incomplete todos')) return 'todo-nudge';
  if (text.includes('command: with-review')) return 'loop-nudge';
  if (text.includes('You are in loop mode. You must call the submit_review tool')) return 'loop-nudge';
  if (text.includes('A background runner task is still active')) return 'runner-nudge';
  if (text.includes('the system context is about to be suspended')) return 'budget-nudge';
  if (text.includes('You must immediately force an emergency stop to all work')) return 'budget-nudge';
  return null;
}

function extractTextsFromContent(content) {
  if (typeof content === 'string') return [content];
  if (Array.isArray(content)) {
    return content.map((p) => p?.text).filter((t) => typeof t === 'string');
  }
  return [];
}

export function isSyntheticContinuation(body) {
  const msgs = body?.messages || [];
  if (msgs.length === 0) return false;
  const lastUserIndex = msgs.map((m) => m?.role).lastIndexOf('user');
  if (lastUserIndex === -1 || lastUserIndex < msgs.length - 2) return false;
  const last = msgs[lastUserIndex];
  const texts = extractTextsFromContent(last.content);
  if (texts.length === 0) return false;
  return texts.some((t) => matchMarker(t) !== null);
}

export function detectSyntheticMarker(body) {
  const msgs = body?.messages || [];
  if (msgs.length === 0) return 'unknown';
  const lastUserIndex = msgs.map((m) => m?.role).lastIndexOf('user');
  if (lastUserIndex === -1 || lastUserIndex < msgs.length - 2) return 'unknown';
  const last = msgs[lastUserIndex];
  const texts = extractTextsFromContent(last.content);
  for (const t of texts) {
    const m = matchMarker(t);
    if (m) return m;
  }
  return 'unknown';
}

export function isTitleGenerationRequest(body) {
  const messages = body?.messages || [];
  const systemPrompt = messages.find((message) => message?.role === 'system');
  const systemTexts = extractTextsFromContent(systemPrompt?.content);
  if (systemTexts.some((text) => text.includes('You are a title generator') || text.includes('Generate a brief title'))) {
    return true;
  }
  const lastUser = [...messages].reverse().find((message) => message?.role === 'user');
  if (!lastUser) return false;
  const texts = extractTextsFromContent(lastUser.content);
  for (const text of texts) {
    if (text.includes(TITLE_GENERATION_MARKER) || text.includes('Please name the conversation')) return true;
  }
  return false;
}

export function requestKindOf(body) {
  if (isTitleGenerationRequest(body)) return 'title';
  if (isSyntheticContinuation(body)) return 'synthetic';
  return 'chat';
}

export function requestSessionOf(body) {
  return body?.sessionId
    || body?.sessionID
    || body?.__testkitHeaders?.['x-session-affinity']
    || body?.__testkitHeaders?.['x-session-id']
    || null;
}

export function requestParentSessionOf(body) {
  return body?.parentSessionId
    || body?.parentSessionID
    || body?.__testkitHeaders?.['x-parent-session-id']
    || null;
}

export function requestRoleOf(body) {
  const requestKind = requestKindOf(body);
  if (requestKind !== 'chat') return requestKind;

  const lastUser = extractLastUserMsg(body) || '';
  if (lastUser.includes('You are the blogger')) return 'blogger';
  const roleCanary = lastUser.match(/Role canary: (executor|inspector|reviewer)\b/);
  if (roleCanary) return roleCanary[1];
  if (lastUser.includes('Summarize command output chunk') || lastUser.includes('Reduce these command-output summaries')) {
    return 'executor';
  }

  const tools = extractToolNames(body);
  if (tools.includes('verdict')) return 'reviewer';
  // DevOps owns fork-pty and may also see executor/inspector/coder tools.
  // Must be classified before the bare executor→inspector heuristic.
  if (tools.includes('fork-pty')) return 'devops';
  if (tools.includes('executor')) return 'inspector';
  if (tools.includes('write') || tools.includes('edit')) return 'coder';
  if (tools.includes('fork') && tools.includes('join') && tools.includes('list')) return 'manager';
  // Orchestrator product tool is fork-manager (prompt-only), not fork.
  if (tools.includes('fork-manager') && tools.includes('join')) return 'orchestrator';
  if (tools.includes('fork') && tools.includes('join')) return 'orchestrator';
  return 'unknown';
}

function pickToolName(t) {
  return t?.function?.name ?? t?.name;
}

export function extractToolNames(body) {
  const tools = body?.tools;
  if (!Array.isArray(tools)) return [];
  const out = [];
  for (const t of tools) {
    const name = pickToolName(t);
    if (typeof name === 'string') out.push(name);
  }
  return out;
}

export function extractLastUserMsg(body) {
  const msgs = body?.messages || [];
  const last = [...msgs].reverse().find((m) => m?.role === 'user');
  if (!last) return null;
  const c = last.content;
  if (typeof c === 'string') return c.slice(0, 2000);
  if (Array.isArray(c)) return JSON.stringify(c).slice(0, 2000);
  return null;
}

function modelMatches(actual, expected) {
  if (expected === undefined || expected === null) return true;
  if (typeof expected === 'string') {
    if (typeof actual === 'string') return actual === expected;
    return actual?.modelID === expected || actual?.id === expected;
  }
  if (!actual || typeof actual !== 'object') return false;
  return actual.providerID === expected.providerID
    && (actual.modelID || actual.id) === (expected.modelID || expected.id)
    && (expected.variant === undefined || actual.variant === expected.variant);
}

/**
 * Script head match on the last user message.
 * - match.user: distinctive substring of the first user turn
 * - match.userRegex: regex source for long/complex first turns
 * - match.containsText: legacy list of substrings (same as multiple user fragments)
 * Concurrent heads must be mutually unique. Ambiguity is a script-author bug.
 * No agent/role tags.
 */
export function matchesExpectation(body, expectation, sessionBindings) {
  const match = expectation.match || {};
  const sessionID = requestSessionOf(body);
  const parentSessionID = requestParentSessionOf(body);
  const expectedSessionID = sessionBindings?.get(expectation.lane.session);
  const expectedParentSessionID = expectation.lane.parentSession
    ? sessionBindings?.get(expectation.lane.parentSession)
    : null;

  if (match.sessionId && sessionID !== match.sessionId) return false;
  // neverEnd scripts absorb every matching request for the scenario (busy hang,
  // blogger sidecars). Do not require session-alias bind — bindChild races and
  // post-nudge same-role sessions must still hit the same head.
  if (!expectation.neverEnd && sessionID && expectedSessionID && sessionID !== expectedSessionID) {
    return false;
  }
  if (!expectation.neverEnd && expectedParentSessionID && parentSessionID && parentSessionID !== expectedParentSessionID) {
    return false;
  }

  if (match.model && !modelMatches(body?.model, match.model)) return false;
  if (match.requestKind && requestKindOf(body) !== match.requestKind) return false;

  if (match.requiredTools && match.requiredTools.length > 0) {
    const names = extractToolNames(body);
    for (const r of match.requiredTools) {
      if (!names.includes(r)) return false;
    }
  }
  if (match.forbiddenTools && match.forbiddenTools.length > 0) {
    const names = extractToolNames(body);
    for (const f of match.forbiddenTools) {
      if (names.includes(f)) return false;
    }
  }

  const lastUser = extractLastUserMsg(body) || '';
  const lastUserStr = typeof lastUser === 'string' ? lastUser : JSON.stringify(lastUser);

  if (match.user !== undefined && match.user !== null && match.user !== '') {
    if (!lastUserStr.includes(String(match.user))) return false;
  }
  if (match.userRegex) {
    let re;
    try {
      re = new RegExp(match.userRegex, match.userRegexFlags || '');
    } catch {
      return false;
    }
    if (!re.test(lastUserStr)) return false;
  }
  if (match.containsText && match.containsText.length > 0) {
    for (const t of match.containsText) {
      if (!lastUserStr.includes(t)) return false;
    }
  }

  if (match.messageCount !== undefined) {
    const messages = body?.messages || [];
    if (messages.length !== match.messageCount) return false;
  }
  return true;
}


/**
 * Provider-visible projection for prefix-cache checks (AGENTS.md):
 * only role / text / reasoning / tool call / result. No timestamp/cost/usage/ids
 * that the model never sees as content.
 * Seal is the canonical JSON of tools + messages. Next chat for the same session
 * must keep this seal as a byte-prefix (append-only).
 */
function normalizeVisibleContent(content) {
  if (content == null) return null;
  if (typeof content === 'string') return content;
  if (Array.isArray(content)) {
    return content.map((part) => {
      if (!part || typeof part !== 'object') return part;
      const out = {};
      if (part.type !== undefined) out.type = part.type;
      if (part.text !== undefined) out.text = part.text;
      if (part.reasoning !== undefined) out.reasoning = part.reasoning;
      if (part.name !== undefined) out.name = part.name;
      if (part.tool_call_id !== undefined) out.tool_call_id = part.tool_call_id;
      if (part.id !== undefined) out.id = part.id;
      if (part.function !== undefined) {
        out.function = {
          name: part.function.name,
          arguments: part.function.arguments,
        };
      }
      if (part.arguments !== undefined) out.arguments = part.arguments;
      return out;
    });
  }
  if (typeof content === 'object') {
    const out = {};
    if (content.text !== undefined) out.text = content.text;
    if (content.reasoning !== undefined) out.reasoning = content.reasoning;
    return out;
  }
  return content;
}

export function sealProviderVisible(body) {
  const tools = (body?.tools || []).map((t) => ({
    name: t?.function?.name ?? t?.name ?? null,
    // parameters schema participates in provider prefix when present
    parameters: t?.function?.parameters ?? t?.parameters ?? null,
  }));
  const messages = (body?.messages || []).map((m) => ({
    role: m?.role ?? null,
    content: normalizeVisibleContent(m?.content),
    tool_calls: Array.isArray(m?.tool_calls)
      ? m.tool_calls.map((tc) => ({
          id: tc?.id ?? null,
          type: tc?.type ?? null,
          function: tc?.function
            ? { name: tc.function.name, arguments: tc.function.arguments }
            : null,
        }))
      : undefined,
    name: m?.name,
    tool_call_id: m?.tool_call_id,
  }));
  return JSON.stringify({ tools, messages });
}

/** True when previous seal is a provider-visible prefix of the next request. */
export function isProviderVisiblePrefix(previousSeal, nextBody) {
  if (!previousSeal) return true;
  let prev;
  try {
    prev = JSON.parse(previousSeal);
  } catch {
    return false;
  }
  const next = JSON.parse(sealProviderVisible(nextBody));
  // Tools must stay identical for KV-cache prefix match.
  if (JSON.stringify(prev.tools) !== JSON.stringify(next.tools)) return false;
  if (!Array.isArray(prev.messages) || !Array.isArray(next.messages)) return false;
  if (prev.messages.length > next.messages.length) return false;
  for (let i = 0; i < prev.messages.length; i += 1) {
    if (JSON.stringify(prev.messages[i]) !== JSON.stringify(next.messages[i])) return false;
  }
  return true;
}

export function estimatePromptTokens(body) {
  return Math.max(1, Math.ceil(JSON.stringify(body?.messages || []).length / 2));
}
