module VibeFs.Shell.CapsBudget

open VibeFs.Kernel.CapsFormat

let maxFileSize = 4 * 1_048_576
let maxTotalContextBytes = 8 * 1_048_576
let maxCapsFiles = 2000

type Budget = { results: CapsFile ResizeArray; totalBytes: int; count: int }

let fresh () : Budget = { results = ResizeArray (); totalBytes = 0; count = 0 }

let isFull (b: Budget) : bool =
    b.count >= maxCapsFiles || b.totalBytes >= maxTotalContextBytes

let absorb (file: CapsFile) (b: Budget) : Budget =
    let nextTotal = b.totalBytes + file.content.Length
    if nextTotal > maxTotalContextBytes then b
    else
        b.results.Add file
        { results = b.results; totalBytes = nextTotal; count = b.count + 1 }
