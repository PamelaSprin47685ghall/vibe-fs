namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.TerminalSignal

[<RequireQualifiedAccess>]
module TerminalWiring =

    val createSource: unit -> ISessionTerminalSource

    val acquire: directory: string -> candidate: ISessionTerminalSource -> ISessionTerminalSource

    val release: directory: string option -> source: ISessionTerminalSource option -> unit

    val tryAcquireForWorkspace:
        workspace: string option -> candidate: ISessionTerminalSource -> Task<(string * ISessionTerminalSource) option>
