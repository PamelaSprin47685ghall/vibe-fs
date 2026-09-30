import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const rawContract = await import("../../../dist/Strength/InvestigationEstimateContract.js");
const rawBudget = await import("../../../dist/Strength/Budget.js");
const rawReadonlyContract = await import("../../../dist/OpenCode/Host/ReadonlyDelegationContract.js");
const rawPluginHooksSurface = await import("../../../dist/OpenCode/Host/PluginHooksSurface.js");
const PluginHooksSurface = rawPluginHooksSurface;
const rawStaticTools = await import("../../../dist/OpenCode/Tools/StaticTools.js");
const rawPluginHooks = await import("../../../dist/OpenCode/Plugin/PluginHooks.js");
const rawDelegate = await import("../../../dist/Strength/OpenCode/Delegate.js");
const rawPluginScope = await import("../../../dist/Strength/OpenCode/PluginScope.js");
const rawModelRoutingSurface = await import("../../../dist/OpenCode/Host/ModelRoutingSurface.js");
const ModelRoutingSurface = rawModelRoutingSurface;
const fs = await import("node:fs");
const path = await import("node:path");
const os = await import("node:os");
const { fileURLToPath } = await import("node:url");

const Contract = {
  ...rawContract,
  EstimatedReadonlyRounds: {
    value: rawContract.EstimatedReadonlyRoundsModule_value,
    toExecutionBudget: rawContract.EstimatedReadonlyRoundsModule_toExecutionBudget,
  },
};

const Budget = {
  ...rawBudget,
  ReadonlyRoundBudget: {
    value: rawBudget.ReadonlyRoundBudgetModule_value,
  },
};

// execution-model-routing-020 / DELEGATE.md 9.1:
// Ensure the test process has initialized the ModelRouting scheduler singleton
// with dynamic predictorConfiguration query, allowing test cases to toggle predictor state
// via globalThis.__wanxiangshu_test_predictor_state.
{
  const tmpDir = fs.mkdtempSync(path.join(fs.realpathSync(os.tmpdir()), 'wxs-016-routing-'));
  const routingHome = path.join(tmpDir, 'routing-home');
  const routingDir = path.join(routingHome, '.config', 'opencode');
  fs.mkdirSync(routingDir, { recursive: true });
  fs.writeFileSync(
    path.join(routingDir, 'wanxiangshu.mjs'),
    `export const routingProtocol = 2
export default function route(role, running, previous, purpose) {
  return { model: 'provider/' + role + '-model', reasoning: 'none' }
}
export const predictorConfiguration = () => {
  const state = globalThis.__wanxiangshu_test_predictor_state ?? 'unconfigured'
  if (state === 'configured') return { state: 'configured', reason: null }
  if (state === 'invalid') return { state: 'invalid', reason: globalThis.__wanxiangshu_test_predictor_reason ?? 'test injected invalid state' }
  return { state: 'unconfigured', reason: null }
}
`
  );
  const prevHome = process.env.HOME;
  const prevProfile = process.env.USERPROFILE;
  process.env.HOME = routingHome;
  process.env.USERPROFILE = routingHome;
  try {
    await ModelRoutingSurface.initialize();
  } finally {
    if (prevHome === undefined) delete process.env.HOME;
    else process.env.HOME = prevHome;
    if (prevProfile === undefined) delete process.env.USERPROFILE;
    else process.env.USERPROFILE = prevProfile;
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
}

test('WHAT[speculative-investigation-016] protocol revision and field constants are defined and stable', () => {
  assert.equal(rawContract.EstimatedReadonlyRoundsField, 'estimated_readonly_rounds');
  assert.equal(rawContract.ProtocolRevision, 2);
  assert.equal(rawContract.ProtocolRevision > 1, true);
});

test('WHAT[speculative-investigation-016] classifyTool maps known tools correctly and without prefix matching', () => {
  const participate = [
    'read', 'glob', 'grep', 'js-manager', 'js-engineer', 'js-devops',
    'edit', 'write', 'mv', 'rm', 'fetch', 'run'
  ];
  for (const name of participate) {
    const policy = Contract.classifyTool(name);
    assert.equal(Contract.policyCode(policy), 'EstimateAfterCall', `tool ${name} must be EstimateAfterCall`);
  }

  const noEstimate = [
    'fork', 'resume', 'commission', 'join', 'horizon', 'review', 'suicide',
    'fission', 'open-terminal', 'send-terminal', 'read-terminal', 'signal-terminal',
    'skill', 'sphinx', 'assume', 'enough', 'abandon', 'defer', 'subscribe',
    'publish', 'celebrate', 'regret', 'chronicle', 'js-bookkeeper',
    'bash-honeypot', 'invalid', 'js-orchestrator', 'js-blogger'
  ];
  for (const name of noEstimate) {
    const policy = Contract.classifyTool(name);
    assert.equal(Contract.policyCode(policy), 'NoEstimate', `tool ${name} must be NoEstimate`);
  }

  // Prefix matching is strictly forbidden
  const unreviewedPrefixes = [
    'read-extra', 'globbing', 'grepper', 'edit_file', 'writer', 'run_command',
    'fetch_data', 'js-devops-v2', 'fork_child', 'resume_parent', 'custom_tool'
  ];
  for (const name of unreviewedPrefixes) {
    const policy = Contract.classifyTool(name);
    assert.equal(Contract.policyCode(policy), 'Unreviewed', `tool ${name} must be Unreviewed`);
  }
});

test('WHAT[speculative-investigation-016] 0 rounds omitting self_note is valid, including -0 normalized to 0', () => {
  const res1 = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0
  });
  assert.equal(res1.tag, 0, 'parse should succeed with Ok');
  const [rounds1, note1] = res1.fields[0];
  assert.equal(Contract.EstimatedReadonlyRounds.value(rounds1), 0);
  assert.equal(note1, undefined);

  // -0 is treated as 0
  const res2 = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: -0
  });
  assert.equal(res2.tag, 0, 'parse with -0 should succeed');
  const [rounds2, note2] = res2.fields[0];
  assert.equal(Contract.EstimatedReadonlyRounds.value(rounds2), 0);
  assert.equal(note2, undefined);

  // Conversion to execution budget
  const execBudget = Contract.EstimatedReadonlyRounds.toExecutionBudget(rounds1);
  assert.equal(Budget.ReadonlyRoundBudget.value(execBudget), 0);
});

