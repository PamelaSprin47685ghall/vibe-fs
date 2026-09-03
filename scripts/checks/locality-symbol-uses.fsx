open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Runtime.Loader
open System.Text.Json

type SymbolUseRecord =
    { ConsumerPath: string
      ProviderPaths: string array
      Symbol: string
      SymbolKind: string
      Assembly: string
      IsNamespace: bool
      IsModule: bool
      Line: int
      Column: int
      IsFromOpenStatement: bool
      IsFromPattern: bool
      IsFromType: bool
      IsFromUse: bool }

type ScanResult =
    { ProjectFile: string
      ProductionFiles: string array
      SymbolUses: SymbolUseRecord array
      ElapsedMilliseconds: int64 }

let arguments = fsi.CommandLineArgs |> Array.skip 1

if arguments.Length <> 7 then
    eprintfn "usage: locality-symbol-uses.fsx <project> <production-root> <scratch-root> <tool-dir> <fable-library> <assets-json> <result-json>"
    exit 2

let stopwatch = Stopwatch.StartNew()
let projectFile = Path.GetFullPath arguments.[0]
let productionRoot = Path.GetFullPath(arguments.[1]).TrimEnd(Path.DirectorySeparatorChar) + string Path.DirectorySeparatorChar
let scratchRoot = Path.GetFullPath arguments.[2]
let toolDirectory = Path.GetFullPath arguments.[3]
let fableLibraryPath = Path.GetFullPath arguments.[4]
let assetsPath = Path.GetFullPath arguments.[5]
let resultPath = Path.GetFullPath arguments.[6]

type ToolLoadContext(directory: string) =
    inherit AssemblyLoadContext("locality-symbol-uses", true)

    override this.Load(name: AssemblyName) =
        let candidate = Path.Combine(directory, name.Name + ".dll")
        if File.Exists candidate then this.LoadFromAssemblyPath candidate else null

let context = ToolLoadContext toolDirectory

let load name =
    let path = Path.Combine(toolDirectory, name)
    if not (File.Exists path) then failwith $"missing Fable tool assembly: {path}"
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
    candidate.GetProperties flags
    |> Array.filter (fun property -> property.Name = name && property.GetIndexParameters().Length = 0)
    |> Array.tryFind (fun property -> property.DeclaringType = candidate)
    |> Option.orElseWith (fun () -> candidate.GetProperties flags |> Array.tryFind (fun property -> property.Name = name))
    |> Option.defaultWith (fun () -> failwith $"missing property: {candidate.FullName}.{name}")

let propertyValue (target: obj) name = requireProperty (target.GetType()) name |> fun property -> property.GetValue target
let staticPropertyValue (candidate: Type) name = requireProperty candidate name |> fun property -> property.GetValue null

let boolPropertyOrFalse (target: obj) name =
    try propertyValue target name :?> bool
    with _ -> false

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
    |> Array.find (fun methodInfo -> methodInfo.Name = "Empty" && methodInfo.IsGenericMethodDefinition)
    |> fun methodInfo -> methodInfo.MakeGenericMethod [| keyType; valueType |]
    |> fun methodInfo -> methodInfo.Invoke(null, [||])

let some valueType value =
    requireType fsharpCore "Microsoft.FSharp.Core.FSharpOption`1"
    |> fun candidate -> candidate.MakeGenericType [| valueType |]
    |> fun candidate -> candidate.GetMethod("Some", flags).Invoke(null, [| value |])

let normalizePath (path: string) = Path.GetFullPath(path).Replace(Path.DirectorySeparatorChar, '/')
let isProductionPath path = Path.GetFullPath(path).StartsWith(productionRoot, StringComparison.Ordinal)

let language = requireType astAssembly "Fable.Language" |> fun candidate -> staticPropertyValue candidate "JavaScript"
let verbosity = requireType astAssembly "Fable.Verbosity" |> fun candidate -> staticPropertyValue candidate "Silent"
let majorVersion = compilerAssembly.GetName().Version.Major

let compilerOptions =
    invokeConstructor
        (requireType astAssembly "Fable.CompilerOptions")
        [| box true
           box false
           language
           listOfValues typeof<string> [| box "FABLE_COMPILER"; box $"FABLE_COMPILER_{majorVersion}"; box "FABLE_COMPILER_JAVASCRIPT" |]
           box false
           box false
           verbosity
           box ".fs.js"
           box false
           box false |]

let cliArgs =
    invokeConstructor
        (requireType compilerAssembly "Fable.Compiler.Util+CliArgs")
        [| box projectFile
           box (Path.GetDirectoryName projectFile)
           some typeof<string> (box scratchRoot)
           box false
           box false
           null
           box false
           some typeof<string> (box fableLibraryPath)
           box "Debug"
           box true
           box true
           box true
           box false
           box false
           null
           emptyList typeof<string>
           emptyMap typeof<string> typeof<string>
           null
           compilerOptions
           verbosity |]

let crackerOptions =
    invokeConstructor (requireType compilerAssembly "Fable.Compiler.ProjectCracker+CrackerOptions") [| cliArgs; box false |]

let resolver = invokeConstructor (requireType compilerAssembly "Fable.Compiler.MSBuildCrackerResolver") [||]

let projectResponse =
    requireType compilerAssembly "Fable.Compiler.ProjectCracker"
    |> fun candidate -> candidate.GetMethod("getFullProjectOpts", flags)
    |> fun methodInfo -> methodInfo.Invoke(null, [| resolver; crackerOptions |])

