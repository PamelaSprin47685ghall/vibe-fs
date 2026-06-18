module VibeFs.Opencode.MagicTypes

let magicTodoToolName = "todowrite"
let magicReviewToolName = "submit_review"

type BacklogEntry = {
    sequence: int
    timestamp: string
    report: string
}

type MagicState = { backlog: BacklogEntry list }
let emptyMagicState = { backlog = [] }
