import assert from 'node:assert/strict';
import test from 'node:test';
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { source } from './gec-support.mjs';
import { gecSurface } from '../../../dist/Sphinx/GecSurface.js';

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(here, '..', '..', '..');

function coreFiles() {
  const direct = join(repoRoot, 'src', 'Wanxiangshu', 'Sphinx', 'Core');
  if (existsSync(direct)) {
    return readdirSync(direct)
      .filter((name) => name.endsWith('.fs') || name.endsWith('.fsi'))
      .map((name) => join(direct, name));
  }
  const flat = join(repoRoot, 'src', 'Wanxiangshu', 'Sphinx');
  if (!existsSync(flat)) return [];
  return readdirSync(flat)
    .filter((name) => name.endsWith('.fs') || name.endsWith('.fsi'))
    .map((name) => join(flat, name));
}

const banned = [
  /Finding/,
  /Evidence/,
  /Hypothes/,
  /Bayes/,
  /AStar|A\*/,
  /MCTS|MonteCarlo/,
  /Borda/,
  /Bradley|BTL/,
  /[Rr]anking/,
  /StopThreshold|stop threshold|stopThreshold/,
  /RenderAnswer|AnswerRenderer|answer renderer/,
  /SemanticAssessment|GenerateCandidates|InvestigateRequest|SynthesizeRequest|RootContract|CognitiveAction/,
];

test('WHAT[EPI-015] core_sources_exclude_epistemic_vocabulary_or_naive_core_reintroduces_legacy_ontology', () => {
  const files = coreFiles().filter((file) => {
    const base = file.split('/').pop();
    return base !== 'McpServer.fs' && base !== 'McpServer.fsi';
  });
  assert.ok(files.length > 0, 'expected Core production sources to exist');
  for (const file of files) {
    const text = readFileSync(file, 'utf8');
    for (const pattern of banned) {
      assert.doesNotMatch(text, pattern, `${file} must not hardcode ${String(pattern)}`);
    }
  }
  void source;
});

test('WHAT[EPI-015] ids_are_kind_specific_opaque_or_stringly_typed_core_accepts_any_string', async () => {
  const surface = gecSurface;
  const valid = [
    { kind: 'InquiryId', value: 'iq_01h455vb4pex5vsknk084sn02x' },
    { kind: 'InquiryId', value: 'iq_00000000000000000000000001' },
    { kind: 'WorkId', value: 'work_01h455vb4pex5vsknk084sn02y' },
    { kind: 'WorkId', value: 'work_00000000000000000000000002' },
    { kind: 'BranchId', value: 'branch_01h455vb4pex5vsknk084sn02z' },
    { kind: 'BranchId', value: 'branch_00000000000000000000000003' },
    { kind: 'EventId', value: 'ev01h455vb4pex5vsknk084sn02a' },
    { kind: 'NodeId', value: 'n01h455vb4pex5vsknk084sn02b' },
    { kind: 'EdgeId', value: 'e01h455vb4pex5vsknk084sn02c' },
    { kind: 'AttemptId', value: 'att01h455vb4pex5vsknk084sn02d' },
    { kind: 'BlindToken', value: 'blind01h455vb4pex5vsknk084sn02e' },
  ];
  for (const input of valid) {
    const result = await surface.validateId(input);
    assert.equal(result.ok, true, `${input.kind}:${input.value} must validate`);
    assert.equal(result.kind, input.kind);
    assert.equal(result.value, input.value);
  }
  const invalid = [
    { kind: 'InquiryId', value: '' },
    { kind: 'InquiryId', value: '   ' },
    { kind: 'InquiryId', value: 'work_01h455vb4pex5vsknk084sn02y' },
    { kind: 'InquiryId', value: 'branch_01h455vb4pex5vsknk084sn02z' },
    { kind: 'InquiryId', value: 'n01h455vb4pex5vsknk084sn02b' },
    { kind: 'WorkId', value: 'iq_01h455vb4pex5vsknk084sn02x' },
    { kind: 'WorkId', value: '' },
    { kind: 'BranchId', value: 'iq_01h455vb4pex5vsknk084sn02x' },
    { kind: 'NodeId', value: 'iq_01h455vb4pex5vsknk084sn02x' },
    { kind: 'NodeId', value: 'work_01h455vb4pex5vsknk084sn02y' },
    { kind: 'NodeId', value: 'branch_01h455vb4pex5vsknk084sn02z' },
    { kind: 'NodeId', value: '' },
    { kind: 'NodeId', value: 'has space' },
    { kind: 'EventId', value: '' },
    { kind: 'EdgeId', value: 'iq_01h455vb4pex5vsknk084sn02x' },
    { kind: 'AttemptId', value: '' },
    { kind: 'BlindToken', value: '' },
    { kind: 'NoSuchKind', value: 'iq_01h455vb4pex5vsknk084sn02x' },
  ];
  for (const input of invalid) {
    const result = await surface.validateId(input);
    assert.equal(result.ok, false, `${input.kind}:${input.value} must be rejected`);
    assert.ok(result.error && typeof result.error.code === 'string', 'rejection must carry a typed error code');
  }
});

test('WHAT[EPI-015] envelopes_compare_by_schema_identity_not_payload_semantics_or_core_interprets_payload', async () => {
  const surface = gecSurface;
  const basePayload = { text: 'same bytes', n: 3 };
  const eventWith = (schemaId, kind) => ({
    type: 'GraphPatched',
    inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
    revision: 1,
    parent: 'none',
    patch: { kind, relation: 'dependsOn', target: 'n01h455vb4pex5vsknk084sn02b' },
    envelope: { schema: { id: schemaId, hash: 'hash-for-' + schemaId }, payload: basePayload },
  });
  const first = await surface.semanticHash({ events: [eventWith('sphinx.probe.open/input@1', 'Abduction')] });
  const sameAgain = await surface.semanticHash({ events: [eventWith('sphinx.probe.open/input@1', 'Abduction')] });
  assert.equal(first.hash, sameAgain.hash, 'identical envelopes must hash identically');
  assert.match(first.hash, /^[0-9a-f]{64}$/, 'hash must be hex sha256');

  const revisionBump = await surface.semanticHash({ events: [eventWith('sphinx.probe.open/input@2', 'Abduction')] });
  assert.notEqual(first.hash, revisionBump.hash, 'schema revision is part of identity and must change the hash');

  const kindCase = await surface.semanticHash({ events: [eventWith('sphinx.probe.open/input@1', 'abduction')] });
  assert.notEqual(first.hash, kindCase.hash, 'Kind compares by exact identity without case folding');

  const relationCase = await surface.semanticHash({
    events: [
      {
        type: 'GraphPatched',
        inquiry: 'iq_01h455vb4pex5vsknk084sn02x',
        revision: 1,
        parent: 'none',
        patch: { kind: 'Abduction', relation: 'dependson', target: 'n01h455vb4pex5vsknk084sn02b' },
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'hash-for-sphinx.probe.open/input@1' }, payload: basePayload },
      },
    ],
  });
  assert.notEqual(first.hash, relationCase.hash, 'Relation compares by exact identity without normalization');
});