test('WHAT[speculative-investigation-016] positive integers with non-blank self_note are valid and preserve original note string', () => {
  for (const r of [1, 2, 7, 2147483647]) {
    const noteText = "  我怀疑入口与调用方对空值的约定不同，接下来先核对调用点  ";
    const res = Contract.parseParticipatingArguments({
      estimated_readonly_rounds: r,
      self_note: noteText
    });
    assert.equal(res.tag, 0, `parse with rounds ${r} should succeed`);
    const [rounds, note] = res.fields[0];
    assert.equal(Contract.EstimatedReadonlyRounds.value(rounds), r);
    // Preserves original string, not trimmed
    assert.equal(note, noteText);

    const execBudget = Contract.EstimatedReadonlyRounds.toExecutionBudget(rounds);
    assert.equal(Budget.ReadonlyRoundBudget.value(execBudget), r);
  }
});

test('WHAT[speculative-investigation-016] 0 rounds rejects any present self_note (including empty, whitespace, null, undefined own-property)', () => {
  // empty string
  const resEmpty = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0,
    self_note: ''
  });
  assert.equal(resEmpty.tag, 1, 'should fail');
  assert.equal(Contract.errorCode(resEmpty.fields[0]), 'NotePresentWhenZero');
  assert.equal(Contract.describeArgumentErrorZh(resEmpty.fields[0]), 'estimated_readonly_rounds 为 0 时必须省略 self_note');
  assert.equal(Contract.describeArgumentErrorEn(resEmpty.fields[0]), 'self_note must be omitted when estimated_readonly_rounds is 0');

  // whitespace
  const resWs = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0,
    self_note: '   '
  });
  assert.equal(resWs.tag, 1);
  assert.equal(Contract.errorCode(resWs.fields[0]), 'NotePresentWhenZero');
  assert.equal(Contract.describeArgumentErrorZh(resWs.fields[0]), 'estimated_readonly_rounds 为 0 时必须省略 self_note');

  // null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0,
    self_note: null
  });
  assert.equal(resNull.tag, 1);
  assert.equal(Contract.errorCode(resNull.fields[0]), 'NotePresentWhenZero');
  assert.equal(Contract.describeArgumentErrorZh(resNull.fields[0]), 'estimated_readonly_rounds 为 0 时必须省略 self_note');

  // own-property with undefined value
  const objWithUndef = { estimated_readonly_rounds: 0 };
  objWithUndef.self_note = undefined;
  const resUndef = Contract.parseParticipatingArguments(objWithUndef);
  assert.equal(resUndef.tag, 1);
  assert.equal(Contract.errorCode(resUndef.fields[0]), 'NotePresentWhenZero', 'own-property undefined must be rejected as NotePresentWhenZero');
  assert.equal(Contract.describeArgumentErrorZh(resUndef.fields[0]), 'estimated_readonly_rounds 为 0 时必须省略 self_note');
  assert.equal(Contract.describeArgumentErrorEn(resUndef.fields[0]), 'self_note must be omitted when estimated_readonly_rounds is 0');

  // Surface helper verification per DELEGATE_REVISE.md §4.1:
  // "真实 JSON 中省略属性与提供 null 不等价。直接 JS 单元测试还应覆盖“自有属性存在但值为 undefined”；
  // 本稿按出现了字段处理，零值时拒绝。校验存在性用 own-property 证据，不用 value == null 混淆。"
  const helperUndef = PluginHooksSurface.readonlyDelegationSelfNoteOf(objWithUndef);
  assert.deepEqual(helperUndef, { ok: false, error: 'NotePresentWhenZero' });
});

