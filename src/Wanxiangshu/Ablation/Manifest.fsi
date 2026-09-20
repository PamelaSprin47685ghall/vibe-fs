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

    val buildRegistry:
        document: ManifestDocument ->
        profileName: string option ->
        explicit: Map<AblationNodeId, AblationMode> ->
            Result<AblationRegistry, AblationLoadError>
