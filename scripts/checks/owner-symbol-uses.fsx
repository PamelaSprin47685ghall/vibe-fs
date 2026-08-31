open System
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text.Json

type SymbolUseRecord =
    {
        ConsumerPath: string
        ProviderPaths: string array
        DeclarationPaths: string array
        Symbol: string
        SymbolKind: string
        Assembly: string
        Line: int
        Column: int
        EndLine: int
        EndColumn: int
        InferredType: string
        IsNamespace: bool
        IsModule: bool
        IsFromOpenStatement: bool
        IsFromPattern: bool
        IsFromType: bool
        IsFromUse: bool
        MissingDeclaration: bool
    }

type ScanResult =
    {
        RunId: string
        ProjectFile: string
        ProjectAssembly: string
        ProductionFiles: string array
        SymbolUses: SymbolUseRecord array
        ApplicationCandidates: SymbolUseRecord array
        ApplicationRanges: ApplicationRangeRecord array
        MatchExpressions: MatchExpressionRecord array
        BindExpressions: BindExpressionRecord array
        LambdaExpressions: LambdaExpressionRecord array
        ConditionalExpressions: ConditionalExpressionRecord array
        TryExpressions: TryExpressionRecord array
        LoopExpressions: LoopExpressionRecord array
        FunctionDefinitions: FunctionDefinitionRecord array
        LocalFunctionBindings: LocalFunctionBindingRecord array
    }

and ApplicationRangeRecord =
    {
        ConsumerPath: string
        TargetStartLine: int
        TargetStartColumn: int
        TargetEndLine: int
        TargetEndColumn: int
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
    }

and MatchClauseRecord =
    {
        PatternKind: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
    }

and MatchExpressionRecord =
    {
        ConsumerPath: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
        ScrutineeStartLine: int
        ScrutineeStartColumn: int
        ScrutineeEndLine: int
        ScrutineeEndColumn: int
        Clauses: MatchClauseRecord array
    }

and BindExpressionRecord =
    {
        ConsumerPath: string
        BuilderKind: string
        BindingStartLine: int
        BindingStartColumn: int
        BindingEndLine: int
        BindingEndColumn: int
        BodyStartLine: int
        BodyStartColumn: int
        BodyEndLine: int
        BodyEndColumn: int
    }

and LambdaExpressionRecord =
    {
        ConsumerPath: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
        BodyStartLine: int
        BodyStartColumn: int
        BodyEndLine: int
        BodyEndColumn: int
    }

and FunctionDefinitionRecord =
    {
        ConsumerPath: string
        Name: string
        Symbol: string
        Line: int
        Column: int
        EndLine: int
        EndColumn: int
    }

and LocalFunctionBindingRecord =
    {
        ConsumerPath: string
        Name: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
        BodyStartLine: int
        BodyStartColumn: int
        BodyEndLine: int
        BodyEndColumn: int
        ScopeStartLine: int
        ScopeStartColumn: int
        ScopeEndLine: int
        ScopeEndColumn: int
    }

and NamedRangeRecord =
    {
        Kind: string
        StartLine: int
        StartColumn: int
        EndLine: int
        EndColumn: int
    }

and ConditionalExpressionRecord =
    {
        ConsumerPath: string
        ConditionStartLine: int
        ConditionStartColumn: int
        ConditionEndLine: int
        ConditionEndColumn: int
        Branches: NamedRangeRecord array
    }

and TryExpressionRecord =
    {
        ConsumerPath: string
        Kind: string
        BodyStartLine: int
        BodyStartColumn: int
        BodyEndLine: int
        BodyEndColumn: int
        Continuations: NamedRangeRecord array
    }

and LoopExpressionRecord =
    {
        ConsumerPath: string
        Kind: string
        BodyStartLine: int
        BodyStartColumn: int
        BodyEndLine: int
        BodyEndColumn: int
    }

let arguments = fsi.CommandLineArgs |> Array.skip 1

if arguments.Length <> 6 then
    eprintfn "usage: owner-symbol-uses.fsx <project> <production-root> <scratch-root> <tool-dir> <fable-library> <result-json>"
    exit 2

let projectFile = Path.GetFullPath arguments.[0]
let productionRoot = Path.GetFullPath(arguments.[1]).TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
let scratchRoot = Path.GetFullPath arguments.[2]
let toolDirectory = Path.GetFullPath arguments.[3]
let fableLibraryPath = Path.GetFullPath arguments.[4]
let resultPath = Path.GetFullPath arguments.[5]
let applicationConsumerPaths =
    match Environment.GetEnvironmentVariable "OMP_FCS_APPLICATION_CONSUMERS" with
    | null
    | "" -> None
    | value ->
        value.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map Path.GetFullPath
        |> Set.ofArray
        |> Some

type ToolLoadContext(directory: string) =
    inherit AssemblyLoadContext("owner-symbol-uses", true)

    override this.Load(name: AssemblyName) =
        let candidate = Path.Combine(directory, name.Name + ".dll")

        if File.Exists candidate then
            this.LoadFromAssemblyPath candidate
        else
            null

let context = ToolLoadContext toolDirectory

let load name =
    let path = Path.Combine(toolDirectory, name)

    if not (File.Exists path) then
        failwith $"missing Fable tool assembly: {path}"

    context.LoadFromAssemblyPath path

let fsharpCore = load "FSharp.Core.dll"
let astAssembly = load "Fable.AST.dll"
let compilerAssembly = load "Fable.Compiler.dll"
let fcsAssembly = load "FSharp.Compiler.Service.dll"

