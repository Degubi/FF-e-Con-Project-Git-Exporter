open System
open System.Diagnostics;
open System.IO
open System.IO.Compression
open System.Xml.Linq

let unzipTmpFolder = "tmp_unzip"

let rec exportFilesFromRuleFolder(folder: XElement, modelName: string, sharedCounters: int[]) =
    let folderName = folder.Attribute(XName.Get "LABEL").Value
    let folderPath = $"{modelName}/{folderName}"

    folder.Elements(XName.Get "RULES") |> Seq.iter(fun k -> exportFilesFromRuleFolder(k, $"{modelName}/{folderName}", sharedCounters))
    folder.Elements(XName.Get "RULE") |> Seq.iter(fun rule ->
        let ruleName = rule.Attribute(XName.Get "LABEL").Value.Replace('/', '_')
        let condition = rule.Element(XName.Get "CONDITION").Value
        let implication = rule.Element(XName.Get "IMPLICATION").Value

        sharedCounters.[0] <- sharedCounters.[0] + 1
        Directory.CreateDirectory(folderPath) |> ignore
        File.WriteAllText($"{folderPath}/{sharedCounters.[0]}_{ruleName}.vbs", $"/*\nCondition: '{condition}'\n*/\n\n{implication}")
    )

let exportTopLevelRuleFolder(modelFileName: string) =
    let modelName = string modelFileName.[0] + modelFileName.Substring(1, modelFileName.IndexOf(',') - 1).ToLower()
    let sharedCounters = [| 0 |]

    if Directory.Exists modelName then
        Directory.Delete(modelName, true)

    printfn $"Exporting model: {modelName}"
    XDocument.Load($"{unzipTmpFolder}/Models/{modelFileName}")
             .Element(XName.Get "CLASS")
             .Element(XName.Get "RULES")
             .Elements(XName.Get "RULES") |> Seq.iter(fun k -> exportFilesFromRuleFolder(k, modelName, sharedCounters))


printfn "Enter commit message!"
let commitMessage = Console.ReadLine()

printfn "Using zip file: 'Export.zip'"
ZipFile.ExtractToDirectory("Export.zip", unzipTmpFolder)
Directory.GetFiles $"{unzipTmpFolder}/Models" |> Seq.map(Path.GetFileName)
                                              |> Seq.filter(fun k -> k.EndsWith ".xml" && k.[0] <> 'I')
                                              |> Seq.iter(exportTopLevelRuleFolder)
Directory.Delete(unzipTmpFolder, true)
File.Delete("Export.zip")

printfn "Publishing to GitHub"
Process.Start("cmd.exe", $"/c \"git config --global core.autocrlf true && git add -A 2>nul && git commit -m \"{commitMessage}\" 2>nul && git push origin master 2>nul\"")
       .WaitForExit()

printfn "Done! Press enter to exit"
Console.Read() |> ignore