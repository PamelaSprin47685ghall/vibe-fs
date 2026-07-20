module Wanxiangshu.Runtime.FileSwap

open Fable.Core
open Wanxiangshu.Kernel.FileSwap
open Wanxiangshu.Runtime.FileSwapIO

type IFileSwapIO = Wanxiangshu.Runtime.FileSwapIO.IFileSwapIO
type NodeFileSwapIO = Wanxiangshu.Runtime.FileSwapIO.NodeFileSwapIO

/// Swap two different files transactionally.
let private swapDifferentFiles
    (io: IFileSwapIO)
    (canon0: string)
    (canon1: string)
    (doc0: TextDocument)
    (doc1: TextDocument)
    (request: SwapRequest)
    (slice0: string array)
    (slice1: string array)
    (original0: string)
    : JS.Promise<Result<string, string>> =
    promise {
        let newLines0 =
            [| yield! doc0.Lines.[0 .. request.Range0.Begin - 2]
               yield! slice1
               yield! doc0.Lines.[request.Range0.EndExclusive - 1 ..] |]

        let newLines1 =
            [| yield! doc1.Lines.[0 .. request.Range1.Begin - 2]
               yield! slice0
               yield! doc1.Lines.[request.Range1.EndExclusive - 1 ..] |]

        let newContent0 = renderTextDocument { doc0 with Lines = newLines0 }
        let newContent1 = renderTextDocument { doc1 with Lines = newLines1 }

        let! temp0 = io.WriteTemp(canon0, newContent0)
        let! temp1 = io.WriteTemp(canon1, newContent1)

        let mutable replaceError: string option = None

        try
            do! io.Replace(temp0, canon0)
        with ex ->
            do! io.DeleteIfExists temp0
            do! io.DeleteIfExists temp1
            replaceError <- Some $"Failed to write {canon0}: {ex.Message}"

        if replaceError.IsNone then
            try
                do! io.Replace(temp1, canon1)
            with ex ->
                do! io.Restore(canon0, original0)
                do! io.DeleteIfExists temp0
                do! io.DeleteIfExists temp1
                replaceError <- Some $"Failed to write {canon1}: {ex.Message}. {canon0} was restored."

        if replaceError.IsNone then
            do! io.DeleteIfExists temp0
            do! io.DeleteIfExists temp1

            let msg =
                $"Swapped {canon0} [{request.Range0.Begin}, {request.Range0.EndExclusive}) <-> {canon1} [{request.Range1.Begin}, {request.Range1.EndExclusive})"

            return Ok msg
        else
            return Error replaceError.Value
    }

/// Swap two ranges within the same file.
let private swapSameFile
    (io: IFileSwapIO)
    (canon0: string)
    (doc0: TextDocument)
    (request: SwapRequest)
    (slice0: string array)
    (slice1: string array)
    (original0: string)
    : JS.Promise<Result<string, string>> =
    promise {
        let lower, upper =
            if request.Range0.Begin < request.Range1.Begin then
                request.Range0, request.Range1
            else
                request.Range1, request.Range0

        let lowerSlice, upperSlice =
            if request.Range0.Begin < request.Range1.Begin then
                slice0, slice1
            else
                slice1, slice0

        let newLines =
            [| yield! doc0.Lines.[0 .. lower.Begin - 2]
               yield! upperSlice
               yield! doc0.Lines.[lower.EndExclusive - 1 .. upper.Begin - 2]
               yield! lowerSlice
               yield! doc0.Lines.[upper.EndExclusive - 1 ..] |]

        let newContent = renderTextDocument { doc0 with Lines = newLines }
        let! temp = io.WriteTemp(canon0, newContent)
        let mutable replaceError: string option = None

        try
            do! io.Replace(temp, canon0)
        with ex ->
            do! io.DeleteIfExists temp
            do! io.Restore(canon0, original0)
            replaceError <- Some $"Failed to write {canon0}: {ex.Message}. Original was restored."

        if replaceError.IsNone then
            do! io.DeleteIfExists temp
            return Ok $"Swapped ranges within {canon0}"
        else
            return Error replaceError.Value
    }

/// Perform a transactional swap between two line ranges.
let swap (io: IFileSwapIO) (request: SwapRequest) : JS.Promise<Result<string, string>> =
    promise {
        let canon0 = canonicalizePath request.Path0
        let canon1 = canonicalizePath request.Path1

        let! content0 = io.ReadText canon0

        let! content1 =
            if canon0 <> canon1 then
                io.ReadText canon1
            else
                Promise.lift content0

        let doc0 = parseTextDocument content0

        let doc1 =
            if canon0 <> canon1 then
                parseTextDocument content1
            else
                doc0

        match validate canon0 request.Range0 canon1 request.Range1 doc0.Lines.Length doc1.Lines.Length with
        | Error e ->
            let msg =
                match e with
                | EmptyPath f -> $"Empty path: {f}"
                | InvalidRange f -> $"Invalid range: {f}"
                | OutOfBounds(p, r, lc) -> $"Out of bounds: {p} [{r.Begin}, {r.EndExclusive}) has {lc} lines"
                | OverlappingRanges -> "Ranges overlap in the same file"

            return Error msg
        | Ok() ->
            let slice0 =
                doc0.Lines.[request.Range0.Begin - 1 .. request.Range0.EndExclusive - 2]

            let slice1 =
                doc1.Lines.[request.Range1.Begin - 1 .. request.Range1.EndExclusive - 2]

            if canon0 <> canon1 then
                return! swapDifferentFiles io canon0 canon1 doc0 doc1 request slice0 slice1 content0
            else
                return! swapSameFile io canon0 doc0 request slice0 slice1 content0
    }
