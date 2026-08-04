/**
 * strict-mock-matches.js — Request-body inspection for StrictMockProvider.
 *
 * What survives the K9 retirement: request-kind classification (title /
 * synthetic / chat) plus the two diagnostic extractors the provider uses to
 * label fatal mismatches. The legacy expectation matcher (`matchesExpectation`,
 * `requestRoleOf`, lane/session lookups) is deleted with strict-mock-forest.js —
 * selection lives in `ScenarioRuntime`. Pure functions, no I/O.
 */

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
