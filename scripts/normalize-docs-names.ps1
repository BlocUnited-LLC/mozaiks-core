# PowerShell Script to Rename Docs to Kebab-Case
Get-ChildItem -Path "docs" -Recurse -File | ForEach-Object {
    $newName = $_.Name.ToLower().Replace("_", "-").Replace(" ", "-")
    # Handle CAPS files specifically if needed, but ToLower() handles the case.
    if ($_.Name -cne $newName) {
        $newPath = Join-Path $_.Directory.FullName $newName
        Write-Host "Renaming $($_.Name) to $newName"
        Rename-Item -Path $_.FullName -NewName $newName
    }
}
