namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.TerminalSignal

module SharedTerminalBus =
    val acquire: directory: string -> candidate: ISessionTerminalSource -> ISessionTerminalSource
    val release: directory: string option -> source: ISessionTerminalSource option -> unit

    val tryAcquireForWorkspace:
        workspace: string option -> candidate: ISessionTerminalSource -> Task<(string * ISessionTerminalSource) option>
