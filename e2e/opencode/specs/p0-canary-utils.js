/**
 * p0-canary-utils.js — Shared helpers for P0 canary tests.
 * Kept in its own module so p0-canary.js stays under the 300-line
 * Kolmogorov line budget.
 */

import fs from 'node:fs';

const NEWLINE = Buffer.from([0x0a]);
export const content = (s) => s + NEWLINE.toString();

export function writeWorkFile(workDir, rel, body) {
  const abs = workDir + '/' + rel;
  fs.writeFileSync(abs, body, 'utf8');
  return abs;
}

export function findPtySessionId(reqBody) {
  const allText = JSON.stringify(reqBody.messages || []);
  const candidates = allText.match(/pty_[a-zA-Z0-9]+/g) || [];
  const toolNames = new Set(['pty_spawn', 'pty_kill', 'pty_list', 'pty_read', 'pty_write']);
  const real = candidates.find((id) => !toolNames.has(id));
  return real || 'unknown-pty';
}

export function extractToolNames(tools) {
  return (tools || []).map((t) => t.function?.name || t.name);
}

export function readNdjsonLines(workDir) {
  const ndjsonPath = workDir + '/.wanxiangshu.ndjson';
  if (!fs.existsSync(ndjsonPath)) return [];
  const lines = fs.readFileSync(ndjsonPath, 'utf8').split('\n').filter((l) => l.trim());
  return lines.map((l) => {
    try { return JSON.parse(l); } catch { return null; }
  }).filter(Boolean);
}

export async function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

const CONTROL_FIELDS = [
  'follow-tdd-and-kolmogorov-principles',
  'impossible-via-other-tools',
  'not-suitable-via-continue-tool'
];

const EXPECTED_CONTROL_FIELDS = {
  'follow-tdd-and-kolmogorov-principles': new Set(['coder', 'executor', 'write', 'edit', 'apply_patch', 'patch', 'ast_edit',
    'ast_grep_replace', 'file_edit_replace_string', 'file_edit_insert',
    'pty_spawn', 'pty_write', 'pty_read', 'pty_list', 'pty_kill', 'swap']),
  'impossible-via-other-tools': new Set(['executor', 'pty_spawn', 'pty_write', 'pty_read', 'pty_list', 'pty_kill']),
  'not-suitable-via-continue-tool': new Set(['coder', 'inspector', 'meditator', 'browser']),
};

function validateSchema(toolName, schema, errors) {
  if (!schema || typeof schema !== 'object') {
    errors.push(`${toolName}: parameters is not an object`);
    return;
  }
  if (schema.type !== 'object') {
    errors.push(`${toolName}: parameters.type is not 'object'`);
  }
  if (!schema.properties || typeof schema.properties !== 'object') {
    errors.push(`${toolName}: parameters.properties missing`);
  }
  const required = schema.required || [];
  if (!Array.isArray(required)) {
    errors.push(`${toolName}: parameters.required is not an array`);
    return;
  }
  const propKeys = new Set(Object.keys(schema.properties || {}));
  for (const r of required) {
    if (!propKeys.has(r)) {
      errors.push(`${toolName}: required field '${r}' missing from properties`);
    }
  }
}

export function assertFuzzyGrepResult(output, { expectedFile, expectedPattern, minMatches }) {
  if (typeof output !== 'string') throw new Error('fuzzy_grep output is not a string');

  const countMatch = output.match(/^(\d+) matches/);
  if (!countMatch) throw new Error(`fuzzy_grep count line missing: ${output.slice(0, 200)}`);
  const matchCount = Number(countMatch[1]);
  if (matchCount < minMatches) {
    throw new Error(`fuzzy_grep expected >= ${minMatches} matches, got ${matchCount}`);
  }

  const fileHeaders = [];
  const seenLines = new Set();
  const lineRegex = /\n (\d+): (.*)/g;
  let lineMatch;
  while ((lineMatch = lineRegex.exec(output)) !== null) {
    const [, lineNo, lineText] = lineMatch;
    if (!lineText.includes(expectedPattern)) {
      throw new Error(`fuzzy_grep line text missing pattern: ${lineText}`);
    }
    seenLines.add(Number(lineNo));
  }
  if (seenLines.size < minMatches) {
    throw new Error(`fuzzy_grep expected >= ${minMatches} line matches, got ${seenLines.size}`);
  }

  // Capture file headers like "grep_target.txt  [untracked in git]".
  const headerRegex = /^(.+?)\s+\[[^\]]+\]$/gm;
  let headerMatch;
  while ((headerMatch = headerRegex.exec(output)) !== null) {
    fileHeaders.push(headerMatch[1].trim());
  }
  if (!fileHeaders.includes(expectedFile)) {
    throw new Error(`fuzzy_grep expected file ${expectedFile} missing; got ${fileHeaders.join(', ')}`);
  }
  const fakeFiles = fileHeaders.filter((f) => f !== expectedFile);
  if (fakeFiles.length > 0) {
    throw new Error(`fuzzy_grep reported unexpected files: ${fakeFiles.join(', ')}`);
  }

  return { matchCount, lines: [...seenLines] };
}

