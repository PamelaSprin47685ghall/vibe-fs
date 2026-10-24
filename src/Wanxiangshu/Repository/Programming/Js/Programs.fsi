namespace Wanxiangshu.Repository.Programming.Js

open System.Threading.Tasks

module JsPrograms =
    val forSession: sessionId: string -> obj
    val pendingNames: sessionId: string -> string list
    val deliveryBlockersWithChoicesForSession: sessionId: string -> (string * string) list
    val closeSession: sessionId: string -> Task<string list>
    val closeAll: unit -> Task<unit>
