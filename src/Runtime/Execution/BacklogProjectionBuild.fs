module Wanxiangshu.Runtime.BacklogProjectionBuild

/// Compatibility surface for backlog text/compaction builders. Prefer opening the
/// focused modules directly for new code.

open Wanxiangshu.Kernel.Backlog.BacklogTypes
open Wanxiangshu.Runtime.BacklogProjectionText
open Wanxiangshu.Runtime.BacklogCompactionPrompt
open Wanxiangshu.Runtime.BacklogCompactingTransform

type BacklogEntry = Wanxiangshu.Kernel.Backlog.BacklogTypes.BacklogEntry

let todoWriteToolNameFor = Wanxiangshu.Kernel.Backlog.BacklogTypes.todoWriteToolNameFor
let todoWriteToolNameDefault = Wanxiangshu.Kernel.Backlog.BacklogTypes.todoWriteToolNameDefault
let reviewToolName = Wanxiangshu.Kernel.Backlog.BacklogTypes.reviewToolName
let trunc = Wanxiangshu.Kernel.Backlog.BacklogTypes.trunc
let isTodoResultFor = Wanxiangshu.Kernel.Backlog.BacklogTypes.isTodoResultFor
let isTodoResult = Wanxiangshu.Kernel.Backlog.BacklogTypes.isTodoResult
let isTodoErrorFor = Wanxiangshu.Kernel.Backlog.BacklogTypes.isTodoErrorFor
let isTodoError = Wanxiangshu.Kernel.Backlog.BacklogTypes.isTodoError
let lastTodoErrorTextFor = Wanxiangshu.Kernel.Backlog.BacklogTypes.lastTodoErrorTextFor
let isReviewTool = Wanxiangshu.Kernel.Backlog.BacklogTypes.isReviewTool
let lastTodoErrorText = Wanxiangshu.Kernel.Backlog.BacklogTypes.lastTodoErrorText

type CompletionItem = BacklogProjectionText.CompletionItem

let buildBacklogTextWithError = BacklogProjectionText.buildBacklogTextWithError
let buildBacklogText = BacklogProjectionText.buildBacklogText
let buildCompactionAnchorPrompt = BacklogCompactionPrompt.buildCompactionAnchorPrompt
let compactionDirective = BacklogCompactionPrompt.compactionDirective
let buildCompactionContextText = BacklogCompactionPrompt.buildCompactionContextText
let compactingTransform = BacklogCompactingTransform.compactingTransform