let flags =
    BindingFlags.Public
    ||| BindingFlags.NonPublic
    ||| BindingFlags.Static
    ||| BindingFlags.Instance

let requireType (assembly: Assembly) name =
    match assembly.GetType(name, false) with
    | null -> failwith $"missing type: {name}"
    | candidate -> candidate

let requireProperty (candidate: Type) name =
    let properties =
        candidate.GetProperties flags
        |> Array.filter (fun property -> property.Name = name && property.GetIndexParameters().Length = 0)

    match properties |> Array.tryFind (fun property -> property.DeclaringType = candidate) with
    | Some property -> property
    | None ->
        match properties |> Array.tryHead with
        | Some property -> property
        | None -> failwith $"missing property: {candidate.FullName}.{name}"

let propertyValue (target: obj) name =
    requireProperty (target.GetType()) name
    |> fun property -> property.GetValue target

let staticPropertyValue (candidate: Type) name =
    requireProperty candidate name
    |> fun property -> property.GetValue null

let invokeConstructor (candidate: Type) (values: obj array) =
    candidate.GetConstructors flags
    |> Array.find (fun constructor -> constructor.GetParameters().Length = values.Length)
    |> fun constructor -> constructor.Invoke values

let emptyList itemType =
    requireType fsharpCore "Microsoft.FSharp.Collections.FSharpList`1"
    |> fun candidate -> candidate.MakeGenericType [| itemType |]
    |> fun candidate -> staticPropertyValue candidate "Empty"

let listOfValues itemType (values: obj array) =
    let listType =
        requireType fsharpCore "Microsoft.FSharp.Collections.FSharpList`1"
        |> fun candidate -> candidate.MakeGenericType [| itemType |]

    let cons =
        listType.GetMethods flags
        |> Array.find (fun methodInfo ->
            methodInfo.Name = "Cons"
            && methodInfo.IsStatic
            && methodInfo.GetParameters().Length = 2)

    values
    |> Array.rev
    |> Array.fold (fun tail value -> cons.Invoke(null, [| value; tail |])) (staticPropertyValue listType "Empty")

let emptyMap keyType valueType =
    requireType fsharpCore "Microsoft.FSharp.Collections.MapModule"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "Empty"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetGenericArguments().Length = 2)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| keyType; valueType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [||])

let some valueType value =
    requireType fsharpCore "Microsoft.FSharp.Core.FSharpOption`1"
    |> fun candidate -> candidate.MakeGenericType [| valueType |]
    |> fun candidate -> candidate.GetMethod("Some", flags).Invoke(null, [| value |])

let optionValue (value: obj) =
    if isNull value then
        None
    elif value.GetType().FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
        Some(propertyValue value "Value")
    else
        Some value

let optionalPropertyValue target name =
    try
        propertyValue target name |> optionValue
    with
    | :? TargetInvocationException as error
        when not (isNull error.InnerException)
             && error.InnerException.Message.EndsWith("property not available", StringComparison.Ordinal) ->
        None

let optionalBooleanProperty target name =
    try
        match propertyValue target name with
        | :? bool as value -> value
        | _ -> false
    with _ ->
        false

let normalizePath (path: string) =
    Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/')

let isProductionPath path =
    Path.GetFullPath(path).StartsWith(productionRoot, StringComparison.Ordinal)

let language =
    requireType astAssembly "Fable.Language"
    |> fun candidate -> staticPropertyValue candidate "JavaScript"

let verbosity =
    requireType astAssembly "Fable.Verbosity"
    |> fun candidate -> staticPropertyValue candidate "Silent"

let majorVersion = compilerAssembly.GetName().Version.Major

let defines =
    [|
        box "FABLE_COMPILER"
        box $"FABLE_COMPILER_{majorVersion}"
        box "FABLE_COMPILER_JAVASCRIPT"
    |]

let compilerOptions =
    invokeConstructor
        (requireType astAssembly "Fable.CompilerOptions")
        [|
            box true
            box false
            language
            listOfValues typeof<string> defines
            box false
            box false
            verbosity
            box ".fs.js"
            box false
            box false
        |]

let cliArgs =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.Util+CliArgs")
        [|
            box projectFile
            box (Path.GetDirectoryName projectFile)
            some typeof<string> (box scratchRoot)
            box false
            box false
            null
            box false
            some typeof<string> (box fableLibraryPath)
            box "Debug"
            box false
            box true
            box true
            box false
            box false
            null
            emptyList typeof<string>
            emptyMap typeof<string> typeof<string>
            null
            compilerOptions
            verbosity
        |]

let crackerOptions =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.ProjectCracker+CrackerOptions")
        [| cliArgs; box false |]

let resolver =
    invokeConstructor (requireType compilerAssembly "Fable.Compiler.MSBuildCrackerResolver") [||]

let projectResponse =
    requireType compilerAssembly "Fable.Compiler.ProjectCracker"
    |> fun candidate -> candidate.GetMethod("getFullProjectOpts", flags)
    |> fun methodInfo -> methodInfo.Invoke(null, [| resolver; crackerOptions |])

let projectOptions = propertyValue projectResponse "ProjectOptions"
let sourceFiles = propertyValue projectOptions "SourceFiles" :?> string array
let missingSources = sourceFiles |> Array.filter (File.Exists >> not)

if missingSources.Length > 0 then
    let sample = missingSources |> Array.truncate 8 |> String.concat ", "
    failwith $"Fable project options contain missing source files: {sample}"

