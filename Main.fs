open System.IO
open System.IO.Compression
open System.Text.RegularExpressions
open System.Xml.Linq

let folderCharactersRegex = Regex(",|\.")
let xn n = XName.Get n

let exportRule(rule: XElement, folderPath: string) =
    let ruleName = rule.Attribute(xn"LABEL").Value.Replace('/', '_')
    let condition = rule.Element(xn"CONDITION").Value
    let implication = rule.Element(xn"IMPLICATION").Value

    File.WriteAllText($"{folderPath}/{ruleName}.vbs", $"/*\nCondition: '{condition}'\n*/\n\n{implication}")

let rec exportRulesFromFolder(folder: XElement, modelName: string, outputPath: string) =
    let folderName = folder.Attribute(XName.Get "LABEL").Value
    let folderPath = $"{outputPath}/{modelName}/{folderName}"

    Directory.CreateDirectory folderPath |> ignore

    folder.Elements(xn"RULES") |> Seq.iter(fun k -> exportRulesFromFolder(k, $"{modelName}/{folderName}", outputPath))
    folder.Elements(xn"RULE")  |> Seq.iter(fun k -> exportRule(k, folderPath))

let exportModelFile(inputFileName: string, modelFileStream: Stream, outputPath: string) =
    let topClassElement = XDocument.Load(modelFileStream).Element(xn"CLASS")

    if topClassElement.Attribute(xn"ISINTERFACE").Value = "false" then
        let modelName = Path.GetFileNameWithoutExtension(inputFileName) |> fun k -> folderCharactersRegex.Replace(k, "_")
        let outputDir = $"{outputPath}/{modelName}";

        Directory.CreateDirectory(outputDir) |> ignore
        printfn $"Exporting model: {modelName}"

        topClassElement.Element(xn"RULES")
                       .Elements(xn"RULES") |> Seq.iter(fun k -> exportRulesFromFolder(k, modelName, outputPath))

    modelFileStream.Dispose()

[<EntryPoint>]
let main args =
    let inputFile = args.[0]

    if not <| File.Exists(inputFile) then
        printfn "Input file doesn't exist: '%s'" inputFile
        1
    else
        let outputPath = args.[1]
        let inputFileExtension = Path.GetExtension(inputFile)

        if inputFileExtension = ".zip" then
            use zipFile = ZipFile.OpenRead(inputFile)

            zipFile.Entries |> Seq.filter(fun k -> k.Name.EndsWith(".xml"))
                            |> Seq.iter(fun k -> exportModelFile(k.Name, k.Open(), outputPath))
            0
        elif inputFileExtension = ".xml" then
            exportModelFile(inputFile, File.OpenRead(inputFile), outputPath)
            0
        else
            printfn "Unknown input file extension received: '%s', input file: '%s'" inputFileExtension inputFile
            1