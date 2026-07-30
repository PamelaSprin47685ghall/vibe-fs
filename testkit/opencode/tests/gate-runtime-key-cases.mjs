/**
 * gate-runtime-key-cases.mjs — the scenario lookup key is a pure function.
 *
 * VERIFY-003. Three properties, each of which the old matcher broke:
 *
 *   pure       the answer depends on the request, not on how many came before
 *   prefix     longest declared prefix wins, and ties are author errors
 *   countable  `step` is read off the request, never accumulated
 *
 * `design-script-forest.md` §3-§4 measured the alternative: an eleven-predicate
 * conjunction can match several edges at once, so a `specificity` score summed
 * substring lengths and added magic numbers (`afterToolResult === true` → +50) to
 * pick one. That is not disambiguation, it is a tiebreak over a key that should not
 * have ties.
 */

import { assertEq, assertTrue } from './gate-lib.mjs';
import { kindOf, lanesOf, resolveEntry, runtimeKeyOf, sessionIdOf, stepOf, turnOf } from '../runtime-key.js';

const SESSION = 'ses_real_1';
const BINDINGS = new Map([
  ['fast-manager', SESSION],
  ['coder-after', 'ses_real_2'],
]);

const user = (text) => ({ role: 'user', content: text });
const assistant = (text) => ({ role: 'assistant', content: text });
const toolResult = (content) => ({ role: 'tool', tool_call_id: 'c1', content });
const toolCall = (name, args = '{}') => ({
  role: 'assistant',
  content: null,
  tool_calls: [{ id: 'c1', type: 'function', function: { name, arguments: args } }],
});

const request = (messages, sessionID = SESSION) => ({ sessionID, messages });

const entry = ({ id, turn, step = 0, lane = 'fast-manager' }) => ({ id, lane, turn, step });

/** `prompt.ts:235` prepends this as `messages[0]`, then appends the real conversation. */
const titleRequest = (text) =>
  request([{ role: 'user', content: 'Generate a title for this conversation:\n' }, user(text)]);

const titleRequestOn = (text, sessionID) =>
  request([{ role: 'user', content: 'Generate a title for this conversation:\n' }, user(text)], sessionID);