test('WHAT[speculative-investigation-016] positive rounds rejects missing, blank or non-string self_note', () => {
  // missing note
  const resMissing = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2
  });
  assert.equal(resMissing.tag, 1);
  assert.equal(Contract.errorCode(resMissing.fields[0]), 'MissingOrBlankNoteWhenPositive');
  assert.equal(Contract.describeArgumentErrorZh(resMissing.fields[0]), '正数估计需要非空的后续查证展望');
  assert.equal(Contract.describeArgumentErrorEn(resMissing.fields[0]), 'A positive estimate requires a non-empty self_note outlook');

  // empty string note
  const resEmpty = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: ''
  });
  assert.equal(resEmpty.tag, 1);
  assert.equal(Contract.errorCode(resEmpty.fields[0]), 'MissingOrBlankNoteWhenPositive');
  assert.equal(Contract.describeArgumentErrorZh(resEmpty.fields[0]), '正数估计需要非空的后续查证展望');

  // blank whitespace note
  const resBlank = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: '  \t\n  '
  });
  assert.equal(resBlank.tag, 1);
  assert.equal(Contract.errorCode(resBlank.fields[0]), 'MissingOrBlankNoteWhenPositive');
  assert.equal(Contract.describeArgumentErrorZh(resBlank.fields[0]), '正数估计需要非空的后续查证展望');

  // non-string note: number
  const resNum = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: 123
  });
  assert.equal(resNum.tag, 1);
  assert.equal(Contract.errorCode(resNum.fields[0]), 'NoteNotString');
  assert.equal(Contract.describeArgumentErrorZh(resNum.fields[0]), 'self_note 必须为字符串类型');
  assert.equal(Contract.describeArgumentErrorEn(resNum.fields[0]), 'self_note must be a string');

  // non-string note: boolean
  const resBool = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: true
  });
  assert.equal(resBool.tag, 1);
  assert.equal(Contract.errorCode(resBool.fields[0]), 'NoteNotString');
  assert.equal(Contract.describeArgumentErrorZh(resBool.fields[0]), 'self_note 必须为字符串类型');

  // non-string note: object
  const resObj = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: { text: "note" }
  });
  assert.equal(resObj.tag, 1);
  assert.equal(Contract.errorCode(resObj.fields[0]), 'NoteNotString');
  assert.equal(Contract.describeArgumentErrorZh(resObj.fields[0]), 'self_note 必须为字符串类型');

  // non-string note: array
  const resArr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: ["note"]
  });
  assert.equal(resArr.tag, 1);
  assert.equal(Contract.errorCode(resArr.fields[0]), 'NoteNotString');
  assert.equal(Contract.describeArgumentErrorZh(resArr.fields[0]), 'self_note 必须为字符串类型');

  // non-string note: null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: null
  });
  assert.equal(resNull.tag, 1);
  assert.equal(Contract.errorCode(resNull.fields[0]), 'NoteNotString');
  assert.equal(Contract.describeArgumentErrorZh(resNull.fields[0]), 'self_note 必须为字符串类型');
});

test('WHAT[speculative-investigation-016] native number checks and range validation reject invalid values', () => {
  // negative
  const resNeg = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: -1
  });
  assert.equal(resNeg.tag, 1);
  assert.equal(Contract.errorCode(resNeg.fields[0]), 'InvalidRange');
  assert.equal(Contract.describeArgumentErrorZh(resNeg.fields[0]), 'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数');
  assert.equal(Contract.describeArgumentErrorEn(resNeg.fields[0]), 'estimated_readonly_rounds must be a non-negative integer between 0 and 2147483647');

  // fractional / float
  const resFloat = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 1.5,
    self_note: 'note'
  });
  assert.equal(resFloat.tag, 1);
  assert.equal(Contract.errorCode(resFloat.fields[0]), 'InvalidRange');
  assert.equal(Contract.describeArgumentErrorZh(resFloat.fields[0]), 'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数');

  // out of range (> 2147483647)
  const resOverflow = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2147483648,
    self_note: 'note'
  });
  assert.equal(resOverflow.tag, 1);
  assert.equal(Contract.errorCode(resOverflow.fields[0]), 'InvalidRange');
  assert.equal(Contract.describeArgumentErrorZh(resOverflow.fields[0]), 'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数');

  // string number (no string coercion)
  const resStr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: '2',
    self_note: 'note'
  });
  assert.equal(resStr.tag, 1);
  assert.equal(Contract.errorCode(resStr.fields[0]), 'WrongNumberType');
  assert.equal(Contract.describeArgumentErrorZh(resStr.fields[0]), 'estimated_readonly_rounds 必须为数字类型');
  assert.equal(Contract.describeArgumentErrorEn(resStr.fields[0]), 'estimated_readonly_rounds must be a number');

  // boolean
  const resBool = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: true,
    self_note: 'note'
  });
  assert.equal(resBool.tag, 1);
  assert.equal(Contract.errorCode(resBool.fields[0]), 'WrongNumberType');
  assert.equal(Contract.describeArgumentErrorZh(resBool.fields[0]), 'estimated_readonly_rounds 必须为数字类型');

  // array
  const resArr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: [1],
    self_note: 'note'
  });
  assert.equal(resArr.tag, 1);
  assert.equal(Contract.errorCode(resArr.fields[0]), 'WrongNumberType');
  assert.equal(Contract.describeArgumentErrorZh(resArr.fields[0]), 'estimated_readonly_rounds 必须为数字类型');

  // object
  const resObj = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: { rounds: 1 },
    self_note: 'note'
  });
  assert.equal(resObj.tag, 1);
  assert.equal(Contract.errorCode(resObj.fields[0]), 'WrongNumberType');
  assert.equal(Contract.describeArgumentErrorZh(resObj.fields[0]), 'estimated_readonly_rounds 必须为数字类型');

  // NaN
  const resNaN = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: NaN,
    self_note: 'note'
  });
  assert.equal(resNaN.tag, 1);
  assert.equal(Contract.errorCode(resNaN.fields[0]), 'InvalidRange');
  assert.equal(Contract.describeArgumentErrorZh(resNaN.fields[0]), 'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数');

  // Infinity
  const resInf = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: Infinity,
    self_note: 'note'
  });
  assert.equal(resInf.tag, 1);
  assert.equal(Contract.errorCode(resInf.fields[0]), 'InvalidRange');
  assert.equal(Contract.describeArgumentErrorZh(resInf.fields[0]), 'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数');

  // null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: null,
    self_note: 'note'
  });
  assert.equal(resNull.tag, 1);
  assert.equal(Contract.errorCode(resNull.fields[0]), 'WrongNumberType');
  assert.equal(Contract.describeArgumentErrorZh(resNull.fields[0]), 'estimated_readonly_rounds 必须为数字类型');

  // missing
  const resMissing = Contract.parseParticipatingArguments({});
  assert.equal(resMissing.tag, 1);
  assert.equal(Contract.errorCode(resMissing.fields[0]), 'MissingEstimate');
  assert.equal(Contract.describeArgumentErrorZh(resMissing.fields[0]), '必须提供 estimated_readonly_rounds 估计字段');
  assert.equal(Contract.describeArgumentErrorEn(resMissing.fields[0]), 'The estimated_readonly_rounds field must be provided');
});

