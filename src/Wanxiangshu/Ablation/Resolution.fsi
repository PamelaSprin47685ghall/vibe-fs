namespace Wanxiangshu.Ablation

/// How an ablation registry is resolved.
///
/// The registry is a value, not global state: whoever needs a decision supplies the
/// registry it should be made against. `FromEnvironment` is the one ambient adapter,
/// and it exists so the Host boundary — which has no registry of its own to thread —
/// can still read the configured profile. Everything above it is pure.
[<RequireQualifiedAccess>]
type RegistrySource =
    /// Use the registry the caller already holds.
    | Supplied of AblationRegistry
    /// Resolve from the ablation environment. The only ambient case.
    | FromEnvironment

[<RequireQualifiedAccess>]
module AblationResolution =

    /// Decide between the two, loading from the environment only when asked.
    ///
    /// Resolution is a value, so a caller that holds a registry never pays for an
    /// environment read — and a caller that holds none gets the same answer the Host
    /// would have produced.
    val resolve: resolution: RegistrySource -> Result<AblationRegistry, AblationLoadError>

    /// The registry to fall back to when the environment cannot supply one.
    val denied: unit -> AblationRegistry
