module Wanxiangshu.Omp.SessionCompacting

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
module Dyn = Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.PromptFrontMatter
open Wanxiangshu.Omp.MagicTodo
open Wanxiangshu.Omp.MessagingCodec

let private backlogSession = BacklogSession omp

let sessionCompactingHandler (_pi: obj) (event: obj) (_ctx: obj) : JS.Promise<obj> =
    promise {
        let sessionId = Dyn.str event "sessionId"
        let messagesArr = Dyn.get event "messages"
        let messagesList =
            if Dyn.isNullish messagesArr || not (Dyn.isArray messagesArr) then []
            else
                let arr = unbox<obj array> messagesArr
                decodeEntries sessionId arr
        let cleaned = stripSyntheticBySource messagesList
        if cleaned.IsEmpty then return createObj []
        else
            let backlogEntries = backlogSession.GetOrRebuildBacklog(sessionId, cleaned)
            let anchorTexts = extractHistoryTexts cleaned
            let contextText = buildCompactionAnchorPrompt backlogEntries (fun () -> anchorTexts)
            if System.String.IsNullOrEmpty contextText then return createObj []
            else
                let contextLines = contextText.Split('\n')
                return createObj [ "context", box contextLines ]
    }