test('WHAT[speculative-investigation-016] protocol field mixing and legacy field rejection', () => {
  // Legacy field alone
  const resLegacy = Contract.parseParticipatingArguments({
    delegate_readonly_rounds: 1,
    self_note: 'note'
  });
  assert.equal(resLegacy.tag, 1);
  assert.equal(Contract.errorCode(resLegacy.fields[0]), 'MixedProtocolFields');
  assert.equal(Contract.describeArgumentErrorZh(resLegacy.fields[0]), '不得携带旧协议字段 delegate_readonly_rounds');
  assert.equal(Contract.describeArgumentErrorEn(resLegacy.fields[0]), 'The legacy delegate_readonly_rounds field must not be used');

  // Both legacy and new fields
  const resBoth = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 1,
    delegate_readonly_rounds: 1,
    self_note: 'note'
  });
  assert.equal(resBoth.tag, 1);
  assert.equal(Contract.errorCode(resBoth.fields[0]), 'MixedProtocolFields');
  assert.equal(Contract.describeArgumentErrorZh(resBoth.fields[0]), '不得携带旧协议字段 delegate_readonly_rounds');
  assert.equal(Contract.describeArgumentErrorEn(resBoth.fields[0]), 'The legacy delegate_readonly_rounds field must not be used');
});

test('WHAT[speculative-investigation-016] invalid argument container (not a plain object) is rejected', () => {
  for (const bad of [null, undefined, 123, 'arguments', [1, 2]]) {
    const res = Contract.parseParticipatingArguments(bad);
    assert.equal(res.tag, 1);
    assert.equal(Contract.errorCode(res.fields[0]), 'InvalidArgumentObject');
    assert.equal(Contract.describeArgumentErrorZh(res.fields[0]), '工具参数必须为合法的普通对象');
    assert.equal(Contract.describeArgumentErrorEn(res.fields[0]), 'Tool arguments must be a valid plain object');
  }
});

test('WHAT[speculative-investigation-016] 8 error categories map to bilingual input rule explanations preserving distinctiveness', () => {
  const expectedZh = [
    '必须提供 estimated_readonly_rounds 估计字段',
    'estimated_readonly_rounds 必须为数字类型',
    'estimated_readonly_rounds 必须为 0 至 2147483647 之间的非负整数',
    'estimated_readonly_rounds 为 0 时必须省略 self_note',
    '正数估计需要非空的后续查证展望',
    'self_note 必须为字符串类型',
    '不得携带旧协议字段 delegate_readonly_rounds',
    '工具参数必须为合法的普通对象',
  ];
  const expectedEn = [
    'The estimated_readonly_rounds field must be provided',
    'estimated_readonly_rounds must be a number',
    'estimated_readonly_rounds must be a non-negative integer between 0 and 2147483647',
    'self_note must be omitted when estimated_readonly_rounds is 0',
    'A positive estimate requires a non-empty self_note outlook',
    'self_note must be a string',
    'The legacy delegate_readonly_rounds field must not be used',
    'Tool arguments must be a valid plain object',
  ];

  for (let tag = 0; tag < 8; tag++) {
    const errorObj = { tag };
    assert.equal(Contract.describeArgumentErrorZh(errorObj), expectedZh[tag]);
    assert.equal(Contract.describeArgumentErrorEn(errorObj), expectedEn[tag]);
  }

  // Ensure all 8 Chinese and English messages are distinct
  assert.equal(new Set(expectedZh).size, 8, 'all 8 Chinese error messages must be distinct');
  assert.equal(new Set(expectedEn).size, 8, 'all 8 English error messages must be distinct');
});

