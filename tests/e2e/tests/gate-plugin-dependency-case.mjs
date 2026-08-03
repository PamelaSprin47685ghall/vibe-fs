import fs from 'node:fs';
import path from 'node:path';
import { assertTrue, tmpScenarioDir } from './gate-lib.mjs';
import { createIsolatedEnv } from '../isolated-env.js';

export const pluginDependencyCase = {
  name: 'global plugin dependency is ready',
  fn: async () => {
    const env = createIsolatedEnv({
      scenarioDir: tmpScenarioDir(),
      llmUrl: 'http://127.0.0.1:9999/v1',
    });
    const dependencies = path.join(env.XDG_CONFIG_HOME, 'opencode');
    assertTrue(fs.existsSync(path.join(dependencies, 'node_modules', '@opencode-ai', 'plugin')), 'global config reuses plugin dependency');
    const lock = JSON.parse(fs.readFileSync(path.join(dependencies, 'package-lock.json'), 'utf8'));
    assertTrue(Boolean(lock.packages?.['']?.dependencies?.['@opencode-ai/plugin']), 'global config locks plugin dependency');
  },
};
