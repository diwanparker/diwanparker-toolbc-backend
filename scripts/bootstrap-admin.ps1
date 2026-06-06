param(
    [string] $BaseUrl = "http://localhost:5272",
    [string] $FullName = "Admin ToolBC",
    [string] $Email = "admin@admin.com",
    [string] $Password = "Admin123!"
)

$body = @{
    fullName = $FullName
    email = $Email
    password = $Password
    role = "Admin"
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "$BaseUrl/api/bootstrap/admin" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
