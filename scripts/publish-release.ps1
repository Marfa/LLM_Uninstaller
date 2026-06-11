# Builds portable and framework-dependent GUI/CLI packages into ./publish/
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

function Publish-Project($project, $output, [bool]$selfContained, [bool]$compress) {
    $args = @(
        "publish", $project,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", ($(if ($selfContained) { "true" } else { "false" })),
        "-p:PublishSingleFile=true",
        "-o", $output
    )
    if ($compress) { $args += "-p:EnableCompressionInSingleFile=true" }
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $project" }
}

Publish-Project "$root\src\LLMUninstaller.Gui\LLMUninstaller.Gui.csproj" "$root\publish\gui-portable" $true $true
Publish-Project "$root\src\LLMUninstaller.Gui\LLMUninstaller.Gui.csproj" "$root\publish\gui-framework" $false $false
Publish-Project "$root\src\LLMUninstaller.Cli\LLMUninstaller.Cli.csproj" "$root\publish\cli-portable" $true $true
Publish-Project "$root\src\LLMUninstaller.Cli\LLMUninstaller.Cli.csproj" "$root\publish\cli-framework" $false $false

Copy-Item "$root\publish\gui-framework\LLMUninstaller.exe" "$root\publish\gui-framework\LLMUninstaller-framework.exe"
Remove-Item "$root\publish\gui-framework\LLMUninstaller.exe"
Copy-Item "$root\publish\cli-framework\llmuninstaller-cli.exe" "$root\publish\cli-framework\llmuninstaller-cli-framework.exe"
Remove-Item "$root\publish\cli-framework\llmuninstaller-cli.exe"

Compress-Archive -Force -Path "$root\publish\gui-portable\LLMUninstaller.exe", "$root\publish\cli-portable\llmuninstaller-cli.exe" -DestinationPath "$root\LLMUninstaller-portable-win-x64.zip"
Compress-Archive -Force -Path "$root\publish\gui-framework\LLMUninstaller-framework.exe", "$root\publish\cli-framework\llmuninstaller-cli-framework.exe" -DestinationPath "$root\LLMUninstaller-framework-win-x64.zip"

Write-Host "Done. Assets in publish/ and ZIP archives in repo root."
