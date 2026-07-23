module Wanxiangshu.Kernel.ToolCatalog.Params

open Wanxiangshu.Kernel.ToolCatalog

let private doc tool field =
    match paramDoc tool field with
    | Ok d -> d
    | Error e -> failwith e

let private coder = doc "coder"
let coderIntents = coder "intents"
let coderTdd = coder "tdd"
let inspectorIntents = doc "inspector" "intents"
let browserIntent = doc "browser" "intent"
let private executor = doc "executor"
let executorLanguage = executor "language"
let executorCommand = executor "command"
let executorDeps = executor "dependencies"
let executorTimeout = executor "timeout_type"
let executorWhatToSummarize = executor "what_to_summarize"
let executorMaxBytes = executor "max_bytes"
let private submitReview = doc "submit_review"
let submitReviewWip = submitReview "wip"
let submitReviewReport = submitReview "report"
let submitReviewAffectedFiles = submitReview "affectedFiles"
let private returnReviewer = doc "return_reviewer"
let returnReviewerVerdict = returnReviewer "verdict"
let returnReviewerFeedback = returnReviewer "feedback"
let private read = doc "read"
let readPath = read "path"
let readOffset = read "offset"
let readLimit = read "limit"
let private write = doc "write"
let writeFilePath = write "file_path"
let writeContent = write "content"
