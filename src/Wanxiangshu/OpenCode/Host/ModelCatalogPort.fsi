namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host.Ports

/// HNI P-08: the OpenCode adapter for the neutral model catalog.
///
/// The adapter enumerates the routes the host advertises and translates the
/// host payload into neutral `ModelRoute` values. It chooses no route.
/// On this host a model is bound per prompt, not per session, so `Apply`
/// reports `HostError.Unsupported HostCapability.ProviderParams`.
module ModelCatalogPort =

    /// The catalog over the OpenCode SDK client (`client.config.providers`,
    /// `GET /config/providers`).
    type SdkModelCatalog =
        new: client: obj * workspaceDirectory: string option -> SdkModelCatalog
        interface IModelCatalog

    /// The catalog over the OpenCode HTTP server (`GET /config/providers`).
    type HttpModelCatalog =
        new: baseUrl: string * workspaceDirectory: string option -> HttpModelCatalog
        interface IModelCatalog

    /// The catalog for the given host wiring, or `None` when the input names
    /// no host the catalog can be built on.
    val create: workspaceDirectory: string option -> input: obj -> IModelCatalog option
