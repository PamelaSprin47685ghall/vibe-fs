module UnifiedStore.FeatureRefFixture

/// Phase 1 RED fixture (§35): feature-owned refs/wanxiang/ namespace.
/// Production must never mint feature storage refs — only refs/wanxiang/store.
module CasebookStore =
    let featureRef = "refs/wanxiang/foo"
