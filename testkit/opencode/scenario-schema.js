/**
 * scenario-schema.js — TOML scenario source → the runtime structures K2-K4 consume.
 *
 * VERIFY-003. Two layers, deliberately not the same shape:
 *
 *   source     a conversation a human reads top to bottom
 *   compiled   (lane, turn, step) entries a machine looks up
 *
 * `design-script-forest.md` §9 argues the prefix index must never be the written
 * form: a hand-written prefix array repeats every earlier turn on every edge, so
 * adding one step means editing all downstream edges and the reader cannot see a
 * conversation at all. Same principle as production — SSOT clauses are prose, `Fold`
 * is the machine projection, and neither borrows the other's form.
 *
 * ── one deviation from the design document ──────────────────────────────────
 *
 * §10's example writes `step = "fork-agent"` inside `[[fault]]`, i.e. a step NAME.
 * Package K2 then established that the runtime `step` is an integer — the count of
 * assistant messages after the last user message, which is what makes the cursor
 * unnecessary.
 *
 * Both are right at their own layer, so the compiler resolves one into the other:
 * an author writes `turn = "manager"` and `step = "fork-agent"`, and the output
 * carries the integer. That is also what makes dangling-reference detection
 * possible — a name can be checked against the declared set, an integer cannot.
 */

import { parse as parseToml } from 'smol-toml';
import { retiredFieldProblems } from './legacy-fields.js';
import { validateFault } from './delivery-plan.js';

// ── the TOML root-key trap ──────────────────────────────────────────────────

const ROOT_KEYS = ['scenario', 'description', 'must', 'flow', 'setup', 'session', 'pass', 'prompt'];

/**
 * Flow verbs, measured from the 19 JSON scenarios rather than invented.
 *
 * A whitelist because `wait`'s dangling reference is checked but its NAME was not: a
 * typo'd `awaitTerminl` is silently ignored, and the author believes a barrier is in
 * force. Same protection as the retired-field rejector, one layer up.
 *
 * `loadScripts` is deliberately absent — the retired-field rejector owns it (§8).
 */
const FLOW_VERBS = new Set([
  'wait',
  'waitFact',
  'prompt',
  'session',
  'lane',
  'timeoutMs',
  'awaitTerminal',
  'requireAssistantTerminal',
  'awaitEvent',
  'awaitRestart',
  'restart',
  'abort',
  'bindChild',
  'createChild',
  'createSession',
  'expectSatisfied',
  'requireIdleAfterActivity',
  'assertFacts',
  'assertFile',
  'assertActiveRequests',
  'assertWorktreeClean',
  'assertPtyEcho',
  'assertModelTrajectory',
  'afterExpectation',
  'custom',
]);

const unknownFlowVerbs = (flow) =>
  (flow ?? []).flatMap((flowStep, index) =>
    Object.keys(flowStep ?? {})
      .filter((verb) => !FLOW_VERBS.has(verb))
      .map((verb) => `flow[${index}]: unknown verb '${verb}'; a misspelled verb is silently ignored`),
  );

/**
 * TOML assigns a root-level key to the LAST table header above it, silently.
 *
 * Measured: `flow = [...]` placed after `[[epoch]]` parses as `epoch[0].flow` with no
 * error. The parsed object cannot reveal this — `epoch[0].flow` is indistinguishable
 * from a `flow` key the author meant to put inside that table — so the check has to
 * read the source text, before and independently of parsing.
 */
export function rootKeyOrderProblems(source) {
  const problems = [];
  let seenHeader = null;

  source.split('\n').forEach((line, index) => {
    const text = line.trim();
    if (text === '' || text.startsWith('#')) return;

    if (text.startsWith('[')) {
      seenHeader = text;
      return;
    }

    const key = text.match(/^([A-Za-z_][\w-]*)\s*=/)?.[1];
    if (key !== undefined && ROOT_KEYS.includes(key) && seenHeader !== null) {
      problems.push(
        `line ${index + 1}: root key '${key}' appears after ${seenHeader}; ` +
          `TOML would silently nest it inside that table`,
      );
    }
  });

  return problems;
}

// ── compilation ─────────────────────────────────────────────────────────────

/** A step's position within its turn IS its runtime `step` (K2). */
const compileTurns = (turns) =>
  turns.flatMap((turn, turnIndex) =>
    (turn.step ?? []).map((step, stepIndex) => ({
      id: step.id ?? `${turn.id}.${stepIndex}`,
      turnId: turn.id,
      turnIndex,
      lane: turn.lane,
      internal: turn.internal === true,
      kind: turn.kind ?? 'chat',
      turn: turn.user,
      step: stepIndex,
      tools: turn.tools ?? [],
      respond: step.respond,
    })),
  );

