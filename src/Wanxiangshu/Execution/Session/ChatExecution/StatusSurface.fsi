namespace Wanxiangshu.Execution.Session.ChatExecution

open Wanxiangshu.Persistence.Journal

module StatusSurface =
    val query: journal: JournalHandle -> sessionId: string -> physicalUserMessageId: string -> obj
    val queryFacts: serializedFacts: string array -> sessionId: string -> physicalUserMessageId: string -> obj