let crackedProjectOptions = propertyValue projectResponse "ProjectOptions"

let packageReferences =
    use document = JsonDocument.Parse(File.ReadAllText assetsPath)
    let root = document.RootElement
    let packagesPath = root.GetProperty("project").GetProperty("restore").GetProperty("packagesPath").GetString()
    let target = root.GetProperty("targets").EnumerateObject() |> Seq.exactlyOne |> fun property -> property.Value
    let libraries = root.GetProperty("libraries")

    target.EnumerateObject()
    |> Seq.collect (fun package ->
        match package.Value.TryGetProperty "compile" with
        | false, _ -> Seq.empty
        | true, compile ->
            let libraryPath = libraries.GetProperty(package.Name).GetProperty("path").GetString()
            compile.EnumerateObject()
            |> Seq.choose (fun reference ->
                if reference.Name = "_._" || Path.GetFileName(reference.Name) = "FSharp.Core.dll" then None
                else Some(Path.Combine(packagesPath, libraryPath, reference.Name))))
    |> Seq.distinct
    |> Seq.map (fun reference -> $"-r:{reference}")
    |> Seq.toArray

let projectOptions =
    let projectOptionsType = crackedProjectOptions.GetType()
    let constructor =
        projectOptionsType.GetConstructors flags
        |> Array.maxBy (fun candidate -> candidate.GetParameters().Length)
    let existingOptions = propertyValue crackedProjectOptions "OtherOptions" :?> string array
    let mergedOptions = Array.append existingOptions packageReferences |> Array.distinct

    constructor.GetParameters()
    |> Array.map (fun parameter ->
        projectOptionsType.GetProperties flags
        |> Array.tryFind (fun property ->
            property.GetIndexParameters().Length = 0
            && String.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
        |> Option.map (fun property ->
            if String.Equals(property.Name, "OtherOptions", StringComparison.OrdinalIgnoreCase) then box mergedOptions
            else property.GetValue crackedProjectOptions)
        |> Option.defaultWith (fun () -> failwith $"cannot clone FSharpProjectOptions constructor field: {parameter.Name}"))
    |> constructor.Invoke
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

let symbolName symbol =
    try
        match propertyValue symbol "FullName" with
        | :? string as value when not (String.IsNullOrWhiteSpace value) -> value
        | _ -> propertyValue symbol "DisplayName" :?> string
    with _ -> propertyValue symbol "DisplayName" :?> string

let symbolLocations symbol =
    [| "DeclarationLocation"; "ImplementationLocation"; "SignatureLocation" |]
    |> Array.choose (fun name ->
        try
            let value = propertyValue symbol name
            if isNull value then None
            elif value.GetType().FullName.StartsWith("Microsoft.FSharp.Core.FSharpOption`1", StringComparison.Ordinal) then
                Some(propertyValue value "Value")
            else Some value
        with _ -> None)
    |> Array.map (fun range -> propertyValue range "FileName" :?> string)
    |> Array.filter (String.IsNullOrWhiteSpace >> not)
    |> Array.map normalizePath
    |> Array.distinct

let symbolUses = checkResultType.GetMethod("GetAllUsesOfAllSymbols", flags).Invoke(checkResult, [| null |]) :?> Array

let records =
    symbolUses
    |> Seq.cast<obj>
    |> Seq.choose (fun symbolUse ->
        let consumer = propertyValue symbolUse "FileName" :?> string
        let isDefinition = propertyValue symbolUse "IsFromDefinition" :?> bool
        if isDefinition || not (isProductionPath consumer) then None
        else
            let symbol = propertyValue symbolUse "Symbol"
            let providers = symbolLocations symbol |> Array.filter isProductionPath |> Array.distinct
            let isNamespace = boolPropertyOrFalse symbol "IsNamespace"
            if providers.Length = 0 || isNamespace then None
            else
                let range = propertyValue symbolUse "Range"
                Some
                    { ConsumerPath = normalizePath consumer
                      ProviderPaths = providers
                      Symbol = symbolName symbol
                      SymbolKind = symbol.GetType().Name
                      Assembly = propertyValue (propertyValue symbol "Assembly") "SimpleName" :?> string
                      IsNamespace = isNamespace
                      IsModule = boolPropertyOrFalse symbol "IsFSharpModule"
                      Line = propertyValue range "StartLine" :?> int
                      Column = propertyValue range "StartColumn" :?> int
                      IsFromOpenStatement = propertyValue symbolUse "IsFromOpenStatement" :?> bool
                      IsFromPattern = propertyValue symbolUse "IsFromPattern" :?> bool
                      IsFromType = propertyValue symbolUse "IsFromType" :?> bool
                      IsFromUse = propertyValue symbolUse "IsFromUse" :?> bool })
    |> Seq.distinct
    |> Seq.sortBy (fun item -> item.ConsumerPath, item.ProviderPaths, item.Symbol, item.Line, item.Column)
    |> Seq.toArray

stopwatch.Stop()
let result =
    { ProjectFile = normalizePath projectFile
      ProductionFiles = sourceFiles |> Array.filter isProductionPath |> Array.map normalizePath |> Array.sort
      SymbolUses = records
      ElapsedMilliseconds = stopwatch.ElapsedMilliseconds }

Directory.CreateDirectory(Path.GetDirectoryName resultPath) |> ignore
let jsonOptions = JsonSerializerOptions(WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
File.WriteAllText(resultPath, JsonSerializer.Serialize(result, jsonOptions))
