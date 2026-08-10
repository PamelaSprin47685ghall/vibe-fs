/** DevOps mechanical repair closure. Scenario: scenarios/devops-mechanical-repair-loop.toml */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { runStaticGate } from '../support/index.js';
import { runCanary } from '../support/scenario-driver.mjs';

function toolNames(request) {
  return (request?.tools ?? []).map((tool) => tool?.function?.name ?? tool?.name);
}

function assertMechanicalRepairLoop(scenario, ctx) {
  const devopsRequests = scenario.provider.requests.filter((request) => request.sessionID === ctx.sessionId);
  const devopsTools = devopsRequests.flatMap(toolNames);

  assert.ok(devopsTools.includes('executor'), 'DevOps must execute the failing and repaired gates');
  assert.ok(devopsTools.includes('read'), 'DevOps must diagnose the failed fixture through a real read boundary');
  assert.ok(devopsTools.includes('coder'), 'DevOps must delegate both RED and GREEN through Coder');
  assert.equal(devopsTools.includes('write'), false, 'DevOps must not receive direct write authority');
  assert.equal(devopsTools.includes('edit'), false, 'DevOps must not receive direct edit authority');

  const coderRequests = scenario.provider.requests.filter((request) =>
    request.sessionID !== ctx.sessionId && toolNames(request).includes('write'));
  assert.ok(coderRequests.length >= 2, 'the RED and GREEN Coder children must each receive their write boundary');

  const target = path.join(scenario.host.workDir, 'mechanical-target.txt');
  const regression = path.join(scenario.host.workDir, 'mechanical-repair.test.mjs');
  assert.equal(fs.readFileSync(target, 'utf8'), 'REPAIRED\n', 'GREEN must repair the deterministic fixture');
  assert.ok(fs.existsSync(regression), 'RED must leave a durable regression test before GREEN');
}

if (!runStaticGate([fileURLToPath(import.meta.url)]).passed) {
  throw new Error('devops-mechanical-repair-loop canary static gate failed');
}

process.exit(await runCanary('devops-mechanical-repair-loop', {
  customs: { assertMechanicalRepairLoop },
}));
