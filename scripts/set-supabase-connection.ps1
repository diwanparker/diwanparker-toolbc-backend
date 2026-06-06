param(
    [Parameter(Mandatory = $true)]
    [string] $ConnectionString
)

$project = Join-Path $PSScriptRoot "..\Toolbc.Api\Toolbc.Api.csproj"

function Convert-PostgresUrlToNpgsql {
    param([string] $Url)

    if ($Url -notmatch '^postgres(ql)?://') {
        return $Url
    }

    $uri = [Uri] $Url
    $userInfo = $uri.UserInfo.Split(':', 2)
    $username = [Uri]::UnescapeDataString($userInfo[0])
    $password = if ($userInfo.Length -gt 1) { [Uri]::UnescapeDataString($userInfo[1]) } else { "" }
    $database = $uri.AbsolutePath.TrimStart('/')

    return "Host=$($uri.Host);Port=$($uri.Port);Database=$database;Username=$username;Password=$password;Ssl Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=60;Pooling=false;Keepalive=30"
}

$normalizedConnectionString = Convert-PostgresUrlToNpgsql $ConnectionString
if ($normalizedConnectionString -notmatch 'Command Timeout=') {
    $normalizedConnectionString = $normalizedConnectionString.TrimEnd(';') + ";Timeout=30;Command Timeout=60;Pooling=false;Keepalive=30"
}

dotnet user-secrets init --project $project | Out-Null
dotnet user-secrets set "ConnectionStrings:Default" $normalizedConnectionString --project $project

Write-Host "Supabase connection string saved to .NET user-secrets."