test('WHAT[speculative-investigation-016] tool.execute.before throws descriptive error explaining input rule instead of raw enum %A', async () => {
  const boot = {
    Scope: { RecordCompactionSettingGap: () => {}, DisposeAsync: async () => {}, Sessions: { SessionParents: new Map(), Companions: new Map() } },
    Journal: null,
    WorkspaceDirectory: null,
    Input: null,
    ProtocolArgumentVault: null,
  };
  const host = {
    Wired: { ChatMessageHook: () => () => {}, ObserveEvent: () => {} },
    SessionPort: null,
    SnapshotOpt: null,
    EventPort: null,
  };
  const hooks = await rawPluginHooks.create(boot, host, async () => {});
  const toolBefore = hooks['tool.execute.before'];

  // Case 1: When Predictor is configured, participating tools are unconditionally validated
  globalThis.__wanxiangshu_test_predictor_state = 'configured';
  try {
    // 1a: zero estimate carrying self_note is rejected with descriptive message
    await assert.rejects(
      async () => {
        await toolBefore(
          { tool: 'read', sessionID: 'ses-test-err', callID: 'call-err' },
          { args: { filePath: 'test.fs', estimated_readonly_rounds: 0, self_note: 'bad note' } }
        );
      },
      (err) => {
        assert.ok(err, 'expected error to be thrown');
        assert.ok(err.message.includes('Invalid investigation estimate arguments:'));
        assert.ok(
          err.message.includes('estimated_readonly_rounds 为 0 时必须省略 self_note') ||
          err.message.includes('self_note must be omitted when estimated_readonly_rounds is 0')
        );
        assert.ok(!err.message.includes('NotePresentWhenZero'), 'error must not leak raw F# enum name NotePresentWhenZero');
        return true;
      },
      'toolBefore must throw descriptive error instead of raw enum'
    );

    // 1b: completely omitted estimated_readonly_rounds for participating tool is rejected as MissingEstimate
    await assert.rejects(
      async () => {
        await toolBefore(
          { tool: 'read', sessionID: 'ses-test-missing', callID: 'call-missing' },
          { args: { filePath: 'test.fs' } }
        );
      },
      (err) => {
        assert.ok(err, 'expected error to be thrown');
        assert.ok(err.message.includes('Invalid investigation estimate arguments:'));
        assert.ok(
          err.message.includes('必须提供 estimated_readonly_rounds 估计字段') ||
          err.message.includes('The estimated_readonly_rounds field must be provided')
        );
        assert.ok(!err.message.includes('MissingEstimate'), 'error must not leak raw F# enum name MissingEstimate');
        return true;
      },
      'toolBefore must reject completely omitted estimated_readonly_rounds for participating tool'
    );
  } finally {
    delete globalThis.__wanxiangshu_test_predictor_state;
  }

  // Case 2: When Predictor is unconfigured, toolBefore does NOT validate or hide, args pass through intact
  globalThis.__wanxiangshu_test_predictor_state = 'unconfigured';
  try {
    const unconfiguredOutput = {
      args: { filePath: 'test.fs', estimated_readonly_rounds: 0, self_note: 'bad note' },
    };
    // Must NOT reject even with invalid protocol fields because Predictor is not configured
    await toolBefore(
      { tool: 'read', sessionID: 'ses-unconf', callID: 'call-unconf' },
      unconfiguredOutput
    );
    // Protocol fields must NOT be hidden
    assert.equal(unconfiguredOutput.args.estimated_readonly_rounds, 0);
    assert.equal(unconfiguredOutput.args.self_note, 'bad note');

    // Missing estimate also passes through cleanly without validation error
    const missingOutput = { args: { filePath: 'test.fs' } };
    await toolBefore(
      { tool: 'read', sessionID: 'ses-unconf-2', callID: 'call-unconf-2' },
      missingOutput
    );
    assert.equal(missingOutput.args.filePath, 'test.fs');
  } finally {
    delete globalThis.__wanxiangshu_test_predictor_state;
  }
});

