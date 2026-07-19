/**
 * p0-canary-tests-fallback.js — Fallback / continuation P0 canary tests.
 * Kept under the 300-line Kolmogorov line budget.
 */

import { getSessionId } from '../harness/scenario.js';
import { sleep, TIMEOUTS } from './p0-canary-utils.js';

function expectNoSessionError(t, sid) {
  t.events.expectCount({ type: 'session.error', sessionID: sid, count: 0 });
}

const tests = [
  {
    name: 'OC-FB-001 empty output injects zero-width continuation',
    fn: async (t) => {
      const sess = await t.client.createSession();
      const sid = getSessionId(sess);
      t.provider.expectText({ id: 'fb-empty-output', text: '' });
      const turn = await t.turn.start(sid);
      await t.client.prompt(sid, 'say nothing');

      // The fallback continuation is dispatched as a synthetic prompt containing
      // U+200B. Wait for it rather than relying solely on turn terminal events,
      // because the first idle (empty output) and the continuation idle are
      // separate events and the harness may observe them out of order.
      const deadline = Date.now() + 20000;
      while (
        t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp').length === 0
        && Date.now() < deadline
      ) {
        await sleep(100);
      }
      await turn.awaitTerminal({ timeoutMs: TIMEOUTS.quick });

      const zwsp = t.provider.syntheticRequests.filter((r) => r.marker === 'zwsp');
      if (zwsp.length === 0) throw new Error('Zero-width continuation not dispatched');
      if (zwsp.length > 1) throw new Error(`Expected one zero-width continuation, got ${zwsp.length}`);

      const continuation = zwsp[0];
      const msgs = continuation.body?.messages || [];
      const lastUser = msgs.slice().reverse().find((m) => m?.role === 'user');
      const texts = [];
      if (typeof lastUser?.content === 'string') {
        texts.push(lastUser.content);
      } else if (Array.isArray(lastUser?.content)) {
        for (const p of lastUser.content) {
          if (p?.type === 'text' || typeof p?.text === 'string') texts.push(p.text);
        }
      }
      if (!texts.some((x) => x && x.includes('\u200B'))) {
        throw new Error('Continuation user message does not contain U+200B: ' + JSON.stringify(texts));
      }

      const messages = (await t.client.messages(sid)).data || [];
      const assistantTexts = messages
        .filter((m) => m.info?.role === 'assistant')
        .flatMap((m) => m.parts || [])
        .filter((p) => p.type === 'text' && typeof p.text === 'string')
        .map((p) => p.text);
      if (!assistantTexts.some((text) => text.includes('done'))) {
        throw new Error('Fallback continuation did not produce final assistant response: ' + JSON.stringify(assistantTexts));
      }

      expectNoSessionError(t, sid);
    },
  },
];

export default tests;
