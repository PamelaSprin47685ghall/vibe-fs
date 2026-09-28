namespace Wanxiangshu.Mission.Relay

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal

module ProjectionSurface =
    val projectMessages: messages: obj array -> obj

    val apply: journal: JournalHandle -> sessionId: string -> acceptedHuman: bool -> messages: obj array -> Task<obj>
