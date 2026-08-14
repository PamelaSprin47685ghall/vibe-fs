/**
 * strict-mock-matches.js — Request-body inspection for StrictMockProvider.
 *
 * What survives the K9 retirement: the two diagnostic extractors the provider
 * uses to label fatal mismatches. The legacy expectation matcher
 * (`matchesExpectation`, `requestRoleOf`, lane/session lookups) is deleted with
 * strict-mock-forest.js — selection lives in `ScenarioRuntime`. Pure functions,
 * no I/O.
 *
 * ARCH-011: the wire-body marker classifiers (`isZWSPContent`, `matchMarker`,
 * `requestKindOf`, …) were deleted with this cleanup. They inferred program
 * state from character features of synthetic payloads — zero-width/whitespace
 * sniffing, fixed template prose, prefix/suffix matching — exactly the reverse
 * inference the clause forbids. Request kind is a typed decision in
 * `ScenarioRuntime`; the mock never re-derives it from bytes.
 */

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
