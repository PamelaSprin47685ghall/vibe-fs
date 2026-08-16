// FinalitySurface re-export — the registered semantic surface (PR 4 manifest)
// is the legal JS entry point (JS-SEMANTIC-SURFACE-002/003). This file only
// re-exports it so tests share one import path; it carries zero Fable
// authority (no dist internals, no representation helpers).
export * from '../../../../dist/Mission/Manager/FinalitySurface.js'
