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
  for (const msg of messages) {
    const texts = extractTextsFromContent(msg?.content);
    for (const t of texts) {
      if (t.includes(TITLE_GENERATION_MARKER)) return true;
    }
  }
  return false;
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
  if (typeof c === 'string') return c.slice(0, 300);
  if (Array.isArray(c)) return JSON.stringify(c[0]).slice(0, 300);
  return null;
}

export function matchesExpectation(body, expectation) {
  const match = expectation.match || {};
  if (match.sessionId && (body?.sessionId || '') !== match.sessionId) return false;
  if (match.model && (body?.model || '') !== match.model) return false;
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
  if (match.containsText && match.containsText.length > 0) {
    const bodyStr = JSON.stringify(body);
    for (const t of match.containsText) {
      if (!bodyStr.includes(t)) return false;
    }
  }
  if (match.messageCount !== undefined) {
    const messages = body?.messages || [];
    if (messages.length !== match.messageCount) return false;
  }
  return true;
}

export function estimatePromptTokens(body) {
  return Math.max(1, Math.ceil(JSON.stringify(body?.messages || []).length / 2));
}
