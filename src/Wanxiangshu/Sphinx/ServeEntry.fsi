namespace Wanxiangshu.Sphinx

open Fable.Core

/// Composition entry for the Sphinx MCP server process: reads
/// SPHINX_COMMON_DIR, assembles the durable store with the Sphinx integration
/// rules, and serves over the injected IEventStore. Compiled by the sphinx
/// composition shard; the runtime shard only serves.
module ServeEntry =
    val serveDefault: unit -> JS.Promise<unit>
