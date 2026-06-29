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
            let backlogEntries =
                backlogSession.GetOrRebuildBacklog(sessionId, cleaned)
                |> List.map (fun be -> box (createObj [ "user_message", box [||]; "aha_moments", box (be.ahaMoments.Trim()); "changes_and_reasons", box (be.changesAndReasons.Trim()); "gotchas", box (be.gotchas.Trim()); "lessons_and_conventions", box (be.lessonsAndConventions.Trim()); "plan", box (be.plan.Trim()) ]))
                |> List.toArray
            let backlogBlock = [ frontMatterRoot (box backlogEntries) ]
            let anchorTexts = extractHistoryTexts cleaned
            let anchorBlocks = anchorTexts |> List.collect extractFrontMatterFenceStrings
            let allBlocks = backlogBlock @ anchorBlocks
            if allBlocks.IsEmpty then return createObj []
            else
                let contextText = renderCompactionAnchorPrompt allBlocks
                let contextLines = contextText.Split('\n')
                return createObj [ "context", box contextLines ]
    }
