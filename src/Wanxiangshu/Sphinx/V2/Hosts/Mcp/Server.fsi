namespace Wanxiangshu.Sphinx.V2.Hosts

open System
open Fable.Core
open System.Threading.Tasks
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Sphinx.V2.Core

module Mcp =
    /// Registers the seven public tools against the same Runtime surface.
    val serve: IEventStore -> JS.Promise<unit>

    /// Starts a stdio server, reporting a boot failure through stderr.
    val boot: commonDir: string -> JS.Promise<unit>
