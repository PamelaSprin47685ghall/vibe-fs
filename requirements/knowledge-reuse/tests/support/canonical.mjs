/**
 * Canonical Case content shared by the knowledge-reuse scenarios.
 *
 * It lives in its own module with no `dist` import so that a cold reader can
 * assert the canonical bytes without loading the Casebook, index, lifecycle or
 * bookkeeper graphs. A cold start that pays the whole graph is no longer a
 * cheap independent observation, it is a second Boot.
 */
export const CANONICAL_Q = 'Canonical maintained question'
export const CANONICAL_A = 'Summary of Inspector answers across turns.'
