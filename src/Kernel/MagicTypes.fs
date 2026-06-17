module VibeFs.Kernel.MagicTypes

let magicTodoToolName = "todowrite"

type BacklogEntry = {
    sequence: int
    timestamp: string
    report: string
}

type MagicState = { backlog: BacklogEntry list }
let emptyMagicState = { backlog = [] }
