namespace Wanxiangshu.Ablation

module AblationManifest =
    val resourcesDir: unit -> string
    val loadNodes: unit -> Result<ManifestDocument, AblationLoadError>
    val nodesFingerprint: unit -> string
    val loadProfiles: unit -> Result<ProfilesDocument, AblationLoadError>
    val loadToolMap: unit -> Result<ToolMapDocument, AblationLoadError>
    val loadFactMap: unit -> Result<FactMapDocument, AblationLoadError>

    val validateNodes: document: ManifestDocument -> Result<unit, AblationLoadError>

    val validateDag:
        document: ManifestDocument -> modes: Map<AblationNodeId, AblationMode> -> Result<unit, AblationLoadError>

    val nodeIds: document: ManifestDocument -> AblationNodeId list

    /// Decode a manifest document from a JS value the caller already holds.
    ///
    /// Same decoder the file reader uses, so a caller cannot construct a document the
    /// loader would have rejected.
    val decodeFromJs: document: obj -> Result<ManifestDocument, AblationLoadError>

    /// Build a registry straight from a document the caller already holds.
    ///
    /// This is the seam that lets a caller ask about a manifest it constructed itself
    /// — a malformed one in a test, a synthesized one in a fixture — without a round
    /// trip through the filesystem and without touching what other callers read.
    val registryOf:
        document: ManifestDocument ->
        profileName: string option ->
        explicit: Map<AblationNodeId, AblationMode> ->
            Result<AblationRegistry, AblationLoadError>

    val buildRegistry:
        document: ManifestDocument ->
        profileName: string option ->
        explicit: Map<AblationNodeId, AblationMode> ->
            Result<AblationRegistry, AblationLoadError>
