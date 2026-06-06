$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $root
try {
    dotnet tool restore
    dotnet tool run dotnet-ef database update --project .\Toolbc.Api\Toolbc.Api.csproj --startup-project .\Toolbc.Api\Toolbc.Api.csproj
}
finally {
    Pop-Location
}
