import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import test from 'node:test';
import { initSpikePlugin } from '../../../build/next/OpenCode/SpikePlugin.js';

const REQUIRED_AGENTS = [
  'fast-orchestrator', 'deep-orchestrator',
  'fast-manager', 'deep-manager',
  'fast-coder', 'deep-coder',
  'fast-inspector', 'deep-inspector',
  'fast-devops', 'deep-devops',
  'fast-browser', 'deep-browser',
  'fast-meditator', 'deep-meditator',
  'fast-reviewer', 'deep-reviewer',
  'fast-blogger', 'deep-blogger',
  'fast-executor', 'deep-executor',
];

function seedManagedAgents(config) {
  config.agent = {};
  for (const name of REQUIRED_AGENTS) {
    config.agent[name] = { model: `provider/${name}-model` };
  }
}

function schemaText(schema) {
  return JSON.stringify(schema?.def ?? schema?._def ?? schema ?? {});
}

test('manager permission denies global executor tool and executes mailbox path', async () => {
  const journalDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'wanxiangshu-manager-'));
  try {
    execFileSync('git', ['init', '--quiet', journalDirectory]);
    const hooks = await initSpikePlugin({
      client: {},
      directory: journalDirectory,
      events: { listen: () => () => {} },
    });
    const names = Object.keys(hooks.tool).sort();

    assert.deepEqual(names, ['coder', 'executor', 'fork', 'fork-manager', 'fork-pty', 'inspector', 'join', 'list', 'verdict']);
    assert.deepEqual(Object.keys(hooks.tool.fork.args).sort(), ['agent', 'prompt']);
    assert.deepEqual(Object.keys(hooks.tool['fork-pty'].args).sort(), ['agent', 'prompt', 'signal']);
    assert.deepEqual(Object.keys(hooks.tool['fork-manager'].args).sort(), ['agent', 'prompt']);
    assert.deepEqual(Object.keys(hooks.tool.inspector.args).sort(), ['agent', 'prompt', 'prompts']);
    assert.deepEqual(Object.keys(hooks.tool.coder.args).sort(), ['agent', 'prompt', 'prompts']);

    for (const [toolName, definition] of Object.entries(hooks.tool)) {
      for (const [argName, schema] of Object.entries(definition.args)) {
        assert.equal(typeof schema?.safeParse, 'function', `${toolName}.${argName} must use the host schema builder`);
      }
    }

    const forkAgentSchema = schemaText(hooks.tool.fork.args.agent);
    assert.match(forkAgentSchema, /fast-coder/);
    assert.match(forkAgentSchema, /deep-coder/);
    assert.match(forkAgentSchema, /fast-inspector/);
    assert.match(forkAgentSchema, /deep-inspector/);
    assert.match(forkAgentSchema, /fast-devops/);
    assert.match(forkAgentSchema, /deep-reviewer/);
    assert.doesNotMatch(forkAgentSchema, /"coder"/);
    assert.doesNotMatch(forkAgentSchema, /"manager"/);
    assert.doesNotMatch(forkAgentSchema, /blogger/);
    assert.doesNotMatch(forkAgentSchema, /executor/);
    assert.equal(hooks.tool.fork.args.agent.isOptional?.() ?? false, false);

    const forkManagerSchema = schemaText(hooks.tool['fork-manager'].args.agent);
    assert.match(forkManagerSchema, /fast-manager/);
    assert.match(forkManagerSchema, /deep-manager/);
    assert.doesNotMatch(forkManagerSchema, /"manager"/);
    assert.equal(hooks.tool['fork-manager'].args.agent.isOptional?.() ?? false, false);

    const inspectorSchema = schemaText(hooks.tool.inspector.args.agent);
    assert.match(inspectorSchema, /fast-inspector/);
    assert.match(inspectorSchema, /deep-inspector/);
    assert.equal(hooks.tool.inspector.args.agent.isOptional?.() ?? false, false);

    const coderSchema = schemaText(hooks.tool.coder.args.agent);
    assert.match(coderSchema, /fast-coder/);
    assert.match(coderSchema, /deep-coder/);
    assert.equal(hooks.tool.coder.args.agent.isOptional?.() ?? false, false);

    for (const name of ['inspector', 'coder']) {
      assert.equal(hooks.tool[name].args.prompt.isOptional?.() ?? false, true);
      assert.equal(hooks.tool[name].args.prompts.isOptional?.() ?? false, true);
    }

    const omitFork = hooks.tool.fork.args.agent.safeParse?.(undefined)
      ?? hooks.tool.fork.args.agent.safeParseAsync?.(undefined);
    if (omitFork && typeof omitFork.then !== 'function') {
      assert.equal(omitFork.success, false, 'omit agent must fail fork schema validation');
    }

    const config = {};
    seedManagedAgents(config);
    hooks.config(config);
    assert.equal(typeof config.agent['fast-manager'].prompt, 'string');
    assert.match(config.agent['fast-manager'].prompt, /Manager thinks and delegates/);
    assert.match(config.agent['fast-manager'].prompt, /fork\(agent, prompt\)/);
    assert.match(config.agent['fast-manager'].prompt, /Treat every `join\(\)` as a deliberate blocking point/);
    assert.match(config.agent['fast-manager'].prompt, /work already known and work newly exposed by the latest facts/);
    assert.match(config.agent['fast-manager'].prompt, /fast-coder/);
    assert.equal(typeof config.agent['fast-coder'].prompt, 'string');
    assert.match(config.agent['fast-coder'].prompt, /Coder edits/);
    assert.match(config.agent['fast-coder'].prompt, /Surgical Precision/);
    assert.match(config.agent['fast-coder'].prompt, /Use Inspector only for a genuinely necessary investigation/);
    assert.match(config.agent['fast-coder'].prompt, /inspector\(agent: "fast-inspector", prompts\)/);
    assert.match(config.agent['fast-coder'].prompt, /DO NOT use `inspector` as a routine verification proxy/);
    assert.doesNotMatch(config.agent['fast-coder'].prompt, /executor/i);
    assert.equal(typeof config.agent['fast-devops'].prompt, 'string');
    assert.match(config.agent['fast-devops'].prompt, /DevOps executes/);
    assert.match(config.agent['fast-devops'].prompt, /fork-pty/);
    assert.match(config.agent['fast-devops'].prompt, /No Direct File Modification/);
    assert.equal(typeof config.agent['fast-inspector'].prompt, 'string');
    assert.match(config.agent['fast-inspector'].prompt, /Investigative Inspector/);
    assert.match(config.agent['fast-inspector'].prompt, /executor[`'"\s]*ONLY/i);
    assert.match(config.agent['fast-inspector'].prompt, /Strict Read-Only Invariant/);
    assert.equal(typeof config.agent['fast-reviewer'].prompt, 'string');
    assert.match(config.agent['fast-reviewer'].prompt, /Uncompromising Reviewer/);
    assert.match(config.agent['fast-reviewer'].prompt, /Render a Verdict Only After Rigorous Review/);
    assert.match(config.agent['fast-reviewer'].prompt, /verdict\("PERFECT"\)/);
    assert.match(config.agent['fast-reviewer'].prompt, /verdict\("REVISE"\)/);
    assert.doesNotMatch(config.agent['fast-reviewer'].prompt, /Double-PERFECT|two consecutive `PERFECT`|confirmation|Nope, let's re-evaluate/i);
    assert.equal(typeof config.agent['fast-browser'].prompt, 'string');
    assert.match(config.agent['fast-browser'].prompt, /Information Navigator/);
    assert.match(config.agent['fast-browser'].prompt, /`network`/);
    assert.match(config.agent['fast-browser'].prompt, /do \*\*not\*\* have/i);
    assert.match(config.agent['fast-browser'].prompt, /Browser-only web access/i);
    assert.match(config.agent['fast-browser'].prompt, /MUST NOT use [`']read[`'], [`']glob[`'], or [`']grep[`'] to read or search local workspace or repository files/i);
    assert.match(config.agent['fast-manager'].prompt, /DO NOT delegate local workspace reading or search to [`']fast-browser[`'] \/ [`']deep-browser[`']/i);
    assert.equal(config.agent['fast-browser'].permission.read, 'allow');
    assert.equal(config.agent['fast-browser'].permission.glob, 'allow');
    assert.equal(config.agent['fast-browser'].permission.grep, 'allow');
    assert.equal(config.agent['fast-browser'].permission.network, 'allow');
    assert.equal(config.agent['fast-browser'].permission.executor, 'deny');
    assert.equal(config.agent['fast-browser'].permission.write, 'deny');
    assert.equal(typeof config.agent['fast-meditator'].prompt, 'string');
    assert.match(config.agent['fast-meditator'].prompt, /Architectural Strategist/);
    assert.match(config.agent['fast-meditator'].prompt, /Transparent Trade-Off Evaluation/);
    assert.match(config.agent['fast-meditator'].prompt, /inspector\(agent: "fast-inspector", prompts\)/);
    assert.equal(config.agent['fast-meditator'].permission.read, 'allow');
    assert.equal(config.agent['fast-meditator'].permission.glob, 'allow');
    assert.equal(config.agent['fast-meditator'].permission.grep, 'allow');
    assert.equal(config.agent['fast-meditator'].permission.inspector, 'allow');
    assert.equal(config.agent['fast-meditator'].permission.write, 'deny');
    assert.equal(config.agent['fast-meditator'].permission.fork, 'deny');
    assert.equal(config.agent['fast-meditator'].permission.network, 'deny');
    assert.equal(typeof config.agent['fast-orchestrator'].prompt, 'string');
    assert.match(config.agent['fast-orchestrator'].prompt, /Multi-Worktree Director/);
    assert.match(config.agent['fast-orchestrator'].prompt, /fork-manager/);
    assert.match(config.agent['fast-orchestrator'].prompt, /Host-owned Dual PERFECT/);
    assert.match(config.agent['fast-orchestrator'].prompt, /fast-manager|deep-manager/);
    assert.equal(config.agent['fast-orchestrator'].permission['fork-manager'], 'allow');
    assert.equal(config.agent['fast-orchestrator'].permission.fork, 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission.join, 'allow');
    assert.equal(config.agent['fast-orchestrator'].permission.list, 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission.read, 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission.executor, 'deny');
    assert.equal(typeof config.agent['fast-executor'].prompt, 'string');
    assert.match(config.agent['fast-executor'].prompt, /Command Output Summarizer/);
    assert.match(config.agent['fast-executor'].prompt, /AgentRole\.Executor/);
    assert.match(config.agent['fast-executor'].prompt, /Tool Capability: \[\] \(NONE\)/);
    assert.equal(config.agent['fast-executor'].permission['*'], 'deny');
    assert.equal(config.agent['fast-executor'].permission.executor, 'deny');
    assert.equal(config.agent['fast-executor'].permission.fork, 'deny');
    assert.equal(config.agent['fast-executor'].permission.read, 'deny');
    assert.equal(typeof config.agent['fast-blogger'].prompt, 'string');
    assert.match(config.agent['fast-blogger'].prompt, /Work Log Blogger/);
    assert.match(config.agent['fast-blogger'].prompt, /AgentRole\.Blogger/);
    assert.match(config.agent['fast-blogger'].prompt, /Tool Capability: \[\] \(NONE\)/);
    assert.match(config.agent['fast-blogger'].prompt, /Self-Compression/);
    assert.equal(config.agent['fast-blogger'].permission['*'], 'deny');
    assert.equal(config.agent['fast-blogger'].permission.executor, 'deny');
    assert.equal(config.agent['fast-blogger'].permission.read, 'deny');
    assert.equal(config.agent['fast-manager'].permission['*'], 'deny');
    assert.equal(config.agent['fast-manager'].permission.executor, 'deny');
    assert.equal(config.agent['fast-manager'].permission['fork-pty'], 'deny');
    assert.equal(config.agent['fast-manager'].permission.inspector, 'deny');
    assert.equal(config.agent['fast-manager'].permission.coder, 'deny');
    assert.equal(config.agent['fast-inspector'].permission['*'], 'deny');
    assert.equal(config.agent['fast-inspector'].permission.executor, 'allow');
    assert.equal(config.agent['fast-inspector'].permission['fork-pty'], 'deny');
    assert.equal(config.agent['fast-inspector'].permission.fork, 'deny');
    assert.equal(config.agent['fast-devops'].permission['*'], 'deny');
    assert.equal(config.agent['fast-devops'].permission['fork-pty'], 'allow');
    assert.equal(config.agent['fast-devops'].permission.executor, 'allow');
    assert.equal(config.agent['fast-devops'].permission.join, 'allow');
    assert.equal(config.agent['fast-devops'].permission.list, 'allow');
    assert.equal(config.agent['fast-devops'].permission.read, 'allow');
    assert.equal(config.agent['fast-devops'].permission.glob, 'allow');
    assert.equal(config.agent['fast-devops'].permission.grep, 'allow');
    assert.equal(config.agent['fast-devops'].permission.inspector, 'allow');
    assert.equal(config.agent['fast-devops'].permission.coder, 'allow');
    assert.equal(config.agent['fast-devops'].permission.write, 'deny');
    assert.equal(config.agent['fast-devops'].permission.edit, 'deny');
    assert.equal(config.agent['fast-devops'].permission.fork, 'deny');
    assert.equal(config.agent['fast-coder'].permission.inspector, 'allow');
    assert.equal(config.agent['fast-coder'].permission.executor, 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission['*'], 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission.fork, 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission['fork-manager'], 'allow');
    assert.equal(config.agent['fast-manager'].permission.fork, 'allow');
    assert.equal(config.agent['fast-manager'].permission['fork-manager'], 'deny');
    assert.equal(config.agent['fast-orchestrator'].permission.join, 'allow');
    assert.equal(config.agent['fast-orchestrator'].permission.list, 'deny');

    const transformed = { messages: [{ role: 'user', text: 'hello' }] };
    hooks['chat.transform']({}, transformed);
    const markerRe = /\[(CAPS|REVIEW|HINT):/;
    const allText = transformed.messages.flatMap((m) => [
      m.text ?? '',
      ...(m.parts ?? []).map((p) => p.text ?? ''),
    ]);
    assert.ok(transformed.messages.some((m) => m.text === 'hello'), 'original user message preserved');
    assert.ok(!allText.some((t) => markerRe.test(t)), 'no synthetic [CAPS]/[REVIEW]/[HINT] marker injected');

    const reviewerInspector = JSON.parse(await hooks.tool.inspector.execute(
      { agent: 'fast-inspector', prompts: ['git status'] },
      { sessionID: 'reviewer-contract' },
    ));
    assert.equal(reviewerInspector.agent, 'fast-inspector');
    assert.equal(reviewerInspector.output, 'test output');

    const context = { sessionID: 'manager-contract' };
    const unknown = JSON.parse(await hooks.tool.fork.execute({ agent: 'deep-inspecter', prompt: 'work' }, context));
    assert.match(unknown.error, /Unknown managed agent 'deep-inspecter'/);
    assert.match(unknown.error, /fast-inspector|deep-inspector/);

    const fork = JSON.parse(await hooks.tool.fork.execute({ agent: 'fast-coder', prompt: 'work' }, context));
    const join = JSON.parse(await hooks.tool.join.execute({}, context));
    const list = JSON.parse(await hooks.tool.list.execute({}, context));

    assert.match(fork.agentId, /^[a-z0-9]{6}$/);
    assert.equal(fork.agent, 'fast-coder');
    assert.equal(fork.role, 'coder');
    assert.equal(fork.tier, 'fast');
    assert.equal(fork.fallbackPeer, 'deep-coder');
    assert.equal(join.kind, 'agent');
    assert.equal(join.status, 'completed');
    assert.equal(join.agentId, fork.agentId);
    assert.equal(join.agent, 'fast-coder');
    assert.equal(join.role, 'coder');
    assert.equal(join.tier, 'fast');
    assert.equal(join.fallbackPeer, 'deep-coder');
    assert.ok(typeof join.finalText === 'string' && join.finalText.length > 0, 'join finalText must be non-empty');
    assert.ok(!Array.isArray(join.outcome), 'join must not emit F# Result arrays');
    assert.equal(list[0].kind, 'agent');
    assert.equal(list[0].agentId, fork.agentId);
    assert.equal(list[0].agent, 'fast-coder');
    assert.equal(list[0].role, 'coder');
    assert.equal(list[0].tier, 'fast');
    assert.equal(list[0].fallbackPeer, 'deep-coder');
    assert.equal(typeof list[0].hasPendingCompletion, 'boolean');
    assert.ok('currentRunId' in list[0], 'list must expose currentRunId');
    assert.ok('lastCompletionStatus' in list[0], 'list must expose lastCompletionStatus');
    const commonDirectory = execFileSync('git', ['-C', journalDirectory, 'rev-parse', '--git-common-dir'], { encoding: 'utf8' }).trim();
    const gitDirectory = path.isAbsolute(commonDirectory) ? commonDirectory : path.resolve(journalDirectory, commonDirectory);
    const runtimeDirectory = path.join(gitDirectory, 'wanxiangshu-next', 'runtimes');
    assert.equal(fs.readdirSync(runtimeDirectory).filter((name) => name.endsWith('.ndjson')).length, 1);
    assert.ok(!fs.existsSync(path.join(journalDirectory, '.wanxiangshu-next')), 'Journal must not dirty the workspace');
  } finally {
    fs.rmSync(journalDirectory, { recursive: true, force: true });
  }
});