/** Resolve `turn`+`step` names against the compiled entries. */
const resolveReference = (entries, reference) => {
  const byTurn = entries.filter((entry) => entry.turnId === reference.turn);
  if (byTurn.length === 0) return { missingTurn: reference.turn };

  if (reference.step === undefined) return { entry: byTurn[0] };

  const byStep =
    typeof reference.step === 'number'
      ? byTurn.filter((entry) => entry.step === reference.step)
      : byTurn.filter((entry) => entry.id === reference.step || entry.id.endsWith(`.${reference.step}`));

  return byStep.length === 0 ? { missingStep: reference.step } : { entry: byStep[0] };
};

/**
 * Which declared turns a flow can actually reach.
 *
 * Two ways in, and they are the only two the wire supports:
 *
 *   a `flow.prompt` sends the turn's user text
 *   an earlier step's tool call carries it as `args.prompt`
 *
 * The second is how child turns exist at all — `fork(agent, prompt)` is what creates
 * the session that will receive them. Both are prefix comparisons because a scenario
 * declares a distinctive fragment, not the whole utterance.
 *
 * This is the one check that requires the static whole. It is also the one most
 * likely to be too strict; package K8 converts the real scenarios and will say.
 */
export function reachableTurnIds(turns, entries, { flow, prompt } = {}) {
  // Reached by a scenario prompt, or by a reachable step's tool-call prompt. Fixpoint,
  // not one pass, because forks chain: Manager → Coder → the Coder's own children.
  //
  // `internal = true` opts a turn out. Production composes those prompts itself, so no
  // scenario text can reach them and the check would have no evidence either way. Three
  // measured sites, and the third is not a child session at all:
  //
  //   `CompanionHostBlogger.fs:72,77,118`  Blogger child, prompt built from the delta
  //   `ExecutorSummarize.fs:95`            map child, prompt built per output chunk
  //   `TurnCompletionProgram.fs:92`        continuation, SAME lane, new user message
  //
  // That last one is why this is a per-TURN flag rather than a per-lane one.
  const scenarioPrompts = [prompt?.text, ...(flow ?? []).map((flowStep) => flowStep.prompt?.text)].filter(
    (text) => typeof text === 'string',
  );

  const reachedBy = (texts, turn) =>
    texts.some((text) => text.startsWith(turn.user) || turn.user.startsWith(text));

  const reachableTurns = new Set(
    turns.filter((turn) => turn.internal === true || reachedBy(scenarioPrompts, turn)),
  );

  for (;;) {
    const before = reachableTurns.size;

    const toolPrompts = entries
      .filter((entry) => [...reachableTurns].some((turn) => turn.id === entry.turnId))
      .map((entry) => entry.respond?.args?.prompt)
      .filter((text) => typeof text === 'string');

    for (const turn of turns) {
      if (!reachableTurns.has(turn) && reachedBy(toolPrompts, turn)) reachableTurns.add(turn);
    }

    if (reachableTurns.size === before) break;
  }

  return new Set([...reachableTurns].map((turn) => turn.id));
}

// ── the load-time validations ───────────────────────────────────────────────

const responseDigest = (respond) => JSON.stringify(respond ?? null);

/**
 * One (lane, turn, step) declared twice. Replaces `specificity`.
 *
 * The design document lists "conflicting responses" and "ambiguous prefixes" as two
 * checks. Under (lane, turn, step) keying they are one: two declarations for one key
 * either disagree about the response or duplicate it, so a separate conflict check
 * would report the same pair twice with a weaker message.
 *
 * DEVIATION: the design document collapses identical templates. That was a mitigation
 * for predicate-conjunction matching, where template reuse produced duplicates
 * naturally. Under this keying a recurring nudge has several steps, so a true
 * duplicate is debris — both cases reject, and the message says which fix applies.
 *
 * DEVIATION: the design document collapses identical templates. That was a mitigation
 * for predicate-conjunction matching, where template reuse produced duplicates
 * naturally. Under (lane, turn, step) keying a recurring nudge has several steps, so a
 * true duplicate is debris — both cases reject, and the message says which fix applies.
 */
