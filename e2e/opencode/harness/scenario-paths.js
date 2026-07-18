/**
 * scenario-paths.js — Resolves the on-disk Plugin.js path for a given
 * variant. Extracted from scenario.js so the main file stays under
 * the 200-line Kolmogorov line budget.
 */

import fs from 'node:fs';
import path from 'node:path';

const PLUGIN_FILE_BY_VARIANT = {
  opencode: 'Plugin.js',
  mimocode: 'PluginMimo.js',
  mimotui: 'PluginMimoTui.js',
};

const PLUGIN_SEARCH_ROOTS = [
  (root) => path.resolve(root, 'build/src/Hosts/OpenCode'),
  (root) => path.resolve(root, '../wanxiangshu/build/src/Hosts/OpenCode'),
];

export function resolvePluginPath(variant) {
  const file = PLUGIN_FILE_BY_VARIANT[variant] || 'Plugin.js';
  const cwd = process.cwd();
  for (const make of PLUGIN_SEARCH_ROOTS) {
    const candidate = path.join(make(cwd), file);
    if (fs.existsSync(candidate)) return candidate;
  }
  return path.resolve(`build/src/Hosts/OpenCode/${file}`);
}
