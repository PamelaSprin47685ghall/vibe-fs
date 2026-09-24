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

    /// The registry used when the environment cannot supply one.
    ///
    /// A broken manifest must not open every gate: the safe reading of "unknown" is
    /// "nothing is allowed", and the audit fields still name what failed.
    let denied () : AblationRegistry =
        { Modes = Map.empty
          Audit =
            { Profile = None
              ManifestVersion = "ablation-v1"
              ManifestFingerprint = "000000000000"
              NodeCount = 0 }
          Nodes = Map.empty }

    /// Decide between the two, loading from the environment only when asked.
    ///
    /// Resolution is a value, so a caller that holds a registry never pays for an
    /// environment read — and a caller that holds none gets the same answer the Host
    /// would have produced.
    let resolve (resolution: RegistrySource) : Result<AblationRegistry, AblationLoadError> =
        match resolution with
        | RegistrySource.Supplied registry -> Ok registry
        | RegistrySource.FromEnvironment ->
            AblationManifest.loadNodes ()
            |> Result.bind (fun document -> AblationManifest.buildRegistry document None Map.empty)