const duplicateDeclarations = (entries) => {
  const problems = [];

  for (const left of entries) {
    for (const right of entries) {
      if (left.lane !== right.lane || left.step !== right.step || left.turn !== right.turn) continue;
      if (left.kind !== right.kind) continue;
      if (left.id >= right.id) continue;

      problems.push(
        responseDigest(left.respond) === responseDigest(right.respond)
          ? `${left.id} and ${right.id} declare the same (lane, turn, step) with the same response; delete one`
          : `${left.id} and ${right.id} declare the same (lane, turn, step) with different responses; ` +
            'the scenario does not say what the model does next',
      );
    }
  }

  return problems;
};

/**
 * A fault declaration that is malformed rather than merely misplaced.
 *
 * `validateFault` had eight callers in the gate and none here, so until now a real
 * scenario could declare `attempts = []` — a fault that never fires — and load clean.
 * Exactly the zero-call-site shape `architecture-gate`'s `single-constructor` check
 * exists to catch one layer down.
 */
const malformedFaults = (scenario) =>
  (scenario.fault ?? []).flatMap((fault, index) =>
    validateFault({ ...fault, kind: fault.delivery ?? fault.kind }).map(
      (problem) => `fault[${index}]: ${problem}`,
    ),
  );

/**
 * A `provider-error` fault must say whether the Host should retry it.
 *
 * This is the load-bearing bit of a fallback scenario and it is invisible in the
 * response body: a retryable 500 means the HOST drives the retries, a non-retryable 400
 * means the Host gives up and the plugin must send a continuation (FALLBACK-009). Get it
 * wrong and the scenario still runs, just proving a different clause than its author
 * believes. So it is required rather than defaulted.
 */
const providerErrorProblems = (scenario) =>
  (scenario.fault ?? []).flatMap((fault, index) => {
    if ((fault.delivery ?? fault.kind) !== 'provider-error') return [];

    const problems = [];
    if (!Number.isInteger(fault.status)) {
      problems.push(`fault[${index}]: a provider-error must declare the HTTP status it returns`);
    }
    if (typeof fault.retryable !== 'boolean') {
      problems.push(
        `fault[${index}]: a provider-error must declare retryable; ` +
          'it decides whether the Host retries or the plugin continues the Logical Run (FALLBACK-009)',
      );
    }
    return problems;
  });

/**
 * Two faults governing one (lane, turn, step).
 *
 * `faultFor` already throws on this, but it throws at DELIVERY — so the author finds out
 * mid-run, in whichever scenario reached that step first, with the Host already up. The
 * static whole is available at load time; there is no reason to wait.
 */
const conflictingFaults = (entries, scenario) => {
  const seen = new Map();
  const problems = [];

  (scenario.fault ?? []).forEach((fault, index) => {
    const { entry } = resolveReference(entries, fault);
    if (entry === undefined) return;

    const key = `${entry.lane ?? ''}\u001f${entry.turn}\u001f${entry.step}`;
    const earlier = seen.get(key);

    if (earlier === undefined) seen.set(key, index);
    else {
      problems.push(
        `fault[${earlier}] and fault[${index}]: two faults declared for the same ` +
          `(lane, turn, step); one delivery cannot fail two ways`,
      );
    }
  });

  return problems;
};

/** An `assertModelTrajectory` naming a lane no turn declares. */
const trajectoryProblems = (entries, scenario) =>
  (scenario.flow ?? []).flatMap((flowStep, index) => {
    const claim = flowStep.assertModelTrajectory;
    if (claim === undefined) return [];

    const problems = [];
    if (!entries.some((entry) => entry.lane === claim.lane)) {
      problems.push(`flow[${index}] assertModelTrajectory references lane '${claim.lane}', which no turn declares`);
    }
    if (!Array.isArray(claim.models) || claim.models.length === 0) {
      problems.push(`flow[${index}] assertModelTrajectory needs the exact expected model sequence`);
    }
    return problems;
  });

/** 3-5. A fault, cold boundary or `must` naming something that does not exist. */
const danglingReferences = (entries, scenario) => {
  const problems = [];

  const check = (label, reference) => {
    const resolved = resolveReference(entries, reference);
    if (resolved.missingTurn !== undefined) {
      problems.push(`${label} references turn '${resolved.missingTurn}', which is not declared`);
    } else if (resolved.missingStep !== undefined) {
      problems.push(`${label} references step '${resolved.missingStep}' of turn '${reference.turn}', which is not declared`);
    }
    return resolved;
  };

  for (const fault of scenario.fault ?? []) check(`fault (${fault.delivery ?? fault.kind})`, fault);
  for (const boundary of scenario.epoch ?? []) check(`cold boundary (${boundary.reason ?? boundary.kind})`, boundary);

  for (const id of scenario.must ?? []) {
    if (!entries.some((entry) => entry.id === id || entry.turnId === id)) {
      problems.push(`must references '${id}', which is not a declared step or turn`);
    }
  }

  for (const flowStep of scenario.flow ?? []) {
    const waited = flowStep.wait;
    if (typeof waited === 'string' && !entries.some((entry) => entry.id === waited || entry.turnId === waited)) {
      problems.push(`flow wait references '${waited}', which is not a declared step or turn`);
    }
  }

  return problems;
};

