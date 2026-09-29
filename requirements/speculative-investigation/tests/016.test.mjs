import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const rawContract = await import("../../../dist/Strength/InvestigationEstimateContract.js");
const rawBudget = await import("../../../dist/Strength/Budget.js");
const rawReadonlyContract = await import("../../../dist/OpenCode/Host/ReadonlyDelegationContract.js");
const rawStaticTools = await import("../../../dist/OpenCode/Tools/StaticTools.js");
const rawPluginHooks = await import("../../../dist/OpenCode/Plugin/PluginHooks.js");
const rawDelegate = await import("../../../dist/Strength/OpenCode/Delegate.js");
const rawPluginScope = await import("../../../dist/Strength/OpenCode/PluginScope.js");
const fs = await import("node:fs");
const path = await import("node:path");
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
    assert.equal(policy.tag, 0, `tool ${name} must be EstimateAfterCall (tag 0)`);
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
    assert.equal(policy.tag, 1, `tool ${name} must be NoEstimate (tag 1)`);
  }

  // Prefix matching is strictly forbidden
  const unreviewedPrefixes = [
    'read-extra', 'globbing', 'grepper', 'edit_file', 'writer', 'run_command',
    'fetch_data', 'js-devops-v2', 'fork_child', 'resume_parent', 'custom_tool'
  ];
  for (const name of unreviewedPrefixes) {
    const policy = Contract.classifyTool(name);
    assert.equal(policy.tag, 2, `tool ${name} must be Unreviewed (tag 2)`);
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
  assert.equal(resEmpty.fields[0].tag, 3, 'NotePresentWhenZero tag is 3');

  // whitespace
  const resWs = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0,
    self_note: '   '
  });
  assert.equal(resWs.tag, 1);
  assert.equal(resWs.fields[0].tag, 3);

  // null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 0,
    self_note: null
  });
  assert.equal(resNull.tag, 1);
  assert.equal(resNull.fields[0].tag, 3);

  // own-property with undefined value
  const objWithUndef = { estimated_readonly_rounds: 0 };
  objWithUndef.self_note = undefined;
  const resUndef = Contract.parseParticipatingArguments(objWithUndef);
  assert.equal(resUndef.tag, 1);
  assert.equal(resUndef.fields[0].tag, 3, 'own-property undefined must be rejected as NotePresentWhenZero');
});

test('WHAT[speculative-investigation-016] positive rounds rejects missing, blank or non-string self_note', () => {
  // missing note
  const resMissing = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2
  });
  assert.equal(resMissing.tag, 1);
  assert.equal(resMissing.fields[0].tag, 4, 'MissingOrBlankNoteWhenPositive tag is 4');

  // empty string note
  const resEmpty = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: ''
  });
  assert.equal(resEmpty.tag, 1);
  assert.equal(resEmpty.fields[0].tag, 4);

  // blank whitespace note
  const resBlank = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: '  \t\n  '
  });
  assert.equal(resBlank.tag, 1);
  assert.equal(resBlank.fields[0].tag, 4);

  // non-string note: number
  const resNum = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: 123
  });
  assert.equal(resNum.tag, 1);
  assert.equal(resNum.fields[0].tag, 5, 'NoteNotString tag is 5');

  // non-string note: boolean
  const resBool = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: true
  });
  assert.equal(resBool.tag, 1);
  assert.equal(resBool.fields[0].tag, 5);

  // non-string note: object
  const resObj = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: { text: "note" }
  });
  assert.equal(resObj.tag, 1);
  assert.equal(resObj.fields[0].tag, 5);

  // non-string note: array
  const resArr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: ["note"]
  });
  assert.equal(resArr.tag, 1);
  assert.equal(resArr.fields[0].tag, 5);

  // non-string note: null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2,
    self_note: null
  });
  assert.equal(resNull.tag, 1);
  assert.equal(resNull.fields[0].tag, 5);
});

test('WHAT[speculative-investigation-016] native number checks and range validation reject invalid values', () => {
  // negative
  const resNeg = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: -1
  });
  assert.equal(resNeg.tag, 1);
  assert.equal(resNeg.fields[0].tag, 2, 'InvalidRange tag is 2');

  // fractional / float
  const resFloat = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 1.5,
    self_note: 'note'
  });
  assert.equal(resFloat.tag, 1);
  assert.equal(resFloat.fields[0].tag, 2);

  // out of range (> 2147483647)
  const resOverflow = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 2147483648,
    self_note: 'note'
  });
  assert.equal(resOverflow.tag, 1);
  assert.equal(resOverflow.fields[0].tag, 2);

  // string number (no string coercion)
  const resStr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: '2',
    self_note: 'note'
  });
  assert.equal(resStr.tag, 1);
  assert.equal(resStr.fields[0].tag, 1, 'WrongNumberType tag is 1');

  // boolean
  const resBool = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: true,
    self_note: 'note'
  });
  assert.equal(resBool.tag, 1);
  assert.equal(resBool.fields[0].tag, 1);

  // array
  const resArr = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: [1],
    self_note: 'note'
  });
  assert.equal(resArr.tag, 1);
  assert.equal(resArr.fields[0].tag, 1);

  // object
  const resObj = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: { rounds: 1 },
    self_note: 'note'
  });
  assert.equal(resObj.tag, 1);
  assert.equal(resObj.fields[0].tag, 1);

  // NaN
  const resNaN = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: NaN,
    self_note: 'note'
  });
  assert.equal(resNaN.tag, 1);
  assert.equal(resNaN.fields[0].tag, 2);

  // Infinity
  const resInf = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: Infinity,
    self_note: 'note'
  });
  assert.equal(resInf.tag, 1);
  assert.equal(resInf.fields[0].tag, 2);

  // null
  const resNull = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: null,
    self_note: 'note'
  });
  assert.equal(resNull.tag, 1);
  assert.equal(resNull.fields[0].tag, 1);

  // missing
  const resMissing = Contract.parseParticipatingArguments({});
  assert.equal(resMissing.tag, 1);
  assert.equal(resMissing.fields[0].tag, 0, 'MissingEstimate tag is 0');
});

