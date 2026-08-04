/**
 * legacy-fields.js — fields a TOML scenario may not use, and what replaced each.
 *
 * VERIFY-003. Every name below existed in the JSON forest and now has a strictly
 * better expression. Rejecting them is not tidiness: each one, if silently accepted,
 * would be ignored by the compiler while the author believed it was in force. A
 * scenario carrying `reusable = true` that the loader drops is a scenario whose author
 * thinks an edge is reusable and gets a one-shot.
 *
 * The reason string matters as much as the rejection. An author hitting one of these
 * needs to know what to write instead, and `design-script-forest.md` §3-§8 is where
 * the argument lives — so the message names the replacement rather than just refusing.
 */

/** Field → what to write instead. Keys are checked at every nesting level. */
export const RETIRED_FIELDS = {
  // §4: predicate conjunction → turn prefix
  match: 'declare the turn text; a step is keyed by (lane, turn, step), not by a predicate bag',
  userRegex: 'declare a distinctive literal prefix of the user text; regexes cannot be prefix-ordered',
  containsText: 'declare the turn text as a prefix',
  requiredTools: 'declare tools on the turn; they are part of the request identity, not a filter',
  forbiddenTools: 'a tool set that must be absent is a different turn, so declare that turn',
  messageCount: 'use step; it counts assistant messages after the last user message',
  afterToolResult: 'a tool result is already in the semantic prefix, so the prefix shape says this',

  // §4: scoring → longest prefix
  specificity: 'the longest matching prefix is unique; there is nothing left to score',

  // §5: the mock re-deriving domain concepts
  role: 'the role comes from AttemptExecutionProfile (PROMPT-008); a mock may not infer it from the wire',
  requestRoleOf: 'same as role: PROMPT-008 makes the profile the only source',


  // §6: out-of-band identity leaking into content matching
  __testkitHeaders: 'harness bookkeeping is one-way; a scenario matches only what the provider received',

  // Measured dead twice over. Its only source was `__testkitHeaders['x-parent-session-id']`
  // (retired above), and `matchesExpectation` looked the value up in `sessionBindings` —
  // where all 16 scenarios that declared a parent had never bound it, so the comparison
  // short-circuited and never ran. Use `internal = true` to say a lane is production-
  // composed; nothing needs to name the parent.
  parentSession:
    'a parent session id is out-of-band and was never bound; mark the lane internal = true instead',

  // §7: flag explosion
  reusable: 'content is a pure function of the request, so every step is inherently reusable',
  pathless: 'there is no cursor to be exempt from',
  neverEnd: 'a step that answers many requests answers them at several steps; declare those steps',
  blocking: 'assert arrival with must, not with a matching flag',
  claimCount: 'each step is independently waitable; there is nothing to count',
  matchCount: 'each step is independently waitable; there is nothing to count',
  aliases: 'two PERFECT verdicts are two steps (REVIEW-003), not one step with two names',

  // §8: dynamic loading
  loadScripts: 'a scenario is one static file; a restart does not change the script',
};

/**
 * `turn` is legitimate as semantic text and retired as an ordinal.
 *
 * The old form was `lane.turn = 7`, an index into a hand-maintained sequence — exactly
 * the program counter ARCH-001 forbids, one layer down. The new form is the user
 * message's content, which is what makes the key a function of the request.
 */
const turnOrdinalProblem = (path, value) =>
  typeof value === 'number'
    ? `${path}: turn must be the user message text, not an ordinal (${value}); ` +
      'a number is a hand-maintained cursor, and step already carries position'
    : null;

const isPlainObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);

/**
 * Every retired field anywhere in a parsed scenario.
 *
 * Walks the whole tree rather than checking known locations: the JSON forest put
 * `reusable` on an edge, `blocking` on an edge, and `loadScripts` inside `flow`, so a
 * location-aware check would need the very map of legacy shapes this replaces.
 */
export function retiredFieldProblems(value, path = '') {
  if (Array.isArray(value)) {
    return value.flatMap((item, index) => retiredFieldProblems(item, `${path}[${index}]`));
  }
  if (!isPlainObject(value)) return [];

  const problems = [];

  for (const [key, child] of Object.entries(value)) {
    const childPath = path === '' ? key : `${path}.${key}`;

    if (key in RETIRED_FIELDS) {
      problems.push(`${childPath} is retired: ${RETIRED_FIELDS[key]}`);
      continue;
    }

    if (key === 'turn') {
      const ordinal = turnOrdinalProblem(childPath, child);
      if (ordinal !== null) {
        problems.push(ordinal);
        continue;
      }
    }

    problems.push(...retiredFieldProblems(child, childPath));
  }

  return problems;
}
