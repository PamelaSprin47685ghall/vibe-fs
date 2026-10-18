namespace Wanxiangshu.Foundation

open Fable.Core
open System
open System.Collections.Generic

module GlobMatch =

    type GlobPatternError = | InvalidPattern

    [<Emit("new RegExp($0)")>]
    let private regexExact (source: string) : obj = jsNative

    [<Emit("$0.test($1)")>]
    let private regexTest (re: obj) (text: string) : bool = jsNative

    let testCompiled (regex: obj) (text: string) : bool = regexTest regex text

    type private Token =
        | Literal of char
        | Star
        | RecursiveStar
        | RecursiveDirectory
        | One
        | CharacterClass of string

    let private classCharacter first character =
        match first, character with
        | _, '\\' -> "\\\\"
        | true, ']' -> "\\]"
        | _ -> string character

    let private tokens (pattern: string) =
        let rec read (chars: char list) acc =
            match chars with
            | [] -> Ok(List.rev acc)
            | '*' :: '*' :: '/' :: rest -> read rest (RecursiveDirectory :: acc)
            | '*' :: '*' :: rest -> read rest (RecursiveStar :: acc)
            | '*' :: rest -> read rest (Star :: acc)
            | '?' :: rest -> read rest (One :: acc)
            | '[' :: rest -> readClass rest "" 0 acc
            | c :: rest -> read rest (Literal c :: acc)

        and readClass chars body count acc =
            match chars with
            | [] -> Error InvalidPattern
            | ']' :: rest when count > 0 -> read rest (CharacterClass body :: acc)
            | '!' :: rest when count = 0 -> readClass rest "^" 1 acc
            | c :: rest -> readClass rest (body + classCharacter (count = 0) c) (count + 1) acc

        let leading = pattern.StartsWith("/")
        let rest = if leading then pattern.Substring(1) else pattern

        if String.IsNullOrEmpty rest then
            Error InvalidPattern
        else
            let body = if leading || rest.Contains("/") then rest else "**/" + rest

            read (List.ofSeq body) []

    let private tokenRegex token =
        match token with
        | Literal c -> System.Text.RegularExpressions.Regex.Escape(string c)
        | Star -> "[^/]*"
        | RecursiveStar -> ".*"
        | RecursiveDirectory -> "(?:.*/)?"
        | One -> "[^/]"
        | CharacterClass body -> "[" + body + "]"

    let compilePattern (pattern: string) : Result<obj, GlobPatternError> =
        tokens pattern
        |> Result.map (fun parsed ->
            parsed
            |> List.map tokenRegex
            |> String.concat ""
            |> fun body -> regexExact ("^" + body + "$"))

    let matchesPathPattern (pattern: string) (path: string) : Result<bool, GlobPatternError> =
        compilePattern pattern |> Result.map (fun regex -> testCompiled regex path)

    type private CharacterMatch =
        | Exact of char
        | ExceptSlash
        | ExceptLineEnd
        | InClass of obj

    type private State =
        { Epsilon: ResizeArray<int>
          Transitions: ResizeArray<CharacterMatch * int> }

    let private emptyState () =
        { Epsilon = ResizeArray()
          Transitions = ResizeArray() }

    let private machine parsed =
        let states =
            ResizeArray(List.init (List.length parsed + 1) (fun _ -> emptyState ()))

        parsed
        |> List.iteri (fun index token ->
            let edge matcher destination =
                states[index].Transitions.Add(matcher, destination)

            match token with
            | Literal c -> edge (Exact c) (index + 1)
            | One -> edge ExceptSlash (index + 1)
            | CharacterClass body -> edge (InClass(regexExact ("^[" + body + "]$"))) (index + 1)
            | Star
            | RecursiveStar ->
                states[index].Epsilon.Add(index + 1)
                edge (if token = Star then ExceptSlash else ExceptLineEnd) index
            | RecursiveDirectory ->
                let repeated = states.Count
                states.Add(emptyState ())
                states[index].Epsilon.Add(index + 1)
                states[index].Epsilon.Add(repeated)
                states[repeated].Transitions.Add(ExceptLineEnd, repeated)
                states[repeated].Transitions.Add(Exact '/', index + 1))

        states.ToArray(), List.length parsed

    let private reachable (next: int -> seq<int>) seeds =
        let found = HashSet<int>()
        let pending = Queue<int>()

        let enqueue state =
            if found.Add state then
                pending.Enqueue state

        seeds |> Seq.iter enqueue

        while pending.Count > 0 do
            next (pending.Dequeue()) |> Seq.iter enqueue

        found

    let private epsilonClosure (states: State array) seeds =
        reachable (fun state -> states[state].Epsilon) seeds

    let private matchesCharacter matcher character =
        match matcher with
        | Exact expected -> character = expected
        | ExceptSlash -> character <> '/'
        | ExceptLineEnd ->
            character <> '\n'
            && character <> '\r'
            && character <> '\u2028'
            && character <> '\u2029'
        | InClass regex -> regexTest regex (string character)

    let private advance (states: State array) active character =
        active
        |> Seq.collect (fun state -> states[state].Transitions)
        |> Seq.choose (fun (matcher, next) ->
            if matchesCharacter matcher character then
                Some next
            else
                None)
        |> epsilonClosure states

    let private pathCharacter character =
        character <> '\u0000' && character <> '\\'

    let private canMatchPathCharacter matcher =
        match matcher with
        | Exact character -> pathCharacter character
        | ExceptSlash
        | ExceptLineEnd -> true
        | InClass regex ->
            Seq.init 65535 (fun code -> char (code + 1))
            |> Seq.exists (fun character -> pathCharacter character && regexTest regex (string character))

    let private pathSuccessors (states: State array) state =
        let targets =
            states[state].Transitions
            |> Seq.filter (fun (matcher, _) -> canMatchPathCharacter matcher)
            |> Seq.map snd

        Seq.append states[state].Epsilon targets

    // Scope paths use '/' separators and contain no NUL or backslash.
    let mayMatchSubtree (pattern: string) (directory: string) : Result<bool, GlobPatternError> =
        tokens pattern
        |> Result.map (fun parsed ->
            let states, terminal = machine parsed
            let characters = directory.ToCharArray()
            let active = Array.fold (advance states) (epsilonClosure states [ 0 ]) characters

            Array.forall pathCharacter characters
            && (active.Contains terminal
                || (advance states active '/' |> reachable (pathSuccessors states)).Contains terminal))