let checkerType = requireType fcsAssembly "FSharp.Compiler.CodeAnalysis.FSharpChecker"

let checker =
    let create =
        checkerType.GetMethods flags
        |> Array.find (fun methodInfo ->
            methodInfo.Name = "Create"
            && methodInfo.IsStatic
            && methodInfo.GetParameters().Length > 10)

    create.GetParameters()
    |> Array.map (fun parameter ->
        match parameter.Name with
        | "keepAssemblyContents"
        | "keepAllBackgroundSymbolUses" -> some typeof<bool> (box true)
        | _ -> null)
    |> fun values -> create.Invoke(null, values)

let checkAsync =
    checkerType.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "ParseAndCheckProject"
        && methodInfo.GetParameters().Length = 2
        && methodInfo.GetParameters().[0].ParameterType.FullName = "FSharp.Compiler.CodeAnalysis.FSharpProjectOptions")
    |> fun methodInfo -> methodInfo.Invoke(checker, [| projectOptions; null |])

let checkResultType = requireType fcsAssembly "FSharp.Compiler.CodeAnalysis.FSharpCheckProjectResults"

let checkResult =
    requireType fsharpCore "Microsoft.FSharp.Control.FSharpAsync"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "RunSynchronously"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetParameters().Length = 3)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| checkResultType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [| checkAsync; null; null |])

let runAsync (resultType: Type) asyncValue =
    requireType fsharpCore "Microsoft.FSharp.Control.FSharpAsync"
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "RunSynchronously"
        && methodInfo.IsGenericMethodDefinition
        && methodInfo.GetParameters().Length = 3)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| resultType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [| asyncValue; null; null |])

let parsingOptions =
    checkerType.GetMethods flags
    |> Array.find (fun methodInfo -> methodInfo.Name = "GetParsingOptionsFromProjectOptions")
    |> fun methodInfo -> methodInfo.Invoke(checker, [| projectOptions |])
    |> fun tuple -> propertyValue tuple "Item1"

let sourceTextOfString =
    fcsAssembly.GetTypes()
    |> Array.find (fun candidate -> candidate.FullName = "FSharp.Compiler.Text.SourceText")
    |> fun candidate -> candidate.GetMethods flags
    |> Array.find (fun methodInfo -> methodInfo.Name = "ofString" && methodInfo.GetParameters().Length = 1)

let parseFileMethod =
    checkerType.GetMethods flags
    |> Array.find (fun methodInfo ->
        methodInfo.Name = "ParseFile"
        && methodInfo.GetParameters().Length >= 3
        && methodInfo.GetParameters().[0].ParameterType = typeof<string>)

let rangeOf target =
    let candidate = target.GetType().GetProperties flags |> Array.tryFind (fun property -> property.Name = "Range")
    match candidate with
    | None -> None
    | Some property ->
        try Some(property.GetValue target)
        with _ -> None

let bindingRangeOf target =
    [ "RangeOfBindingWithRhs"; "RangeOfBindingAndRhs"; "Range" ]
    |> List.tryPick (fun name ->
        match target.GetType().GetProperties flags |> Array.tryFind (fun property -> property.Name = name) with
        | None -> None
        | Some property ->
            try Some(property.GetValue target)
            with _ -> None)

let rangeRecord consumer targetRange applicationRange =
    {
        ConsumerPath = normalizePath consumer
        TargetStartLine = propertyValue targetRange "StartLine" :?> int
        TargetStartColumn = propertyValue targetRange "StartColumn" :?> int
        TargetEndLine = propertyValue targetRange "EndLine" :?> int
        TargetEndColumn = propertyValue targetRange "EndColumn" :?> int
        StartLine = propertyValue applicationRange "StartLine" :?> int
        StartColumn = propertyValue applicationRange "StartColumn" :?> int
        EndLine = propertyValue applicationRange "EndLine" :?> int
        EndColumn = propertyValue applicationRange "EndColumn" :?> int
    }

