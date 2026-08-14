import fs from 'node:fs';
import path from 'node:path';

export function provisionPluginDependency(configDir) {
  const repoNodeModules = path.resolve(process.cwd(), 'node_modules');
  const pluginPackage = path.join(repoNodeModules, '@opencode-ai', 'plugin', 'package.json');
  if (!fs.existsSync(pluginPackage)) return;

  fs.mkdirSync(configDir, { recursive: true });
  const nodeModules = path.join(configDir, 'node_modules');
  if (!fs.existsSync(nodeModules)) fs.symlinkSync(repoNodeModules, nodeModules, 'dir');

  const version = JSON.parse(fs.readFileSync(pluginPackage, 'utf8')).version;
  const dependencies = { '@opencode-ai/plugin': version };
  fs.writeFileSync(path.join(configDir, 'package.json'), JSON.stringify({ private: true, dependencies }), 'utf8');
  fs.writeFileSync(
    path.join(configDir, 'package-lock.json'),
    JSON.stringify({ lockfileVersion: 3, packages: { '': { dependencies } } }),
    'utf8',
  );
}