export const runtimeKeyCases = [
  // ── kind: the fourth component, and why turn alone cannot carry it ────────

  {
    name: 'VERIFY-003 a title request and its chat turn share turn and step',
    fn: () => {
      // The measurement that forced `kind` into the key. The Host prepends its title
      // marker at `messages[0]` and appends the whole conversation after it
      // (`../opencode/packages/opencode/src/session/prompt.ts:235`), while `turnOf`
      // reads the LAST user message — so both requests report the same turn.
      //
      // Without a fourth component a title edge and a chat edge for one turn collide,
      // and the load-time duplicate check would reject a legitimate scenario.
      const title = titleRequest('Ship the parser fix.');
      const chat = request([user('Ship the parser fix.')]);

      assertEq(turnOf(title), turnOf(chat), 'turn cannot tell them apart');
      assertEq(stepOf(title), stepOf(chat), 'nor can step');
      assertEq(kindOf(title), 'title');
      assertEq(kindOf(chat), 'chat');
    },
  },

  {
    name: 'VERIFY-003 kind is read from the preamble, not from position 0',
    fn: () => {
      // This case previously asserted the marker must be at `messages[0]` and cited
      // `prompt.ts:235` for it. Measured against a live Host in K9, a real title request is:
      //
      //   roles   ["system", "user", "user"]
      //   [0]     "You are a title generator. You output ONLY a thread title…"
      //   [1]     "Generate a title for this conversation:\n"
      //
      // The title agent's system prompt comes first. So the old assertion was not merely
      // incomplete — it pinned the WRONG position, every title request classified as `chat`,
      // and no title turn could ever match. The case passed because its fixture built the
      // shape the code expected.
      assertEq(kindOf(titleRequest('Ship it.')), 'title', 'marker after a system preamble');
      assertEq(
        kindOf(request([
          { role: 'system', content: 'You are a title generator.' },
          user('Generate a title for this conversation:\n'),
          user('Ship it.'),
        ])),
        'title',
        'the measured production shape',
      );

      // Still bounded: the marker is a preamble, so a real turn cannot push it back. A user
      // quoting the phrase deep in a conversation is having an ordinary chat turn, and
      // answering it with a title would be the prose-matching this avoids.
      assertEq(
        kindOf(request([user('a'), user('b'), user('c'), user('d'), user('Generate a title for this conversation:')])),
        'chat',
        'beyond the preamble it is just text',
      );
      assertEq(kindOf(request([])), 'chat');
    },
  },

  {
    name: 'VERIFY-003 there is no synthetic kind',
    fn: () => {
      // The old classifier had one, decided by `NUDGE_MARKERS` — production prompt
      // sentences copied into the mock, which the extinction list condemns as a
      // cross-product dead heuristic. It is unnecessary: a nudge's LAST user message IS
      // the nudge sentence, so `turnOf` already distinguishes it.
      const nudge = request([
        user('Ship it.'),
        assistant('r1'),
        user('There are still incomplete todos. Continue working through the remaining items.'),
      ]);

      assertEq(kindOf(nudge), 'chat', 'a nudge is an ordinary chat request');
      assertEq(turnOf(nudge), 'There are still incomplete todos. Continue working through the remaining items.');
    },
  },

  {
    name: 'VERIFY-003 kind partitions declarations, and defaults to chat',
    fn: () => {
      const entries = [
        { id: 'chat', lane: 'fast-manager', turn: 'Ship it.', step: 0 },
        { id: 'title', lane: 'fast-manager', kind: 'title', turn: 'Ship it.', step: 0 },
      ];

      assertEq(resolveEntry(request([user('Ship it.')]), entries, BINDINGS).matched?.id, 'chat');
      assertEq(resolveEntry(titleRequest('Ship it.'), entries, BINDINGS).matched?.id, 'title');

      // An undeclared kind means chat, so single-lane scenarios need not say so.
      assertEq(runtimeKeyOf(request([user('Ship it.')]), BINDINGS).kind, 'chat');
    },
  },

  // ── step is a property of the request ─────────────────────────────────────

  {
    name: 'VERIFY-003 step counts assistant messages after the last user message',
    fn: () => {
      // The Host appends exactly one assistant message per provider step
      // (`../opencode/packages/opencode/src/session/prompt.ts:1186`), so this is
      // countable rather than something the mock has to remember.
      assertEq(stepOf(request([user('go')])), 0, 'first provider step of a turn');
      assertEq(stepOf(request([user('go'), assistant('r1')])), 1, 'after one reply');
      assertEq(stepOf(request([user('go'), toolCall('fork'), toolResult('ok'), assistant('r2')])), 2, 'two replies');
    },
  },

  {
    name: 'VERIFY-003 a new user message resets step',
    fn: () => {
      // Turn boundaries are where step restarts. A cursor would have kept counting.
      assertEq(stepOf(request([user('go'), assistant('r1'), user('again')])), 0);
      assertEq(stepOf(request([user('go'), assistant('r1'), user('again'), assistant('r2')])), 1);
    },
  },

  {
    name: 'VERIFY-003 step is zero when there is no user message at all',
    fn: () => {
      assertEq(stepOf(request([assistant('r1')])), 0);
      assertEq(stepOf(request([])), 0);
      assertEq(stepOf({}), 0);
    },
  },

  {
    name: 'VERIFY-003 tool results do not count as steps',
    fn: () => {
      // Only assistant messages are provider steps. Counting tool results would
      // double-count a single step that happened to call a tool.
      assertEq(stepOf(request([user('go'), toolResult('a'), toolResult('b')])), 0);
      assertEq(stepOf(request([user('go'), toolCall('fork'), toolResult('a')])), 1);
    },
  },

  {
    name: 'VERIFY-003 reading the key twice gives the same answer',
    fn: () => {
      // Purity, stated directly. The old `pathCursor` advanced on observation, so
      // asking twice moved the answer.
      const body = request([user('go'), assistant('r1')]);
      const first = runtimeKeyOf(body, BINDINGS);
      const second = runtimeKeyOf(body, BINDINGS);

      assertEq(first.lane, second.lane);
      assertEq(first.turn, second.turn);
      assertEq(first.step, second.step);
    },
  },

  // ── turn is prefix-comparable semantic text ──────────────────────────────

  {
    name: 'VERIFY-003 a shorter utterance is a string prefix of a longer one',
    fn: () => {
      // The property longest-prefix matching rests on. `renderSemantic` would fail
      // it: its closing `}]}]}` sits after the text, so no shorter utterance is ever
      // a prefix and the rule silently degrades to whole-string equality.
      const short = turnOf(request([user('Fix the bug')]));
      const long = turnOf(request([user('Fix the bug in parser')]));

      assertEq(short, 'Fix the bug');
      assertTrue(long.startsWith(short), 'longer utterance must extend the shorter one');
    },
  },

  {
    name: 'VERIFY-003 turn reads the LAST user message',
    fn: () => {
      // A conversation carries many user messages; the one being answered is the
      // last. Matching on the first would pin every step of a session to turn one.
      assertEq(turnOf(request([user('first'), assistant('r1'), user('second')])), 'second');
    },
  },

  {
    name: 'VERIFY-003 turn is null when no user message exists',
    fn: () => {
      assertEq(turnOf(request([assistant('r1')])), null);
      assertEq(turnOf(request([])), null);
    },
  },

  {
    name: 'VERIFY-003 turn is not truncated',
    fn: () => {
      // `extractLastUserMsg` sliced at 2000 characters, so two long prompts became
      // identical whenever they differed only past the cut — and a prompt long
      // enough to be truncated is exactly the kind a scenario needs to distinguish.
      const head = 'x'.repeat(2000);
      const a = turnOf(request([user(`${head}ALPHA`)]));
      const b = turnOf(request([user(`${head}BETA`)]));

      assertTrue(a !== b, 'two prompts differing past 2000 chars must not collapse');
      assertEq(a.length, 2005);
    },
  },

  {
    name: 'VERIFY-003 prose can never be confused with a tool call',
    fn: () => {
      // Non-prose parts are tagged with `\u001f`, which prose cannot contain. Without
      // the tag, a scenario declaring the text `fork` would match a tool call to
      // `fork` — content and structure would share one namespace.
      const prose = turnOf(request([user('fork')]));
      const call = turnOf(request([user('u'), toolCall('fork'), user('u2')]));

      assertEq(prose, 'fork');
      assertTrue(!call.includes('\u001f') || call !== prose, 'tool call text must be distinguishable');
    },
  },

  // ── lane comes from the durable binding, never from a guess ──────────────

  {
    name: 'HOST-008 lane resolves through the session binding',
    fn: () => {
      assertEq([...lanesOf(request([user('go')]), BINDINGS)].join(), 'fast-manager');
      assertEq([...lanesOf(request([user('go')], 'ses_real_2'), BINDINGS)].join(), 'coder-after');
    },
  },

  {
    name: 'HOST-008 an unbound session yields no lane, not a guess',
    fn: () => {
      // The mock cannot know which alias an unbound session belongs to. Inventing
      // one would answer a question only the durable association can answer.
      assertEq(lanesOf(request([user('go')], 'ses_unknown'), BINDINGS).size, 0);
      assertEq(lanesOf({ messages: [] }, BINDINGS).size, 0);
      assertEq(lanesOf(request([user('go')]), undefined).size, 0);
    },
  },

  {
    name: 'HOST-008 one session with two aliases yields both, not the first',
    fn: () => {
      // Measured in K9, and it was an impurity hiding inside the function that names the
      // key. Every converted scenario binds two aliases to its primary session — the Host
      // titles a session on the same id it chats on, so
      // `bind = ["inspector-title", "fast-inspector"]` is the norm rather than an edge case.
      //
      // A reverse lookup returning the FIRST match made the answer depend on Map insertion
      // order: same request, same bindings, different lane depending on which alias the
      // driver happened to register first. `process-stress` failed to resolve its title
      // request for exactly this reason.
      const shared = 'ses_shared';
      const titleFirst = new Map([['inspector-title', shared], ['fast-inspector', shared]]);
      const chatFirst = new Map([['fast-inspector', shared], ['inspector-title', shared]]);
      const body = request([user('go')], shared);

      assertEq([...lanesOf(body, titleFirst)].sort().join('|'), 'fast-inspector|inspector-title');
      assertEq(
        [...lanesOf(body, chatFirst)].sort().join('|'),
        [...lanesOf(body, titleFirst)].sort().join('|'),
        'insertion order may not change the answer',
      );
    },
  },

  {
    name: 'HOST-008 kind separates the title entry from the chat entry on one session',
    fn: () => {
      // The consequence of the above: both entries are at the same session and the same
      // step, so `kind` is what tells them apart. That is what it was added to the key for.
      const shared = 'ses_shared';
      const bindings = new Map([['t', shared], ['c', shared]]);
      const entries = [
        { id: 'chat.0', lane: 'c', kind: 'chat', turn: 'Ship it.', step: 0 },
        { id: 'title.0', lane: 't', kind: 'title', turn: 'Ship it.', step: 0 },
      ];

      assertEq(resolveEntry(request([user('Ship it.')], shared), entries, bindings).matched.id, 'chat.0');
      assertEq(
        resolveEntry(titleRequestOn('Ship it.', shared), entries, bindings).matched.id,
        'title.0',
      );
    },
  },

  {
    name: 'VERIFY-003 the session id is a request HEADER, not a body field',
    fn: () => {
      // This case previously asserted the opposite — that a header must be IGNORED — and it
      // was the reason every lane came back unbound on a real request.
      //
      // Measured: `../opencode/packages/opencode/src/session/llm/request.ts:197` sets
      // `x-session-affinity` and `X-Session-Id` on every non-`opencode` provider request,
      // and the body carries no session field at all. These are PRODUCTION headers a real
      // provider receives, so VERIFY-003 permits reading them; what it forbids is harness
      // bookkeeping, and the confusion came from the capture field being named
      // `__testkitHeaders` — the name says harness, the contents are the wire.
      assertEq(sessionIdOf({ __testkitHeaders: { 'x-session-affinity': 'ses_a' } }), 'ses_a');
      assertEq(sessionIdOf({ __testkitHeaders: { 'x-session-id': 'ses_b' } }), 'ses_b');
      assertEq(
        sessionIdOf({ __testkitHeaders: { 'x-opencode-session': 'ses_c' } }),
        'ses_c',
        'the first-party provider branch sets a different header for the same value',
      );

      // Body fields remain a fallback so a unit fixture can build a request without headers.
      // They are not what production sends, which is exactly what this case failed to notice
      // for as long as every fixture used them.
      assertEq(sessionIdOf({ sessionID: 'ses_d' }), 'ses_d');
      assertEq(sessionIdOf({}), null);
    },
  },

  // ── longest prefix wins, and a tie is an author error ────────────────────

  {
    name: 'VERIFY-003 the longest declared prefix wins',
    fn: () => {
      const entries = [
        entry({ id: 'short', turn: 'Fix' }),
        entry({ id: 'long', turn: 'Fix the bug' }),
        entry({ id: 'other', turn: 'Ship it' }),
      ];

      const resolved = resolveEntry(request([user('Fix the bug in parser')]), entries, BINDINGS);
      assertEq(resolved.matched?.id, 'long', 'longest matching prefix, not first or most specific');
    },
  },

  {
    name: 'VERIFY-003 a shorter prefix still wins when the longer one does not match',
    fn: () => {
      const entries = [entry({ id: 'short', turn: 'Fix' }), entry({ id: 'long', turn: 'Fix the bug' })];

      const resolved = resolveEntry(request([user('Fixate on this')]), entries, BINDINGS);
      assertEq(resolved.matched?.id, 'short');
    },
  },

  {
    name: 'VERIFY-003 two same-length prefixes are ambiguous, never scored',
    fn: () => {
      // The replacement for `specificity`. Two declarations of equal length that both
      // match describe one point in the conversation with two different responses, so
      // the scenario does not say what the model does next. Picking one would answer
      // a question the author never answered.
      const entries = [entry({ id: 'x', turn: 'Do it' }), entry({ id: 'y', turn: 'Do it' })];

      const resolved = resolveEntry(request([user('Do it now')]), entries, BINDINGS);

      assertTrue(resolved.matched === undefined, 'a tie must not resolve to a match');
      assertEq(resolved.ambiguousTurn?.length, 2);
      assertEq(
        resolved.ambiguousTurn
          .map((e) => e.id)
          .sort()
          .join(','),
        'x,y',
      );
    },
  },

  {
    name: 'VERIFY-003 step and lane partition the declarations before prefixing',
    fn: () => {
      // Same turn text at two steps is the normal shape of a multi-step turn, and it
      // must not be an ambiguity. The old matcher needed `messageCount` for this.
      const entries = [
        entry({ id: 'step0', turn: 'Do it', step: 0 }),
        entry({ id: 'step1', turn: 'Do it', step: 1 }),
      ];

      assertEq(resolveEntry(request([user('Do it')]), entries, BINDINGS).matched?.id, 'step0');
      assertEq(resolveEntry(request([user('Do it'), assistant('r1')]), entries, BINDINGS).matched?.id, 'step1');

      // And the same turn in another lane is another conversation.
      const laned = [
        entry({ id: 'mgr', turn: 'Do it', lane: 'fast-manager' }),
        entry({ id: 'coder', turn: 'Do it', lane: 'coder-after' }),
      ];
      assertEq(resolveEntry(request([user('Do it')]), laned, BINDINGS).matched?.id, 'mgr');
      assertEq(resolveEntry(request([user('Do it')], 'ses_real_2'), laned, BINDINGS).matched?.id, 'coder');
    },
  },

  {
    name: 'VERIFY-003 nothing declared fails closed with the key that missed',
    fn: () => {
      const entries = [entry({ id: 'only', turn: 'Ship it' })];
      const resolved = resolveEntry(request([user('Something else')]), entries, BINDINGS);

      assertTrue(resolved.matched === undefined, 'no match must not produce a match');
      assertEq(resolved.unmatched?.key.turn, 'Something else');
      assertEq(resolved.unmatched?.key.step, 0);
      assertEq(resolved.unmatched?.key.lane, 'fast-manager');
    },
  },

  {
    name: 'VERIFY-003 a request with no user message matches nothing',
    fn: () => {
      // A bare continuation cannot begin with any declared user text. Admitting it
      // would make every scenario match every synthetic nudge.
      const entries = [entry({ id: 'any', turn: '' })];
      const resolved = resolveEntry(request([assistant('r1')]), entries, BINDINGS);

      assertTrue(resolved.matched === undefined, 'a null turn must not match the empty declaration');
    },
  },

  {
    name: 'VERIFY-003 a lane-less declaration matches any lane',
    fn: () => {
      // Single-lane scenarios should not have to name their only lane. `undefined`
      // means "any", which is different from `null` — the value an unbound session
      // produces — so an unbound session cannot silently match a lane-bound edge.
      const entries = [{ id: 'anywhere', turn: 'Do it', step: 0 }];

      assertEq(resolveEntry(request([user('Do it')]), entries, BINDINGS).matched?.id, 'anywhere');
      assertEq(resolveEntry(request([user('Do it')], 'ses_unbound'), entries, BINDINGS).matched?.id, 'anywhere');

      const bound = [entry({ id: 'mgr-only', turn: 'Do it' })];
      assertTrue(
        resolveEntry(request([user('Do it')], 'ses_unbound'), bound, BINDINGS).matched === undefined,
        'an unbound session must not match a lane-bound declaration',
      );
    },
  },
];