let parsedSyntaxEvidence =
    let fsharpType = requireType fsharpCore "Microsoft.FSharp.Reflection.FSharpType"
    let fsharpValue = requireType fsharpCore "Microsoft.FSharp.Reflection.FSharpValue"
    let isUnionMethod =
        fsharpType.GetMethods flags
        |> Array.find (fun methodInfo -> methodInfo.Name = "IsUnion" && methodInfo.GetParameters().Length = 2)
    let isRecordMethod =
        fsharpType.GetMethods flags
        |> Array.find (fun methodInfo -> methodInfo.Name = "IsRecord" && methodInfo.GetParameters().Length = 2)
    let getUnionFieldsMethod =
        fsharpValue.GetMethods flags
        |> Array.find (fun methodInfo -> methodInfo.Name = "GetUnionFields" && methodInfo.GetParameters().Length = 3)
    let getRecordFieldsMethod =
        fsharpValue.GetMethods flags
        |> Array.find (fun methodInfo -> methodInfo.Name = "GetRecordFields" && methodInfo.GetParameters().Length = 2)
    let rec unionType (candidate: Type) =
        if isNull candidate then None
        elif isUnionMethod.Invoke(null, [| candidate; null |]) :?> bool then Some candidate
        else unionType candidate.BaseType
    let unionFields value =
        match unionType (value.GetType()) with
        | None -> None
        | Some candidate ->
            let tuple = getUnionFieldsMethod.Invoke(null, [| value; candidate; null |])
            let caseInfo = propertyValue tuple "Item1"
            Some(propertyValue caseInfo "Name" :?> string, propertyValue tuple "Item2" :?> obj array)
    let recordFields value =
        let candidate = value.GetType()
        if isRecordMethod.Invoke(null, [| candidate; null |]) :?> bool then
            Some(getRecordFieldsMethod.Invoke(null, [| value; null |]) :?> obj array)
        else
            None
    sourceFiles
    |> Array.filter isProductionPath
    |> Array.filter (fun sourceFile ->
        applicationConsumerPaths
        |> Option.forall (Set.contains (Path.GetFullPath sourceFile)))
    |> Array.map (fun sourceFile ->
        let fileText = File.ReadAllText sourceFile
        let sourceText = sourceTextOfString.Invoke(null, [| fileText |])
        let parseArguments =
            parseFileMethod.GetParameters()
            |> Array.map (fun parameter ->
                match parameter.Name with
                | "fileName" -> box sourceFile
                | "sourceText" -> sourceText
                | "options" -> parsingOptions
                | _ -> null)
        let parseAsync = parseFileMethod.Invoke(checker, parseArguments)
        let parseResultType = parseFileMethod.ReturnType.GetGenericArguments().[0]
        let parseResult = runAsync parseResultType parseAsync
        let tree = propertyValue parseResult "ParseTree"
        let applications = ResizeArray<ApplicationRangeRecord>()
        let matches = ResizeArray<MatchExpressionRecord>()
        let binds = ResizeArray<BindExpressionRecord>()
        let lambdas = ResizeArray<LambdaExpressionRecord>()
        let conditionals = ResizeArray<ConditionalExpressionRecord>()
        let tries = ResizeArray<TryExpressionRecord>()
        let loops = ResizeArray<LoopExpressionRecord>()
        let bindingCandidates = ResizeArray<string * obj * obj>()
        let lineOffsets =
            fileText
            |> Seq.mapi (fun index character -> index, character)
            |> Seq.choose (fun (index, character) -> if character = '\n' then Some(index + 1) else None)
            |> Seq.append [ 0 ]
            |> Seq.toArray
        let rangeValues range =
            propertyValue range "StartLine" :?> int,
            propertyValue range "StartColumn" :?> int,
            propertyValue range "EndLine" :?> int,
            propertyValue range "EndColumn" :?> int
        let rangeText range =
            let startLine, startColumn, endLine, endColumn = rangeValues range
            let startOffset = lineOffsets.[startLine - 1] + startColumn
            let endOffset = lineOffsets.[endLine - 1] + endColumn
            fileText.Substring(startOffset, endOffset - startOffset)
        let builderKindAt range =
            let startLine, startColumn, _, _ = rangeValues range
            let startOffset = lineOffsets.[startLine - 1] + startColumn
            let prefix = fileText.Substring(0, startOffset)
            [ "TaskResult", prefix.LastIndexOf("taskResult {", StringComparison.Ordinal)
              "Task", prefix.LastIndexOf("task {", StringComparison.Ordinal)
              "Async", prefix.LastIndexOf("async {", StringComparison.Ordinal) ]
            |> List.maxBy snd
            |> fun (kind, index) -> if index < 0 then "Unknown" else kind
        let expressionFields fields =
            fields
            |> Array.filter (fun field ->
                not (isNull field)
                && not (isNull (field.GetType().FullName))
                && field.GetType().FullName.StartsWith("FSharp.Compiler.Syntax.SynExpr", StringComparison.Ordinal))
        let collectionItems (field: obj) =
            if not (isNull field) && not (field :? string) && field :? Collections.IEnumerable then
                field :?> Collections.IEnumerable |> Seq.cast<obj>
            else Seq.empty
        let namedRange kind range =
            let startLine, startColumn, endLine, endColumn = rangeValues range
            {
                Kind = kind
                StartLine = startLine
                StartColumn = startColumn
                EndLine = endLine
                EndColumn = endColumn
            }
        let clauseExpressionRanges fields =
            fields
            |> Seq.collect collectionItems
            |> Seq.choose (fun clause ->
                match unionFields clause with
                | Some("SynMatchClause", clauseFields) ->
                    clauseFields |> expressionFields |> Array.tryLast |> Option.bind rangeOf
                | _ -> None)
            |> Seq.toArray
        let seen = Collections.Generic.HashSet<obj>(Collections.Generic.ReferenceEqualityComparer.Instance)
        let rec visit depth (value: obj) =
            if not (isNull value)
               && depth < 64
               && not (value :? string)
               && not (value.GetType().IsPrimitive)
               && seen.Add value then
                let candidateType = value.GetType()
                let candidateName = candidateType.FullName
                let iterativeCollection =
                    candidateType.IsArray
                    || (not (isNull candidateName)
                        && (candidateName.StartsWith("Microsoft.FSharp.Collections.FSharpList", StringComparison.Ordinal)
                            || candidateName.StartsWith("System.Collections.Generic", StringComparison.Ordinal))
                        && value :? Collections.IEnumerable)
                if iterativeCollection then
                    for item in value :?> Collections.IEnumerable do visit depth item
                else
                    match unionFields value with
                    | Some("App", fields) ->
                        let expressions = expressionFields fields
                        if expressions.Length = 2 then
                            let targetRange =
                                match rangeOf expressions.[0] with
                                | Some functionRange when (rangeText functionRange).TrimEnd().EndsWith("|>", StringComparison.Ordinal) ->
                                    match unionFields expressions.[1] with
                                    | Some("App", _) -> None
                                    | _ -> rangeOf expressions.[1]
                                | range -> range
                            match targetRange, rangeOf value with
                            | Some exactTargetRange, Some applicationRange -> applications.Add(rangeRecord sourceFile exactTargetRange applicationRange)
                            | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some(("Match" | "MatchBang"), fields) ->
                        let expressions = expressionFields fields
                        let clauses =
                            fields
                            |> Seq.collect collectionItems
                            |> Seq.choose (fun clause ->
                                match unionFields clause with
                                | Some("SynMatchClause", clauseFields) ->
                                    let clauseExpressions = expressionFields clauseFields
                                    let pattern =
                                        clauseFields
                                        |> Array.tryFind (fun field ->
                                            not (isNull field)
                                            && not (isNull (field.GetType().FullName))
                                            && field.GetType().FullName.StartsWith("FSharp.Compiler.Syntax.SynPat", StringComparison.Ordinal))
                                    match pattern |> Option.bind rangeOf, clauseExpressions |> Array.tryLast |> Option.bind rangeOf with
                                    | Some patternRange, Some expressionRange ->
                                        let patternText = rangeText patternRange |> fun text -> text.TrimStart()
                                        let patternKind =
                                            if patternText = "Ok" || patternText.StartsWith("Ok ", StringComparison.Ordinal) || patternText.StartsWith("Ok(", StringComparison.Ordinal) then "Ok"
                                            elif patternText = "Error" || patternText.StartsWith("Error ", StringComparison.Ordinal) || patternText.StartsWith("Error(", StringComparison.Ordinal) then "Error"
                                            elif patternText = "Some" || patternText.StartsWith("Some ", StringComparison.Ordinal) || patternText.StartsWith("Some(", StringComparison.Ordinal) then "Some"
                                            elif patternText = "None" then "None"
                                            else "Other"
                                        let startLine, startColumn, endLine, endColumn = rangeValues expressionRange
                                        Some {
                                            PatternKind = patternKind
                                            StartLine = startLine
                                            StartColumn = startColumn
                                            EndLine = endLine
                                            EndColumn = endColumn
                                        }
                                    | _ -> None
                                | _ -> None)
                            |> Seq.toArray
                        match expressions |> Array.tryHead |> Option.bind rangeOf, rangeOf value with
                        | Some scrutineeRange, Some matchRange when clauses.Length > 0 ->
                            let startLine, startColumn, endLine, endColumn = rangeValues matchRange
                            let scrutineeStartLine, scrutineeStartColumn, scrutineeEndLine, scrutineeEndColumn = rangeValues scrutineeRange
                            matches.Add {
                                ConsumerPath = normalizePath sourceFile
                                StartLine = startLine
                                StartColumn = startColumn
                                EndLine = endLine
                                EndColumn = endColumn
                                ScrutineeStartLine = scrutineeStartLine
                                ScrutineeStartColumn = scrutineeStartColumn
                                ScrutineeEndLine = scrutineeEndLine
                                ScrutineeEndColumn = scrutineeEndColumn
                                Clauses = clauses
                            }
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("LetOrUseBang", fields) ->
                        let expressions = expressionFields fields
                        if expressions.Length >= 2 then
                            match rangeOf expressions.[0], rangeOf expressions.[1] with
                            | Some bindingRange, Some bodyRange ->
                                let bindingStartLine, bindingStartColumn, bindingEndLine, bindingEndColumn = rangeValues bindingRange
                                let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                                binds.Add {
                                    ConsumerPath = normalizePath sourceFile
                                    BuilderKind = builderKindAt bindingRange
                                    BindingStartLine = bindingStartLine
                                    BindingStartColumn = bindingStartColumn
                                    BindingEndLine = bindingEndLine
                                    BindingEndColumn = bindingEndColumn
                                    BodyStartLine = bodyStartLine
                                    BodyStartColumn = bodyStartColumn
                                    BodyEndLine = bodyEndLine
                                    BodyEndColumn = bodyEndColumn
                                }
                            | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("LetOrUse", fields) ->
                        let expressions = expressionFields fields
                        match rangeOf value, expressions |> Array.tryLast |> Option.bind rangeOf with
                        | Some expressionRange, Some bodyRange when (rangeText expressionRange).Contains("let!", StringComparison.Ordinal) ->
                            let bindingStartLine, bindingStartColumn, _, _ = rangeValues expressionRange
                            let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                            binds.Add {
                                ConsumerPath = normalizePath sourceFile
                                BuilderKind = builderKindAt expressionRange
                                BindingStartLine = bindingStartLine
                                BindingStartColumn = bindingStartColumn
                                BindingEndLine = bodyStartLine
                                BindingEndColumn = bodyStartColumn
                                BodyStartLine = bodyStartLine
                                BodyStartColumn = bodyStartColumn
                                BodyEndLine = bodyEndLine
                                BodyEndColumn = bodyEndColumn
                            }
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("Lambda", fields) ->
                        let expressions = expressionFields fields
                        match rangeOf value, expressions |> Array.tryLast |> Option.bind rangeOf with
                        | Some lambdaRange, Some bodyRange ->
                            let startLine, startColumn, endLine, endColumn = rangeValues lambdaRange
                            let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                            lambdas.Add {
                                ConsumerPath = normalizePath sourceFile
                                StartLine = startLine
                                StartColumn = startColumn
                                EndLine = endLine
                                EndColumn = endColumn
                                BodyStartLine = bodyStartLine
                                BodyStartColumn = bodyStartColumn
                                BodyEndLine = bodyEndLine
                                BodyEndColumn = bodyEndColumn
                            }
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("IfThenElse", fields) ->
                        let expressions = expressionFields fields
                        let optionalElse =
                            fields
                            |> Seq.choose (fun field ->
                                if isNull field then None
                                else
                                    match unionFields field with
                                    | Some("Some", optionFields) -> optionFields |> expressionFields |> Array.tryHead
                                    | _ -> None)
                            |> Seq.tryHead
                        if expressions.Length >= 2 then
                            match rangeOf expressions.[0], rangeOf expressions.[1] with
                            | Some conditionRange, Some thenRange ->
                                let conditionStartLine, conditionStartColumn, conditionEndLine, conditionEndColumn = rangeValues conditionRange
                                let branches = ResizeArray<NamedRangeRecord>()
                                branches.Add(namedRange "Then" thenRange)
                                optionalElse |> Option.bind rangeOf |> Option.iter (namedRange "Else" >> branches.Add)
                                conditionals.Add {
                                    ConsumerPath = normalizePath sourceFile
                                    ConditionStartLine = conditionStartLine
                                    ConditionStartColumn = conditionStartColumn
                                    ConditionEndLine = conditionEndLine
                                    ConditionEndColumn = conditionEndColumn
                                    Branches = branches.ToArray()
                                }
                            | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("TryWith", fields) ->
                        let expressions = expressionFields fields
                        match expressions |> Array.tryHead |> Option.bind rangeOf with
                        | Some bodyRange ->
                            let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                            tries.Add {
                                ConsumerPath = normalizePath sourceFile
                                Kind = "With"
                                BodyStartLine = bodyStartLine
                                BodyStartColumn = bodyStartColumn
                                BodyEndLine = bodyEndLine
                                BodyEndColumn = bodyEndColumn
                                Continuations = clauseExpressionRanges fields |> Array.map (namedRange "With")
                            }
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("TryFinally", fields) ->
                        let expressions = expressionFields fields
                        if expressions.Length >= 2 then
                            match rangeOf expressions.[0], rangeOf expressions.[1] with
                            | Some bodyRange, Some finallyRange ->
                                let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                                tries.Add {
                                    ConsumerPath = normalizePath sourceFile
                                    Kind = "Finally"
                                    BodyStartLine = bodyStartLine
                                    BodyStartColumn = bodyStartColumn
                                    BodyEndLine = bodyEndLine
                                    BodyEndColumn = bodyEndColumn
                                    Continuations = [| namedRange "Finally" finallyRange |]
                                }
                            | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some(("While" | "For" | "ForEach") as loopKind, fields) ->
                        let expressions = expressionFields fields
                        match expressions |> Array.tryLast |> Option.bind rangeOf with
                        | Some bodyRange ->
                            let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                            loops.Add {
                                ConsumerPath = normalizePath sourceFile
                                Kind = loopKind
                                BodyStartLine = bodyStartLine
                                BodyStartColumn = bodyStartColumn
                                BodyEndLine = bodyEndLine
                                BodyEndColumn = bodyEndColumn
                            }
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some("SynBinding", fields) ->
                        let pattern =
                            fields
                            |> Array.tryFind (fun field ->
                                not (isNull field)
                                && not (isNull (field.GetType().FullName))
                                && field.GetType().FullName.StartsWith("FSharp.Compiler.Syntax.SynPat", StringComparison.Ordinal))
                        let expressions = expressionFields fields
                        match bindingRangeOf value, pattern |> Option.bind rangeOf, expressions |> Array.tryLast |> Option.bind rangeOf with
                        | Some bindingRange, Some patternRange, Some bodyRange ->
                            let name = Text.RegularExpressions.Regex.Match(rangeText patternRange, "[A-Za-z_][A-Za-z0-9_']*").Value
                            if not (String.IsNullOrWhiteSpace name) then bindingCandidates.Add(name, bindingRange, bodyRange)
                        | _ -> ()
                        fields |> Array.iter (visit (depth + 1))
                    | Some(_, fields) -> fields |> Array.iter (visit (depth + 1))
                    | None ->
                        match recordFields value with
                        | Some fields ->
                            if not (isNull candidateName)
                               && candidateName.StartsWith("FSharp.Compiler.Syntax.SynBinding", StringComparison.Ordinal) then
                                let pattern =
                                    fields
                                    |> Array.tryFind (fun field ->
                                        not (isNull field)
                                        && not (isNull (field.GetType().FullName))
                                        && field.GetType().FullName.StartsWith("FSharp.Compiler.Syntax.SynPat", StringComparison.Ordinal))
                                let expressions = expressionFields fields
                                match bindingRangeOf value, pattern |> Option.bind rangeOf, expressions |> Array.tryLast |> Option.bind rangeOf with
                                | Some bindingRange, Some patternRange, Some bodyRange ->
                                    let name =
                                        Text.RegularExpressions.Regex.Match(rangeText patternRange, "[A-Za-z_][A-Za-z0-9_']*").Value
                                    if not (String.IsNullOrWhiteSpace name) then bindingCandidates.Add(name, bindingRange, bodyRange)
                                | _ -> ()
                            fields |> Array.iter (visit (depth + 1))
                        | None when not (isNull candidateType.FullName)
                                    && (candidateType.FullName.StartsWith("System.Tuple", StringComparison.Ordinal)
                                        || candidateType.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal)) ->
                            candidateType.GetProperties flags
                            |> Array.filter (fun property -> property.GetIndexParameters().Length = 0)
                            |> Array.iter (fun property ->
                                try visit (depth + 1) (property.GetValue value)
                                with _ -> ())
                        | None -> ()
        visit 0 tree
        let containsRange outer inner =
            let outerStartLine, outerStartColumn, outerEndLine, outerEndColumn = rangeValues outer
            let innerStartLine, innerStartColumn, innerEndLine, innerEndColumn = rangeValues inner
            (outerStartLine, outerStartColumn) <= (innerStartLine, innerStartColumn)
            && (innerEndLine, innerEndColumn) <= (outerEndLine, outerEndColumn)
        let rangeSize range =
            let startLine, startColumn, endLine, endColumn = rangeValues range
            (endLine - startLine) * 1000000 + endColumn - startColumn
        let localFunctions =
            bindingCandidates
            |> Seq.choose (fun (name, bindingRange, bodyRange) ->
                let bindingScopes =
                    bindingCandidates
                    |> Seq.choose (fun (_, candidateRange, candidateBody) ->
                        if not (obj.ReferenceEquals(candidateRange, bindingRange)) && containsRange candidateBody bindingRange then Some candidateBody else None)
                let scopes = bindingScopes |> Seq.toArray
                if scopes.Length = 0 then None
                else
                    let scopeRange = scopes |> Array.minBy rangeSize
                    let startLine, startColumn, endLine, endColumn = rangeValues bindingRange
                    let bodyStartLine, bodyStartColumn, bodyEndLine, bodyEndColumn = rangeValues bodyRange
                    let scopeStartLine, scopeStartColumn, scopeEndLine, scopeEndColumn = rangeValues scopeRange
                    Some {
                        ConsumerPath = normalizePath sourceFile
                        Name = name
                        StartLine = startLine
                        StartColumn = startColumn
                        EndLine = endLine
                        EndColumn = endColumn
                        BodyStartLine = bodyStartLine
                        BodyStartColumn = bodyStartColumn
                        BodyEndLine = bodyEndLine
                        BodyEndColumn = bodyEndColumn
                        ScopeStartLine = scopeStartLine
                        ScopeStartColumn = scopeStartColumn
                        ScopeEndLine = scopeEndLine
                        ScopeEndColumn = scopeEndColumn
                    })
            |> Seq.toArray
        applications.ToArray(), matches.ToArray(), binds.ToArray(), lambdas.ToArray(), conditionals.ToArray(), tries.ToArray(), loops.ToArray(), localFunctions)

