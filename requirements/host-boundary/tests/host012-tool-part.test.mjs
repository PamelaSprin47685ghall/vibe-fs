import test from 'node:test';
import assert from 'node:assert/strict';
import { providerProjection, toList } from '../../verification-system/tests/support/domain.mjs';
const { decodeMessageView, toolResultDigests } = providerProjection;

// VERIFY-007 / HOST-012: Host 1.18.10 的组装形状（message-v2.ts）把 tool 结果
// 放在 assistant 消息的 parts 里：`{ type: "tool-<tool>", state:
// "output-available", toolCallId, input, output }`。Projection 必须把它解码成
// WireToolResult，否则 REVIEW-010 的 seal 永远没有 IncludedToolResultDigests，
// REVIEW-003 的第二次 PERFECT 必拒（实测 dual-PERFECT 全失败）。
test('HOST_012_tool_part_shape_decodes_to_wire_tool_result', () => {
  const view = decodeMessageView(toList([
    {
      info: { role: 'assistant', sessionID: 'ses_1' },
      parts: [
        {
          type: 'tool-verdict',
          state: 'output-available',
          toolCallId: 'call_1',
          input: {},
          output: 'Nope, let us re-evaluate.',
        },
      ],
    },
  ]));
  const digests = toolResultDigests((s) => s, view);
  assert.equal(digests.length, 1);
  assert.equal(digests[0], 'Nope, let us re-evaluate.');
});

// 旧形状（tool-result / tool_result 独立消息）保持兼容。
test('HOST_012_legacy_tool_result_shape_still_decodes', () => {
  const view = decodeMessageView(toList([
    {
      info: { role: 'tool', sessionID: 'ses_1' },
      parts: [{ type: 'tool_result', callID: 'call_1', result: 'ok' }],
    },
  ]));
  const digests = toolResultDigests((s) => s, view);
  assert.equal(digests.length, 1);
  assert.equal(digests[0], 'ok');
});

// errorText 分支（工具失败）同样进入 digest。
test('HOST_012_tool_error_part_enters_digest', () => {
  const view = decodeMessageView(toList([
    {
      info: { role: 'assistant', sessionID: 'ses_1' },
      parts: [{ type: 'tool-read', state: 'output-error', toolCallId: 'c1', input: {}, errorText: 'boom' }],
    },
  ]));
  const digests = toolResultDigests((s) => s, view);
  assert.equal(digests.length, 1);
  assert.equal(digests[0], 'boom');
});
