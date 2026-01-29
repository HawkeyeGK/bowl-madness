$games = Invoke-RestMethod -Uri "http://localhost:7071/api/GetGames?seasonId=2025" -Method Get
$rose = $games | Where-Object { $_.id -eq "199c472a-21c0-4176-8794-5195c9b876e3" }
$orange = $games | Where-Object { $_.id -eq "fee1ac81-c8fb-4852-bc52-081c8cb720e4" }
$peach = $games | Where-Object { $_.id -eq "937758f5-88bb-491b-827a-15aa007a522a" }

Write-Host "--- ROSE BOWL ---"
$rose | ConvertTo-Json -Depth 5
Write-Host "`n--- ORANGE BOWL ---"
$orange | ConvertTo-Json -Depth 5
Write-Host "`n--- PEACH BOWL ---"
$peach | ConvertTo-Json -Depth 5