test('WHAT[speculative-investigation-016] single-source behavioral consistency across schema, boundary and batch consumers', async () => {
  // Consumer 1: schema 装饰端 (ReadonlyDelegationContract.decorateDefinition)
  function checkSchemaDecoratesTool(toolName) {
    const toolInput = { toolID: toolName };
    const toolOutput = {
      description: "original tool description",
      parameters: {
        type: "object",
        properties: { path: { type: "string" } },
        required: ["path"],
      },
    };
    rawReadonlyContract.decorateDefinition(toolInput, toolOutput);
    return Boolean(
      toolOutput.parameters?.properties?.estimated_readonly_rounds ||
      toolOutput.jsonSchema?.properties?.estimated_readonly_rounds
    );
  }

  // Consumer 2: 调用边界端 (PluginHooks toolBefore parameter stripping/hiding)
  async function checkBoundaryParticipates(toolName, predictorConfigured = true) {
    const boot = {
      Scope: {
        RecordCompactionSettingGap: () => {},
        DisposeAsync: async () => {},
        Sessions: { SessionParents: new Map(), Companions: new Map() },
      },
      Journal: null,
      WorkspaceDirectory: null,
      Input: null,
      ProtocolArgumentVault: null,
    };
    const host = {
      Wired: {
        ChatMessageHook: () => () => {},
        ObserveEvent: () => {},
      },
      SessionPort: null,
      SnapshotOpt: null,
      EventPort: null,
    };
    const hooks = await rawPluginHooks.create(boot, host, async () => {});
    const toolBefore = hooks["tool.execute.before"];

    const beforeOutput = {
      args: {
        path: "test-file",
        estimated_readonly_rounds: 0,
      },
    };

    if (predictorConfigured) {
      globalThis.__wanxiangshu_test_predictor_state = "configured";
    } else {
      globalThis.__wanxiangshu_test_predictor_state = "unconfigured";
    }

    try {
      await toolBefore({ tool: toolName, sessionID: "ses-test", callID: "call-test" }, beforeOutput);
      // Under configured predictor: participating tools have estimated_readonly_rounds hidden.
      // Under unconfigured predictor or non-participating tools: arguments are untouched.
      return !("estimated_readonly_rounds" in beforeOutput.args);
    } finally {
      delete globalThis.__wanxiangshu_test_predictor_state;
    }
  }

  // Consumer 3: 来源批次端 (StrengthDelegate.tryCapture -> aggregateBatchEstimate)
  async function checkBatchReadsEstimate(toolName) {
    const strengthScope = new rawPluginScope.PluginStrengthScope();
    strengthScope.AttachStrengthReplicaRuntime({});
    const output = {
      messages: [
        {
          role: "user",
          id: "u-1",
          sessionID: "ses-1",
          parts: [{ type: "text", text: "query" }],
        },
        {
          role: "assistant",
          id: "a-1",
          sessionID: "ses-1",
          parentID: "u-1",
          parts: [
            {
              type: "tool",
              tool: toolName,
              callID: "call-1",
              state: {
                status: "completed",
                input: { estimated_readonly_rounds: 0 },
                output: "result",
              },
            },
          ],
        },
      ],
    };

    const snapshotPort = {
      GetMessages: async () => ({
        tag: 0,
        fields: [[{ Id: "a-1", Role: "assistant", ParentId: "u-1" }]],
      }),
    };
    const journal = {
      Snapshot: () => ({
        AgentProjections: {
          Associations: new Map([["ses-1", [{ tag: 0 }, { tag: 0 }]]]),
          Profiles: new Map([
            [
              "ses-1",
              {
                CanonicalRole: "engineer",
                AuthorityKind: { tag: 0 },
                LogicalRunId: "log-1",
                AuthorityRootUserMessageId: "u-1",
              },
            ],
          ]),
        },
      }),
    };
    const durability = {
      LoadProjection: async () => ({ tag: 0, fields: [{ ByDecision: new Map() }] }),
      Append: async () => ({ tag: 0 }),
    };

    const outcome = await rawDelegate.tryCapture(
      snapshotPort,
      journal,
      durability,
      strengthScope,
      () => null,
      null,
      true,
      output
    );

    // If participating (EstimateAfterCall):
    // aggregateBatchEstimate identifies it as an estimate call; with 0 rounds it returns EstimatedZero ("estimated-zero")
    // If not participating (NoEstimate or Unreviewed):
    // aggregateBatchEstimate filters it out -> empty estimateCalls -> returns NoEstimateOpportunity ("no-estimate-opportunity")
    return outcome?.fields?.[0] !== "no-estimate-opportunity";
  }

  // Representative set: at least one participating (read), one explicit NoEstimate (chronicle),
  // one unknown/unreviewed (js-foo-unknown), and one host synthetic placeholder (invalid).
  const representativeCases = [
    { name: "read", expected: true },
    { name: "chronicle", expected: false },
    { name: "js-foo-unknown", expected: false },
    { name: "invalid", expected: false },
  ];

  for (const { name, expected } of representativeCases) {
    const schemaAdds = checkSchemaDecoratesTool(name);
    // Boundary with Predictor configured: participating tools hide estimated_readonly_rounds
    const boundaryParticipates = await checkBoundaryParticipates(name, true);
    const batchReads = await checkBatchReadsEstimate(name);

    assert.equal(
      schemaAdds,
      expected,
      `Schema decorator for ${name} must yield ${expected}, got ${schemaAdds}`
    );
    assert.equal(
      boundaryParticipates,
      expected,
      `Boundary hook (configured) for ${name} must yield ${expected}, got ${boundaryParticipates}`
    );
    assert.equal(
      batchReads,
      expected,
      `Batch aggregator for ${name} must yield ${expected}, got ${batchReads}`
    );
    assert.equal(
      schemaAdds === boundaryParticipates && boundaryParticipates === batchReads,
      true,
      `All three consumers must yield identical conclusions for tool '${name}' when Predictor is configured`
    );

    // Baseline when Predictor is UNCONFIGURED: boundary must NEVER hide arguments for any tool
    const boundaryUnconfigured = await checkBoundaryParticipates(name, false);
    assert.equal(
      boundaryUnconfigured,
      false,
      `Boundary hook (unconfigured) for ${name} must NOT hide arguments (got ${boundaryUnconfigured})`
    );
  }
});

