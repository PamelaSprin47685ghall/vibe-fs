module Wanxiangshu.Shell.SessionIoSpawn

let formatSubagentReport (noOutputText: string) (abortedPrefix: string) (text: string) (aborted: bool) : string =
    if aborted then
        if text = "" then abortedPrefix
        else $"{abortedPrefix} {text}"
    elif text = "" then noOutputText
    else text