let parsedApplicationRanges = parsedSyntaxEvidence |> Array.collect (fun (applications, _, _, _, _, _, _, _) -> applications) |> Array.distinct
let parsedMatchExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, matches, _, _, _, _, _, _) -> matches) |> Array.distinct
let parsedBindExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, _, binds, _, _, _, _, _) -> binds) |> Array.distinct
let parsedLambdaExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, _, _, lambdas, _, _, _, _) -> lambdas) |> Array.distinct
let parsedConditionalExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, _, _, _, conditionals, _, _, _) -> conditionals) |> Array.distinct
let parsedTryExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, _, _, _, _, tries, _, _) -> tries) |> Array.distinct
let parsedLoopExpressions = parsedSyntaxEvidence |> Array.collect (fun (_, _, _, _, _, _, loops, _) -> loops) |> Array.distinct
let parsedLocalFunctionBindings = parsedSyntaxEvidence |> Array.collect (fun (_, _, _, _, _, _, _, localFunctions) -> localFunctions) |> Array.distinct

let diagnostics = propertyValue checkResult "Diagnostics" :?> Array

let errors =
    diagnostics
    |> Seq.cast<obj>
    |> Seq.filter (fun diagnostic -> string (propertyValue diagnostic "Severity") = "Error")
    |> Seq.toArray