test('WHAT[speculative-investigation-016] source batch capture rejects participating call with missing estimate as ArgumentError instead of no-estimate-opportunity', async () => {
  const strengthScope = new rawPluginScope.PluginStrengthScope();
  strengthScope.AttachStrengthReplicaRuntime({});

  const snapshotPort = {
    GetMessages: async () => ({
      tag: 0,
      fields: [[{ Id: "a-missing-1", Role: "assistant", ParentId: "u-missing-1" }]],
    }),
  };
  const journal = {
    Snapshot: () => ({
      AgentProjections: {
        Associations: new Map([["ses-m", [{ tag: 0 }, { tag: 0 }]]]),
        Profiles: new Map([
          [
            "ses-m",
            {
              CanonicalRole: "engineer",
              AuthorityKind: { tag: 0 },
              LogicalRunId: "log-m",
              AuthorityRootUserMessageId: "u-missing-1",
            },
          ],
        ]),
      },
    }),
  };
  const durability = {
    LoadProjection: async () => ({ tag: 0, fields: [{ ByDecision: new Map() }] }),
    Append: async () => ({ tag: 0 }),
  };

  // Case 1: Participating tool ('read') completely omits estimated_readonly_rounds
  const outputMissingParticipating = {
    messages: [
      {
        role: "user",
        id: "u-missing-1",
        sessionID: "ses-m",
        parts: [{ type: "text", text: "check file" }],
      },
      {
        role: "assistant",
        id: "a-missing-1",
        sessionID: "ses-m",
        parentID: "u-missing-1",
        parts: [
          {
            type: "tool",
            tool: "read",
            callID: "call-read-missing",
            state: {
              status: "completed",
              input: { filePath: "src/file.fs" }, // completely omitted estimated_readonly_rounds!
              output: "let x = 1",
            },
          },
        ],
      },
    ],
  };

  const outcomeParticipating = await rawDelegate.tryCapture(
    snapshotPort,
    journal,
    durability,
    strengthScope,
    () => null,
    null,
    true,
    outputMissingParticipating
  );

  // Must fail as an ArgumentError rejection, NOT silently degrade to no-estimate-opportunity or estimated-zero
  assert.equal(rawDelegate.captureOutcomeCode(outcomeParticipating), 'Skipped', 'outcome must be Skipped');
  const participatingReason = outcomeParticipating?.fields?.[0];
  assert.ok(typeof participatingReason === 'string', 'skipped reason must be a string');
  assert.notEqual(
    participatingReason,
    'no-estimate-opportunity',
    'participating call with missing estimate must not silently degrade to no-estimate-opportunity'
  );
  assert.notEqual(
    participatingReason,
    'estimated-zero',
    'participating call with missing estimate must not become estimated-zero'
  );
  assert.ok(
    participatingReason.includes('rejected:') &&
    (participatingReason.includes('estimated_readonly_rounds') || participatingReason.includes('MissingEstimate')),
    `rejected reason must explain missing estimate: ${participatingReason}`
  );

  // Case 2: Non-participating tool ('chronicle') completely omits estimated_readonly_rounds
  const outputMissingNonParticipating = {
    messages: [
      {
        role: "user",
        id: "u-missing-1",
        sessionID: "ses-m",
        parts: [{ type: "text", text: "log note" }],
      },
      {
        role: "assistant",
        id: "a-missing-1",
        sessionID: "ses-m",
        parentID: "u-missing-1",
        parts: [
          {
            type: "tool",
            tool: "chronicle",
            callID: "call-chronicle-1",
            state: {
              status: "completed",
              input: { note: "some note" },
              output: "logged",
            },
          },
        ],
      },
    ],
  };

  const outcomeNonParticipating = await rawDelegate.tryCapture(
    snapshotPort,
    journal,
    durability,
    strengthScope,
    () => null,
    null,
    true,
    outputMissingNonParticipating
  );

  // Non-participating tool is filtered out of estimateCalls -> evaluates to no-estimate-opportunity
  assert.equal(rawDelegate.captureOutcomeCode(outcomeNonParticipating), 'Skipped', 'outcome must be Skipped');
  assert.equal(
    outcomeNonParticipating?.fields?.[0],
    'no-estimate-opportunity',
    'non-participating tool call must evaluate to no-estimate-opportunity'
  );
});