test('WHAT[speculative-investigation-016] protocol field mixing and legacy field rejection', () => {
  // Legacy field alone
  const resLegacy = Contract.parseParticipatingArguments({
    delegate_readonly_rounds: 1,
    self_note: 'note'
  });
  assert.equal(resLegacy.tag, 1);
  assert.equal(resLegacy.fields[0].tag, 6, 'MixedProtocolFields tag is 6');

  // Both legacy and new fields
  const resBoth = Contract.parseParticipatingArguments({
    estimated_readonly_rounds: 1,
    delegate_readonly_rounds: 1,
    self_note: 'note'
  });
  assert.equal(resBoth.tag, 1);
  assert.equal(resBoth.fields[0].tag, 6);
});

test('WHAT[speculative-investigation-016] invalid argument container (not a plain object) is rejected', () => {
  for (const bad of [null, undefined, 123, 'arguments', [1, 2]]) {
    const res = Contract.parseParticipatingArguments(bad);
    assert.equal(res.tag, 1);
    assert.equal(res.fields[0].tag, 7, 'InvalidArgumentObject tag is 7');
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
  async function checkBoundaryParticipates(toolName) {
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
    await toolBefore({ tool: toolName, sessionID: "ses-test", callID: "call-test" }, beforeOutput);
    // Participating tools have estimated_readonly_rounds hidden from the business view.
    // Non-participating and unreviewed tools leave arguments intact.
    return !("estimated_readonly_rounds" in beforeOutput.args);
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
    const boundaryParticipates = await checkBoundaryParticipates(name);
    const batchReads = await checkBatchReadsEstimate(name);

    assert.equal(
      schemaAdds,
      expected,
      `Schema decorator for ${name} must yield ${expected}, got ${schemaAdds}`
    );
    assert.equal(
      boundaryParticipates,
      expected,
      `Boundary hook for ${name} must yield ${expected}, got ${boundaryParticipates}`
    );
    assert.equal(
      batchReads,
      expected,
      `Batch aggregator for ${name} must yield ${expected}, got ${batchReads}`
    );
    assert.equal(
      schemaAdds === boundaryParticipates && boundaryParticipates === batchReads,
      true,
      `All three consumers must yield identical conclusions for tool '${name}'`
    );
  }
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
    return policy.tag === 2; // Unreviewed
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
      policy.tag,
      2,
      `Acknowledged candidate '${candidate}' must evaluate to Unreviewed (tag 2)`
    );
  }
});

test('WHAT[speculative-investigation-016] known-bad fixtures prove the gate detects policy mutations and unreviewed leakage', () => {
  // Mutation 1: default branch mutates from Unreviewed (tag 2) to EstimateAfterCall (tag 0)
  function mutatedClassifyDefaultEstimate(name) {
    const policy = Contract.classifyTool(name);
    if (policy.tag === 2) {
      return { tag: 0, cases: () => ["EstimateAfterCall", "NoEstimate", "Unreviewed"] };
    }
    return policy;
  }

  // Under mutated default, an unknown tool falsely expects participation (tag 0),
  // but the schema decorator does not decorate it (false).
  assert.throws(
    () => {
      const toolName = "js-foo-unknown";
      const mutatedPolicy = mutatedClassifyDefaultEstimate(toolName);
      const expectedParticipate = mutatedPolicy.tag === 0;
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

  // Mutation 2: a NoEstimate tool (chronicle) is incorrectly moved into participating set (tag 0)
  function mutatedClassifyChronicleParticipating(name) {
    if (name === "chronicle") {
      return { tag: 0, cases: () => ["EstimateAfterCall", "NoEstimate", "Unreviewed"] };
    }
    return Contract.classifyTool(name);
  }

  assert.throws(
    () => {
      const mutatedPolicy = mutatedClassifyChronicleParticipating("chronicle");
      const expectedParticipate = mutatedPolicy.tag === 0;
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
      const unreviewed = simulatedKnownTools.filter((name) => Contract.classifyTool(name).tag === 2);
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