if errors.Length > 0 || propertyValue checkResult "HasCriticalErrors" :?> bool then
    errors
    |> Array.truncate 20
    |> Array.iter (fun diagnostic ->
        eprintfn
            "%s(%O,%O): %s"
            (propertyValue diagnostic "FileName" :?> string)
            (propertyValue diagnostic "StartLine")
            (propertyValue diagnostic "StartColumn")
            (propertyValue diagnostic "Message" :?> string))

    failwith $"FCS project check failed with {errors.Length} error(s)"

let projectAssembly =
    let fullName = propertyValue checkResult "AssemblyFullName" :?> string
    AssemblyName(fullName).Name

let symbolName symbol =
    try
        match propertyValue symbol "FullName" with
        | :? string as value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> propertyValue symbol "DisplayName" :?> string
    with _ ->
        propertyValue symbol "DisplayName" :?> string

let inferredType symbol =
    try
        let fullType = propertyValue symbol "FullType"
        string fullType
    with _ ->
        ""

let symbolLocations symbol =
    [| "DeclarationLocation"; "ImplementationLocation"; "SignatureLocation" |]
    |> Array.choose (optionalPropertyValue symbol)
    |> Array.map (fun range -> propertyValue range "FileName" :?> string)
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.map normalizePath
    |> Array.distinct