/** 6. A declared step no flow can reach. */
const deadEdges = (turns, entries, scenario) => {
  const reachable = reachableTurnIds(turns, entries, scenario);

  return turns
    .filter((turn) => !reachable.has(turn.id))
    .map((turn) => `turn '${turn.id}' is declared but no flow prompt or tool call reaches it (dead edge)`);
};

// ── the entry point ─────────────────────────────────────────────────────────

/**
 * Compile one scenario source.
 *
 * Returns `{ ok: true, scenario }` or `{ ok: false, problems }`. Never a partial
 * scenario with warnings: a scenario that half-loads is a scenario whose author
 * believes something is covered that is not.
 */
export function compileScenario(source, { name = '<inline>' } = {}) {
  const orderProblems = rootKeyOrderProblems(source);
  if (orderProblems.length > 0) {
    return { ok: false, problems: orderProblems.map((problem) => `${name}: ${problem}`) };
  }

  let raw;
  try {
    raw = parseToml(source);
  } catch (error) {
    return { ok: false, problems: [`${name}: TOML parse failed: ${error.message}`] };
  }

  const retired = retiredFieldProblems(raw);
  if (retired.length > 0) {
    return { ok: false, problems: retired.map((problem) => `${name}: ${problem}`) };
  }

  const problems = [];
  if (typeof raw.scenario !== 'string' || raw.scenario === '') problems.push('scenario name is required');

  const turns = raw.turn ?? [];
  turns.forEach((turn, index) => {
    if (typeof turn.id !== 'string' || turn.id === '') problems.push(`turn[${index}] needs an id`);
    if (typeof turn.user !== 'string' || turn.user === '') problems.push(`turn[${index}] needs user text`);
    if (!Array.isArray(turn.step) || turn.step.length === 0) problems.push(`turn[${index}] needs at least one step`);
    if (turn.kind !== undefined && turn.kind !== 'chat' && turn.kind !== 'title') {
      problems.push(`turn[${index}] kind '${turn.kind}' is not chat or title`);
    }
    if (turn.internal !== undefined && turn.internal !== true) {
      problems.push(`turn[${index}] internal must be true when present; omit it otherwise`);
    }
  });

  if (problems.length > 0) {
    return { ok: false, problems: problems.map((problem) => `${name}: ${problem}`) };
  }

  const entries = compileTurns(turns);

  const validationProblems = [
    ...unknownFlowVerbs(raw.flow),
    ...malformedFaults(raw),
    ...providerErrorProblems(raw),
    ...conflictingFaults(entries, raw),
    ...trajectoryProblems(entries, raw),
    ...duplicateDeclarations(entries),
    ...danglingReferences(entries, raw),
    ...deadEdges(turns, entries, raw),
  ];

  if (validationProblems.length > 0) {
    return { ok: false, problems: validationProblems.map((problem) => `${name}: ${problem}`) };
  }

  return {
    ok: true,
    scenario: {
      name: raw.scenario,
      description: raw.description,
      must: raw.must ?? [],
      flow: raw.flow ?? [],
      entries,
      faults: (raw.fault ?? []).map((fault) => compileFault(entries, fault)),
      boundaries: (raw.epoch ?? []).map((boundary) => compileBoundary(entries, boundary)),
    },
  };
}

/** A fault's `turn`/`step` names become the integer key `faultFor` looks up. */
const compileFault = (entries, fault) => {
  const { entry } = resolveReference(entries, fault);
  return {
    lane: entry.lane,
    turn: entry.turn,
    step: entry.step,
    attempts: fault.attempts,
    kind: fault.delivery ?? fault.kind,
    status: fault.status,
    retryable: fault.retryable,
  };
};

const compileBoundary = (entries, boundary) => {
  const { entry } = resolveReference(entries, boundary);
  return {
    lane: entry.lane,
    turn: entry.turn,
    step: entry.step,
    kind: boundary.reason ?? boundary.kind,
  };
};