export function findToolPart(messages, toolName, predicate) {
  for (const msg of messages || []) {
    for (const part of msg?.parts || []) {
      if (part.type === 'tool' && part.tool === toolName) {
        if (!predicate || predicate(part)) return part;
      }
    }
  }
  return null;
}

export function validateToolSchema(tools) {
  const errors = [];
  const names = [];
  const seen = new Set();
  for (const entry of tools || []) {
    const fn = entry.function || entry;
    const name = fn.name;
    if (!name || typeof name !== 'string') {
      errors.push('tool missing name');
      continue;
    }
    if (seen.has(name)) {
      errors.push(`duplicate tool name: ${name}`);
    }
    seen.add(name);
    names.push(name);

    const desc = fn.description;
    if (!desc || typeof desc !== 'string' || desc.trim().length < 5) {
      errors.push(`${name}: description missing or too short`);
    }

    validateSchema(name, fn.parameters, errors);

    // Executor must expose max_bytes.
    if (name === 'executor') {
      const props = fn.parameters?.properties || {};
      if (!props.max_bytes) {
        errors.push('executor: missing max_bytes property');
      }
      const req = fn.parameters?.required || [];
      if (!req.includes('max_bytes')) {
        errors.push('executor: max_bytes not in required');
      }
    }

    // Control fields must only appear on the tools that should carry them.
    const props = fn.parameters?.properties || {};
    for (const field of CONTROL_FIELDS) {
      const hasField = Object.prototype.hasOwnProperty.call(props, field);
      const expected = EXPECTED_CONTROL_FIELDS[field];
      if (hasField && !expected.has(name)) {
        errors.push(`${name}: unexpected control field ${field}`);
      }
      if (!hasField && expected.has(name)) {
        errors.push(`${name}: missing expected control field ${field}`);
      }
    }
  }

  // Agent tools (subagent dispatchers) should be present and carry warn_reuse.
  const agentTools = ['browser', 'coder', 'inspector', 'meditator'];
  for (const name of agentTools) {
    if (!seen.has(name)) {
      errors.push(`agent tool missing: ${name}`);
    }
  }

  if (errors.length > 0) {
    throw new Error(`tool schema validation failed:\n${errors.join('\n')}`);
  }
  return names;
}

export function parsePtySpawnOutput(output) {
  const id = output.match(/^id: (pty_[a-zA-Z0-9]+)$/m)?.[1];
  const pid = Number(output.match(/^pid: (\d+)$/m)?.[1]);
  const status = output.match(/^status: (\S+)$/m)?.[1];
  return { id, pid, status };
}

export function parsePtyListOutput(output, expectedId) {
  const entries = [];
  const headerRegex = /^### (pty_[a-zA-Z0-9]+)$/gm;
  let m;
  while ((m = headerRegex.exec(output)) !== null) {
    const end = output.indexOf('###', m.index + 1);
    const chunk = output.slice(m.index, end === -1 ? undefined : end);
    const id = m[1];
    const pid = Number(chunk.match(/^pid: (\d+)$/m)?.[1]);
    const status = chunk.match(/^status: (\S+)$/m)?.[1];
    const command = chunk.match(/^command: (.+)$/m)?.[1];
    entries.push({ id, pid, status, command });
  }
  if (expectedId) return entries.find((e) => e.id === expectedId) || null;
  return entries;
}

export function parsePtyKillOutput(output) {
  const id = output.match(/^id: (pty_[a-zA-Z0-9]+)$/m)?.[1];
  const action = output.match(/^action: (\S+)$/m)?.[1];
  const cleanup = output.match(/^cleanup: (\S+)$/m)?.[1] === 'true';
  return { id, action, cleanup };
}

export const TIMEOUTS = {
  prompt: 60000,
  quick: 30000,
  long: 90000,
  idleNudge: 30000,
  postNudgeObserve: 3000,
  ptyPostObserve: 200,
};
