namespace Wanxiangshu.Next.Journal

// DELETED body (PERSIST-009 C7). DurableEffectProjection / EffectProjection gone.
// Typed markers: OrchestratorProjection.WorktreeEffects.
//
// This path must not remain on disk: architecture-gate fsproj-drift fails while
// an undeclared .fs exists. Coder cannot unlink — run before gate:static:
//   git rm src/Wanxiangshu.Next/Journal/EffectProjection.fs
