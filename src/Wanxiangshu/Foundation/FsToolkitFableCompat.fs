namespace Wanxiangshu.Foundation

open System.Threading.Tasks
open Fable.Core

/// Fable-only async/result vocabulary owned by this repository.
/// FsToolkit.ErrorHandling excludes its Task helpers from the Fable build.
module TaskValue =
    let map (mapper: 'value -> 'next) (operation: Task<'value>) : Task<'next> =
        task {
            let! value = operation
            return mapper value
        }

module TaskResult =
    let mapError
        (mapper: 'error -> 'nextError)
        (operation: Task<Result<'value, 'error>>)
        : Task<Result<'value, 'nextError>> =
        task {
            match! operation with
            | Ok value -> return Ok value
            | Error error -> return Error(mapper error)
        }

module TaskResultList =
    let traverseM
        (mapper: 'item -> Task<Result<'value, 'error>>)
        (items: 'item list)
        : Task<Result<'value list, 'error>> =
        let rec collect reversed remaining =
            taskResult {
                match remaining with
                | [] -> return Microsoft.FSharp.Collections.List.rev reversed
                | item :: rest ->
                    let! value = mapper item
                    return! collect (value :: reversed) rest
            }

        collect [] items

/// JS-native executable view of TaskResultList traversal. Callback results use
/// bool only at this boundary; false is translated to Error inside traverseM.
module TaskResultListSurface =
    [<Emit("$0($1)")>]
    let private invokeMapper (mapper: obj) (item: string) : Task<bool> = jsNative

    let traverseM (mapper: obj) (items: string array) : Task<string array> =
        task {
            let map item =
                task {
                    let! accepted = invokeMapper mapper item
                    return if accepted then Ok item else Error item
                }

            match! TaskResultList.traverseM map (items |> Array.toList) with
            | Ok values -> return Array.append [| "Ok" |] (values |> List.toArray)
            | Error error -> return [| "Error"; error |]
        }
