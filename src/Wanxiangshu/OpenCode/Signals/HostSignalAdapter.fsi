namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Session.ChatExecution

module HostSignalAdapter =
    val sessionIdOf: HostSignal -> SessionId
    val tryAdapt: isOwned: (SessionId -> bool) -> rawInput: obj -> HostSignal option

type HostSignalRouter =
    new:
        ownedSessions: HashSet<string> *
        onSignal: (HostSignal -> unit) *
        ?onExactAssistantObservation:
            (ExactProviderStartObservation -> bool -> ExactProviderTerminalObservation option -> Task<unit>) ->
            HostSignalRouter

    member RegisterOwned: sessionId: SessionId -> unit
    member UnregisterOwned: sessionId: SessionId -> unit
    member Observe: raw: obj -> unit
    member ObserveLocal: raw: obj -> Task<unit>
