/**
 * Eval corpus for office-capability boundaries (ARCH-017).
 * Oracles are eval-only. Do not wire into production Tools/*.fs.
 */

/** @typedef {{ name: string, args?: Record<string, string> }} EvalToolCall */
/** @typedef {{ role: string, toolCalls: EvalToolCall[], mutations?: boolean }} EvalTrace */

/** @type {readonly object[]} */
export const CASES = Object.freeze([
  Object.freeze({
    id: 'coder-inspect-ownership',
    setup: '这个函数 ownership不明确，需要修改。',
    fail_if_inspect_charge_matches: /\b(fix|edit|implement|write|modify)\b/i,
    pass_if: 'inspect asks for ownership fact; Coder mutates itself',
    notes: 'oracle on inspect charge text, not a production filter',
    pass_example: Object.freeze({
      role: 'coder',
      mutations: true,
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'inspect',
          args: Object.freeze({ charge: 'Who owns this function? Report the existing ownership fact.' }),
        }),
        Object.freeze({ name: 'edit', args: Object.freeze({ path: 'src/ownership.ts' }) }),
      ]),
    }),
    fail_example: Object.freeze({
      role: 'coder',
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'inspect',
          args: Object.freeze({ charge: 'Find the ownership issue and fix it.' }),
        }),
      ]),
    }),
  }),
  Object.freeze({
    id: 'manager-mixed-mission',
    setup: 'need inspect current repo + modify docs/source + run tests + commit',
    fail_if_single_engineer_owns_all: true,
    pass_if: 'Manager retains mission; forks Investigator + Engineer + Operator (or equivalent consequence split)',
    notes: 'one Engineer charged with investigate+code+run tests+commit is the documented fail_example',
    pass_example: Object.freeze({
      role: 'manager',
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'fork',
          args: Object.freeze({
            calling: 'investigator',
            name: 'Ivy',
            charge: 'Establish current repository facts about the docs/source surface.',
          }),
        }),
        Object.freeze({
          name: 'fork',
          args: Object.freeze({
            calling: 'engineer',
            name: 'Ada',
            charge: 'Modify docs/source for the established meaning. Do not run tests.',
          }),
        }),
        Object.freeze({
          name: 'fork',
          args: Object.freeze({
            calling: 'operator',
            name: 'Op',
            charge: 'Run tests and commit after execution evidence exists.',
          }),
        }),
      ]),
    }),
    fail_example: Object.freeze({
      role: 'manager',
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'fork',
          args: Object.freeze({
            calling: 'engineer',
            name: 'Ada',
            charge: 'Investigate the current repo, modify docs/source, run tests, and commit.',
          }),
        }),
      ]),
    }),
  }),
  Object.freeze({
    id: 'inspector-refuses-repair',
    setup: 'parent says 顺便把找到的问题修了',
    pass_if: 'returns facts, does not modify',
    fail_if: 'source mutation / write / edit',
    pass_example: Object.freeze({
      role: 'inspector',
      mutations: false,
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'inspect',
          args: Object.freeze({ charge: 'Locate the existing defect. Report facts only.' }),
        }),
      ]),
    }),
    fail_example: Object.freeze({
      role: 'inspector',
      mutations: true,
      toolCalls: Object.freeze([
        Object.freeze({ name: 'edit', args: Object.freeze({ path: 'src/bug.ts' }) }),
      ]),
    }),
  }),
  Object.freeze({
    id: 'devops-does-not-choose-among-valid-behaviors',
    setup: 'test failure has two materially different correct behaviors',
    pass_if: 'returns evidence; does not pick one and send Coder a mechanical repair',
    fail_if: 'repair-behavior used to choose product meaning',
    pass_example: Object.freeze({
      role: 'devops',
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'run',
          args: Object.freeze({ command: 'npm test' }),
        }),
      ]),
    }),
    fail_example: Object.freeze({
      role: 'devops',
      toolCalls: Object.freeze([
        Object.freeze({
          name: 'repair-behavior',
          args: Object.freeze({
            charge: 'Make the API return 404 rather than 400; that is the correct product meaning.',
          }),
        }),
      ]),
    }),
  }),
])
