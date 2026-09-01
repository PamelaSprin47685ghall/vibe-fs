namespace Wanxiangshu.Sphinx

open Fable.Core

module McpServer =
    val create: store: SessionStore -> obj
    val serveStdio: store: SessionStore -> JS.Promise<unit>
    val serveDefault: unit -> JS.Promise<unit>
