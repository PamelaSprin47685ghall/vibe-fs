/**
 * diagnostics-causal.js — Scheme B causal-wait snapshot collect/format helpers.
 * Kept separate so diagnostics-collect/format stay under the 200-line budget.
 */

import fs from 'node:fs';
import path from 'node:path';

function subjectBlob(wait) {
  try {
    return JSON.stringify(wait?.subject || []);
  } catch {
    return '';
  }
}

export function correlateCausalExpectations(blocked, snap) {
  const active = Array.isArray(snap?.active) ? snap.active : [];
  const matched = [];
  const unmatched = [];
  for (const exp of blocked) {
    const token = String(exp.id || '');
    const hit = active.some((w) => {
      const blob = subjectBlob(w);
      return token && (blob.includes(token) || String(w.waitKind || '').includes(token));
    });
    (hit ? matched : unmatched).push(exp.id);
  }
  return {
    matched,
    unmatched,
    divergence: active.length === 0 && unmatched.length > 0,
  };
}

export function collectCausalWaits(diag, scenario) {
  const workDir = scenario.host?.workDir;
  if (!workDir) return;
  const filePath = path.join(workDir, '.wanxiangshu', 'diagnostics', 'causal-waits.json');
  if (!fs.existsSync(filePath)) return;
  try {
    const raw = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    diag.causalWaitSnapshot = raw;
    if (raw.frontiers) diag.causalFrontier = raw.frontiers;
    const blocked = scenario.provider?.blockedExpectations;
    if (Array.isArray(blocked) && blocked.length > 0) {
      diag.causalExpectationCorrelation = correlateCausalExpectations(blocked, raw);
    }
  } catch {}
}

function formatOwner(owner) {
  if (!owner) return '?';
  const ids = Array.isArray(owner.identity)
    ? owner.identity.map((p) => `${p.k || p[0]}=${p.v || p[1]}`).join(',')
    : '';
  return ids ? `${owner.kind}:{${ids}}` : String(owner.kind || '?');
}

function formatProducer(producer) {
  if (!producer) return 'none';
  if (producer.tag === 'external') {
    const ids = Array.isArray(producer.identity)
      ? producer.identity.map((p) => `${p.k || p[0]}=${p.v || p[1]}`).join(',')
      : '';
    return `external:${producer.kind}${ids ? `{${ids}}` : ''}`;
  }
  return `workflow:${formatOwner(producer.owner)}`;
}

function formatFrontier(frontier) {
  const out = [];
  out.push(`kind=${frontier.kind || '?'}`);
  out.push(`  ${frontier.detail || ''}`);
  for (const node of frontier.chain || []) {
    const wait = node.waitKind ? ` wait=${node.waitKind}` : '';
    out.push(`  ${formatOwner(node.owner)}${wait}`);
  }
  if (frontier.frontierProducer) {
    out.push(`  producer=${formatProducer(frontier.frontierProducer)}`);
  }
  if (Array.isArray(frontier.cycle) && frontier.cycle.length > 0) {
    out.push(`  cycle=${frontier.cycle.map(formatOwner).join(' → ')}`);
  }
  return out;
}

export function formatCausalSection(diag) {
  if (!diag.causalWaitSnapshot && !diag.causalFrontier) return [];
  const out = ['════════════ CAUSAL FRONTIER ════════════'];
  const frontiers = diag.causalFrontier
    || diag.causalWaitSnapshot?.frontiers
    || [];
  if (frontiers.length === 0) {
    out.push('(no frontier data)');
  } else {
    for (const frontier of frontiers) {
      out.push(...formatFrontier(frontier));
      out.push('');
    }
  }

  const snap = diag.causalWaitSnapshot || {};
  const active = snap.active || [];
  out.push('── Active wait graph ──');
  if (active.length === 0) {
    out.push('  (none)');
  } else {
    for (const wait of active) {
      out.push(
        `  ${wait.waitKind || '?'} owner=${formatOwner(wait.owner)} `
        + `producer=${formatProducer(wait.producer)}`,
      );
    }
  }

  const history = snap.history || [];
  const last = history.slice(-12);
  out.push('── Last transitions ──');
  if (last.length === 0) {
    out.push('  (none)');
  } else {
    for (const t of last) {
      const exit = t.exit ? ` exit=${t.exit}` : '';
      out.push(`  #${t.sequence} ${t.kind} ${t.wait?.waitKind || '?'}${exit}`);
    }
  }

  const corr = diag.causalExpectationCorrelation;
  if (corr) {
    out.push('── Expectation correlation ──');
    if (corr.divergence) out.push('  HARNESS/PRODUCTION DIVERGENCE');
    if (corr.matched?.length) out.push(`  matched: ${corr.matched.join(', ')}`);
    if (corr.unmatched?.length) out.push(`  unmatched: ${corr.unmatched.join(', ')}`);
  }

  out.push('');
  return out;
}
