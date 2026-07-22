namespace Wanxiangshu.Next.Tests.JournalTests

open System
open Xunit
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Outcome
open Wanxiangshu.Next.Journal

module FactCodecTests =

    [<Fact>]
    let DeserializeFact_ReviewApplied_NeedsChanges_with_null_and_non_string_elements () =
        // 1) Null element in changeRequests array -> Error with "null or non-string"
        let jsonNullElem =
            """{"version":1,"tag":"ReviewApplied","verdict":{"tag":"NeedsChanges","changeRequests":["c1",null,"c2"]},"round":1}"""

        let resNull = FactCodec.deserializeFact jsonNullElem

        match resNull with
        | Error _ -> ()
        | Ok _ -> Assert.True(false, sprintf "Expected Error when element is null, got: %A" resNull)

        // 2) Non-string element in array (e.g. number 123)
        let jsonNonStr =
            """{"version":1,"tag":"ReviewApplied","verdict":{"tag":"NeedsChanges","changeRequests":["c1",123]},"round":1}"""

        let resNonStr = FactCodec.deserializeFact jsonNonStr

        match resNonStr with
        | Error _ -> ()
        | Ok _ -> Assert.True(false, sprintf "Expected Error when element is non-string, got: %A" resNonStr)

        // 3) Empty array
        let jsonEmpty =
            FactCodec.serializeFact (
                Fact.Review(
                    ReviewApplied
                        {| Verdict = ReviewVerdict.NeedsChanges []
                           Round = 1
                           ResultingTodo = None |}
                )
            )

        let resEmpty = FactCodec.deserializeFact jsonEmpty

        match resEmpty with
        | Ok(Fact.Review(ReviewApplied r)) ->
            match r.Verdict with
            | ReviewVerdict.NeedsChanges reqs -> Assert.Empty(reqs)
            | _ -> Assert.True(false, "Expected NeedsChanges verdict")
        | _ -> Assert.True(false, sprintf "Expected Ok ReviewApplied, got: %A" resEmpty)

    [<Fact>]
    let DeserializeFact_ReviewApplied_with_bad_resultingTodo () =
        // resultingTodo is invalid JSON object missing "items" field -> Error under fail-closed semantics
        let jsonBadTodo =
            """{"version":1,"tag":"ReviewApplied","verdict":{"tag":"Passed"},"round":1,"resultingTodo":{"invalid_field":123}}"""

        let resBad = FactCodec.deserializeFact jsonBadTodo

        match resBad with
        | Error err -> Assert.False(String.IsNullOrWhiteSpace(err))
        | _ -> Assert.True(false, sprintf "Expected Error for invalid resultingTodo, got: %A" resBad)

    [<Fact>]
    let DeserializeFact_TodoChanged_with_null_and_empty_items () =
        // Null element in items -> Error
        let jsonNullItem =
            """{"version":1,"tag":"TodoChanged","snapshot":{"items":["t1",null]}}"""

        let resNull = FactCodec.deserializeFact jsonNullItem

        match resNull with
        | Error _ -> ()
        | _ -> Assert.True(false, sprintf "Expected Error when item is null, got: %A" resNull)

        // Empty items array -> Ok
        let jsonEmptyItem =
            FactCodec.serializeFact (Fact.Todo(TodoChanged {| Snapshot = { Items = [] } |}))

        let resEmpty = FactCodec.deserializeFact jsonEmptyItem

        match resEmpty with
        | Ok(Fact.Todo(TodoChanged r)) -> Assert.Empty(r.Snapshot.Items)
        | _ -> Assert.True(false, sprintf "Expected Ok TodoChanged with empty items, got: %A" resEmpty)
