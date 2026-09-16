namespace Wanxiangshu.Ablation

type AblationMode =
    | Ablated
    | Borrowed
    | Active

type AblationNodeId = AblationNodeId of string

type AblationAudit =
    { Profile: string option
      ManifestVersion: string
      ManifestFingerprint: string
      NodeCount: int }

type AblationRegistry =
    { Modes: Map<AblationNodeId, AblationMode>
      Audit: AblationAudit }

type AblationLoadError =
    | MissingManifest of path: string
    | InvalidManifest of reason: string
    | UnknownProfile of name: string
    | InvalidMode of node: string * raw: string
    | DagViolation of reason: string

type ManifestNode =
    { Id: string
      Package: string
      Station: int
      Kind: string
      Parent: string option }

type ManifestEdge =
    { From: string
      To: string
      Kind: string }

type ManifestDocument =
    { Version: string
      Nodes: ManifestNode list
      Edges: ManifestEdge list }

type ProfilesDocument =
    { Profiles: Map<string, Map<string, string>> }

type ToolMapDocument = { Tools: Map<string, string> }

type FactMapDocument = { Facts: Map<string, string> }

[<RequireQualifiedAccess>]
module AblationNodeId =
    val create: raw: string -> AblationNodeId
    val value: AblationNodeId -> string

[<RequireQualifiedAccess>]
module AblationMode =
    val parse: raw: string -> AblationMode option
    val wire: mode: AblationMode -> string
    val allowsToolExecution: mode: AblationMode -> bool
    val allowsToolSchema: mode: AblationMode -> bool

[<RequireQualifiedAccess>]
module AblationRegistry =
    val modeFor: id: AblationNodeId -> registry: AblationRegistry -> AblationMode
    val isActive: id: AblationNodeId -> registry: AblationRegistry -> bool
    val isBorrowedOrActive: id: AblationNodeId -> registry: AblationRegistry -> bool
