/**
 * scenario-paths.js — Resolves the on-disk Plugin.js path for a given
 * variant. Extracted from scenario.js so path resolution lives in its own module.
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const DEFAULT_REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');

export function resolvePluginPath(variant = 'opencode', options = {}) {
  const targetVariant = variant || 'opencode';
  if (targetVariant !== 'opencode') {
    throw new Error(`unknown plugin variant: '${targetVariant}'`);
  }
  const root = options?.productionRoot ? path.resolve(options.productionRoot) : DEFAULT_REPO_ROOT;
  const candidate = path.join(root, 'dist', 'OpenCode', 'Plugin', 'Plugin.js');
  if (fs.existsSync(candidate)) return candidate;
  throw new Error(`production plugin not found for variant '${targetVariant}': ${candidate}`);
}