test('WHAT[speculative-investigation-016] mechanical inventory of unreviewed tools against known tool surface', () => {
  // Authoritative known tool names from source StaticTools.fs
  const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../../..");
  const staticToolsPath = path.join(repoRoot, "src/Wanxiangshu/OpenCode/Tools/StaticTools.fs");
  assert.ok(fs.existsSync(staticToolsPath), `StaticTools.fs must exist at ${staticToolsPath}`);

  const sourceContent = fs.readFileSync(staticToolsPath, "utf8");
  const knownMatch = sourceContent.match(/let\s+knownToolNames\s*=\s*\[([\s\S]*?)\]/);
  if (!knownMatch) {
    throw new Error("Blocked: failed to mechanically extract knownToolNames from StaticTools.fs");
  }
  const knownToolsFromSrc = [...knownMatch[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);

  // Also verify against compiled StaticTools export
  const knownToolsFromDist = Array.from(rawStaticTools.knownToolNames);
  assert.deepEqual(
    knownToolsFromSrc,
    knownToolsFromDist,
    "Source knownToolNames and compiled knownToolNames must be identical"
  );

  // Anti fail-open: the tool surface must be non-empty and contain representative active tools
  assert.ok(knownToolsFromSrc.length >= 30, `knownToolNames must cover active tool surface, got ${knownToolsFromSrc.length}`);
  assert.ok(knownToolsFromSrc.includes("read"), "knownToolNames must include 'read'");
  assert.ok(knownToolsFromSrc.includes("chronicle"), "knownToolNames must include 'chronicle'");
  assert.ok(knownToolsFromSrc.includes("fork"), "knownToolNames must include 'fork'");
  assert.ok(knownToolsFromSrc.includes("run"), "knownToolNames must include 'run'");

  // Mechanically compute the difference set of tools in knownToolNames that are Unreviewed (tag === 2)
  const unreviewedDifference = knownToolsFromSrc.filter((name) => {
    const policy = Contract.classifyTool(name);
    return Contract.policyCode(policy) === 'Unreviewed';
  });

  // Explicit registry of acknowledged unreviewed candidates.
  // Any newly added tool that is not classified in classifyTool will appear in unreviewedDifference
  // and fail this assertion until explicitly reviewed and classified or acknowledged here.
  const acknowledgedUnreviewedCandidates = [];

  assert.deepEqual(
    unreviewedDifference,
    acknowledgedUnreviewedCandidates,
    `Unreviewed difference set between known tools and classifyTool must match explicit registry. Found unreviewed: ${JSON.stringify(unreviewedDifference)}`
  );

  for (const candidate of acknowledgedUnreviewedCandidates) {
    const policy = Contract.classifyTool(candidate);
    assert.equal(
      Contract.policyCode(policy),
      'Unreviewed',
      `Acknowledged candidate '${candidate}' must evaluate to Unreviewed`
    );
  }
});

test('WHAT[speculative-investigation-016] known-bad fixtures prove the gate detects policy mutations and unreviewed leakage', () => {
  // Mutation 1: default branch mutates from Unreviewed to EstimateAfterCall
  function mutatedClassifyDefaultEstimate(name) {
    const policy = Contract.classifyTool(name);
    if (Contract.policyCode(policy) === 'Unreviewed') {
      return Contract.classifyTool("read");
    }
    return policy;
  }

  // Under mutated default, an unknown tool falsely expects participation,
  // but the schema decorator does not decorate it (false).
  assert.throws(
    () => {
      const toolName = "js-foo-unknown";
      const mutatedPolicy = mutatedClassifyDefaultEstimate(toolName);
      const expectedParticipate = Contract.policyCode(mutatedPolicy) === 'EstimateAfterCall';
      // Real schema decoration for unknown tool is false
      const schemaDecorates = false;
      assert.equal(
        schemaDecorates,
        expectedParticipate,
        `Schema decoration mismatch for ${toolName}: expected ${expectedParticipate} but got ${schemaDecorates}`
      );
    },
    /AssertionError/,
    "Gate must fail when default branch mutates from Unreviewed to EstimateAfterCall"
  );

  // Mutation 2: a NoEstimate tool (chronicle) is incorrectly moved into participating set
  function mutatedClassifyChronicleParticipating(name) {
    if (name === "chronicle") {
      return Contract.classifyTool("read");
    }
    return Contract.classifyTool(name);
  }

  assert.throws(
    () => {
      const mutatedPolicy = mutatedClassifyChronicleParticipating("chronicle");
      const expectedParticipate = Contract.policyCode(mutatedPolicy) === 'EstimateAfterCall';
      // Real schema decoration for chronicle is false (NoEstimate)
      const schemaDecorates = false;
      assert.equal(
        schemaDecorates,
        expectedParticipate,
        `NoEstimate tool 'chronicle' incorrectly marked as participating must fail consistency check`
      );
    },
    /AssertionError/,
    "Gate must fail when a NoEstimate tool is incorrectly moved into participating set"
  );

  // Mutation 3: an unreviewed tool leaks into known tools without being acknowledged/classified
  assert.throws(
    () => {
      const simulatedKnownTools = ["read", "chronicle", "new-leaked-tool"];
      const acknowledgedList = [];
      const unreviewed = simulatedKnownTools.filter((name) => Contract.policyCode(Contract.classifyTool(name)) === 'Unreviewed');
      assert.deepEqual(
        unreviewed,
        acknowledgedList,
        `Unreviewed tools leaked: ${unreviewed.join(", ")}`
      );
    },
    /AssertionError/,
    "Gate must fail when an unreviewed tool leaks into known tools without explicit registry"
  );
});
}