let symbolUses =
    checkResultType.GetMethod("GetAllUsesOfAllSymbols", flags).Invoke(checkResult, [| null |]) :?> Array

let records =
    symbolUses
    |> Seq.cast<obj>
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let normalizedConsumer = normalizePath consumer
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool

        if isDefinition || not (isProductionPath consumer) then
            None
        else
            let symbol = propertyValue symbolUse "Symbol"
            let locations = symbolLocations symbol
            let providers =
                locations
                |> Array.filter isProductionPath
                |> Array.filter ((<>) normalizedConsumer)
                |> Array.distinct

            let assembly = propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
            let missingDeclaration = locations.Length = 0 && assembly = projectAssembly
            let isFromOpenStatement = propertyValue symbolUse "IsFromOpenStatement" :?> bool
            let isNamespace = optionalBooleanProperty symbol "IsNamespace"
            let isModule = optionalBooleanProperty symbol "IsFSharpModule"

            if isFromOpenStatement || isNamespace || isModule || (providers.Length = 0 && not missingDeclaration) then
                None
            else
                let range = propertyValue symbolUse "Range"

                Some
                    {
                        ConsumerPath = normalizedConsumer
                        ProviderPaths = providers
                        DeclarationPaths = locations |> Array.filter isProductionPath |> Array.distinct
                        Symbol = symbolName symbol
                        SymbolKind = symbol.GetType().Name
                        Assembly = assembly
                        Line = propertyValue range "StartLine" :?> int
                        Column = propertyValue range "StartColumn" :?> int
                        EndLine = propertyValue range "EndLine" :?> int
                        EndColumn = propertyValue range "EndColumn" :?> int
                        InferredType = inferredType symbol
                        IsNamespace = isNamespace
                        IsModule = isModule
                        IsFromOpenStatement = isFromOpenStatement
                        IsFromPattern = propertyValue symbolUse "IsFromPattern" :?> bool
                        IsFromType = propertyValue symbolUse "IsFromType" :?> bool
                        IsFromUse = propertyValue symbolUse "IsFromUse" :?> bool
                        MissingDeclaration = missingDeclaration
                    })
    |> Seq.distinct
    |> Seq.sortBy (fun item -> item.ConsumerPath, item.ProviderPaths, item.Symbol, item.Line, item.Column)
    |> Seq.toArray

