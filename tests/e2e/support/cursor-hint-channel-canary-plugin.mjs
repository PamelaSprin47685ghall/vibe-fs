import assert from 'node:assert/strict';
import { pathToFileURL } from 'node:url';

const pluginPath = process.env.WANXIANGSHU_CURSOR_CANARY_PLUGIN;
if (!pluginPath) throw new Error('Cursor Pair Hint canary requires WANXIANGSHU_CURSOR_CANARY_PLUGIN');

const production = (await import(pathToFileURL(pluginPath).href)).default;
const separator = '\0\uFEFF';

const terminalText = (part) => {
  if (part?.type !== 'tool') return null;
  if (part?.state?.status === 'completed' && typeof part.state.output === 'string') return part.state.output;
  if (part?.state?.status === 'error' && typeof part.state.error === 'string') return part.state.error;
  if (part?.state?.status === 'error' && typeof part.state.output === 'string') return part.state.output;
  return null;
};

const assertCursorProjection = (output) => {
  if (!Array.isArray(output?.messages)) return;
  const terminal = output.messages
    .flatMap((message) => message?.parts ?? [])
    .map(terminalText)
    .filter((value) => value !== null);
  if (terminal.length === 0) return;

  assert.equal(
    output.messages.some((message) => message?.info?.source === 'pair-programming-auto-injected'),
    false,
    'HOST-013 Cursor projection must not create synthetic messages',
  );
  assert.ok(
    terminal.at(-1).includes(separator),
    'HOST-013 Cursor projection must append NUL+BOM guidance to the real terminal tool result',
  );
};

export default {
  id: 'wanxiangshu-cursor-pair-hint-live-canary',
  async server(input) {
    const hooks = await production.server(input);
    const messagesTransform = hooks['experimental.chat.messages.transform'];
    return {
      ...hooks,
      'experimental.chat.messages.transform': async (hookInput, hookOutput) => {
        await messagesTransform?.(hookInput, hookOutput);
        assertCursorProjection(hookOutput);
      },
    };
  },
};
