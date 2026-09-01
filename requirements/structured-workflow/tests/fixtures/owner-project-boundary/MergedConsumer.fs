namespace OwnerProjectBoundary

module MergedConsumer =
    // Fable source-merges ordinary ProjectReference inputs, so `internal` is
    // intentionally visible here. This is a toolchain canary, not permission.
    let value = Runtime.secretValue