let applicationCandidates =
    symbolUses
    |> Seq.cast<obj>
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool
        let symbol = propertyValue symbolUse "Symbol"
        let symbolKind = symbol.GetType().Name
        let isFromOpenStatement = propertyValue symbolUse "IsFromOpenStatement" :?> bool

        if isDefinition
           || not (isProductionPath consumer)
           || isFromOpenStatement
           || (symbolKind <> "FSharpMemberOrFunctionOrValue" && symbolKind <> "FSharpField") then
            None
        else
            let locations = symbolLocations symbol
            let providers = locations |> Array.filter isProductionPath |> Array.distinct
            let range = propertyValue symbolUse "Range"

            Some
                {
                    ConsumerPath = normalizePath consumer
                    ProviderPaths = providers
                    DeclarationPaths = providers
                    Symbol = symbolName symbol
                    SymbolKind = symbolKind
                    Assembly = propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
                    Line = propertyValue range "StartLine" :?> int
                    Column = propertyValue range "StartColumn" :?> int
                    EndLine = propertyValue range "EndLine" :?> int
                    EndColumn = propertyValue range "EndColumn" :?> int
                    InferredType = inferredType symbol
                    IsNamespace = false
                    IsModule = false
                    IsFromOpenStatement = false
                    IsFromPattern = propertyValue symbolUse "IsFromPattern" :?> bool
                    IsFromType = propertyValue symbolUse "IsFromType" :?> bool
                    IsFromUse = propertyValue symbolUse "IsFromUse" :?> bool
                    MissingDeclaration = false
                })
    |> Seq.distinct
    |> Seq.sortBy (fun item -> item.ConsumerPath, item.Symbol, item.Line, item.Column)
    |> Seq.toArray

let functionDefinitions =
    symbolUses
    |> Seq.cast<obj>
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool
        let symbol = propertyValue symbolUse "Symbol"
        let symbolKind = symbol.GetType().Name
        let functionType = inferredType symbol
        if not isDefinition
           || not (isProductionPath consumer)
           || symbolKind <> "FSharpMemberOrFunctionOrValue"
           || not (functionType.Contains("->", StringComparison.Ordinal)) then
            None
        else
            let range = propertyValue symbolUse "Range"
            Some {
                ConsumerPath = normalizePath consumer
                Name = propertyValue symbol "DisplayName" :?> string
                Symbol = symbolName symbol
                Line = propertyValue range "StartLine" :?> int
                Column = propertyValue range "StartColumn" :?> int
                EndLine = propertyValue range "EndLine" :?> int
                EndColumn = propertyValue range "EndColumn" :?> int
            })
    |> Seq.distinct
    |> Seq.toArray

let result =
    {
        RunId =
            match Environment.GetEnvironmentVariable "OMP_FCS_EVIDENCE_RUN_ID" with
            | null -> ""
            | value -> value
        ProjectFile = normalizePath projectFile
        ProjectAssembly = projectAssembly
        ProductionFiles =
            sourceFiles
            |> Array.filter isProductionPath
            |> Array.map normalizePath
            |> Array.sort
        SymbolUses = records
        ApplicationCandidates = applicationCandidates
        ApplicationRanges = parsedApplicationRanges
        MatchExpressions = parsedMatchExpressions
        BindExpressions = parsedBindExpressions
        LambdaExpressions = parsedLambdaExpressions
        ConditionalExpressions = parsedConditionalExpressions
        TryExpressions = parsedTryExpressions
        LoopExpressions = parsedLoopExpressions
        FunctionDefinitions = functionDefinitions
        LocalFunctionBindings = parsedLocalFunctionBindings
    }

Directory.CreateDirectory(Path.GetDirectoryName resultPath) |> ignore

let jsonOptions = JsonSerializerOptions(WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions))
