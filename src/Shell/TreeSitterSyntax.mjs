// Tree-sitter syntax check helper. Kept in plain JS because the native
// tree-sitter interaction is easier to audit and test when not embedded
// in a giant F# Emit string.
import { createRequire } from 'node:module';
import hljs from 'highlight.js';

const require = createRequire(import.meta.url);

export default async function checkTreeSitterSyntax(content, filePath) {
  const { platform, arch } = process;
  const suffix =
    platform === 'darwin' && arch === 'arm64' ? 'darwin-arm64' :
    platform === 'darwin' && arch === 'x64' ? 'darwin-x64' :
    platform === 'linux' && arch === 'x64' ? 'linux-x64-gnu' :
    platform === 'linux' && arch === 'arm64' ? 'linux-arm64-gnu' :
    platform === 'win32' && arch === 'x64' ? 'win32-x64-msvc' :
    platform === 'win32' && arch === 'arm64' ? 'win32-arm64-msvc' :
    null;

  if (!suffix) {
    return { ok: false, lang: '', reason: `Unsupported platform: ${platform}-${arch}` };
  }

  let pack;
  try {
    const nativePath = require.resolve(`@kreuzberg/tree-sitter-language-pack/ts-pack-core-node.${suffix}.node`);
    pack = require(nativePath);
    try { pack.downloadAll(); } catch {}
  } catch (e) {
    return { ok: false, lang: '', reason: `native pack load failed: ${e && e.message || e}` };
  }

  let lang = '';
  try { lang = pack.detectLanguageFromPath(filePath) || ''; } catch {}
  if (!lang) {
    try { lang = pack.detectLanguageFromContent(content) || ''; } catch {}
  }
  if (!lang) {
    try {
      const hl = hljs.highlightAuto(content);
      if (hl.language && pack.hasLanguage(hl.language) && hl.relevance >= 5) {
        lang = hl.language;
      }
    } catch {}
  }
  if (!lang) {
    return { ok: true, lang: '', errors: [] };
  }

  let parser;
  try {
    parser = pack.getParser(lang);
  } catch (e) {
    return { ok: false, lang, reason: `parser load failed: ${e && e.message || e}` };
  }

  let tree;
  try {
    tree = parser.parse(content);
  } catch (e) {
    return { ok: false, lang, reason: `parse failed: ${e && e.message || e}` };
  }
  if (!tree) {
    return { ok: false, lang, reason: 'parser returned undefined' };
  }

  function collect(node, out) {
    const count = typeof node.childCount === 'function' ? node.childCount() : 0;
    let hasInner = false;
    for (let i = 0; i < count; i++) {
      const c = node.child(i);
      if (c) {
        const ci = collect(c, out);
        if (ci) hasInner = true;
      }
    }
    const missing = typeof node.isMissing === 'function' ? node.isMissing() : false;
    const err = typeof node.isError === 'function' ? node.isError() : false;
    if (missing || (err && !hasInner)) {
      const s = node.startPosition ? node.startPosition() : { row: 0, column: 0 };
      const e = node.endPosition ? node.endPosition() : { row: 0, column: 0 };
      const rawKind = typeof node.kind === 'function' ? node.kind() : node.type;
      const kind = rawKind || (missing ? 'MISSING' : 'ERROR');
      out.push({
        line: s.row + 1,
        column: s.column + 1,
        endLine: e.row + 1,
        endColumn: e.column + 1,
        severity: 'warning',
        message: missing ? `Missing: ${kind}` : kind
      });
      return true;
    }
    return missing || err || hasInner;
  }

  const errors = [];
  collect(tree.rootNode, errors);
  return { ok: true, lang, errors };
}
