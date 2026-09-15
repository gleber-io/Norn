$ErrorActionPreference = 'SilentlyContinue'
$input = [Console]::In.ReadToEnd() | ConvertFrom-Json
$filePath = $input.tool_input.file_path
if ($filePath -and (Test-Path $filePath) -and $filePath -like '*.cs') {
    dotnet format --include $filePath | Out-Null
}
