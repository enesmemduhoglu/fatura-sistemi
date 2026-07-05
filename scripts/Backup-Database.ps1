# PostgreSQL yedekleme betiği.
# Bağlantı bilgilerini repo kökündeki .env dosyasından okur (DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD),
# pg_dump custom formatında tarih damgalı yedek alır ve en yeni $KeepLast yedek dışındakileri siler.
#
# Elle çalıştırma:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Backup-Database.ps1
#
# Geri yükleme (örnek):
#   pg_restore -h localhost -U postgres -d isbasi --clean --if-exists <yedek-dosyasi>

param(
    [string]$BackupDir = (Join-Path $env:USERPROFILE "isbasi-yedekler"),
    [int]$KeepLast = 30,
    [string]$PgDump = "C:\Program Files\PostgreSQL\17\bin\pg_dump.exe"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot ".env"
if (-not (Test-Path $envFile)) { throw ".env dosyasi bulunamadi: $envFile" }
if (-not (Test-Path $PgDump)) { throw "pg_dump bulunamadi: $PgDump" }

$vars = @{}
foreach ($line in Get-Content $envFile) {
    $line = $line.Trim()
    if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
        $idx = $line.IndexOf("=")
        $vars[$line.Substring(0, $idx).Trim()] = $line.Substring($idx + 1).Trim()
    }
}
foreach ($key in @("DB_HOST", "DB_PORT", "DB_NAME", "DB_USER", "DB_PASSWORD")) {
    if (-not $vars.ContainsKey($key) -or -not $vars[$key]) { throw ".env icinde $key eksik" }
}

if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }

$stamp = Get-Date -Format "yyyy-MM-dd_HHmm"
$outFile = Join-Path $BackupDir "isbasi_$stamp.dump"

$env:PGPASSWORD = $vars["DB_PASSWORD"]
try {
    & $PgDump -h $vars["DB_HOST"] -p $vars["DB_PORT"] -U $vars["DB_USER"] -d $vars["DB_NAME"] -F c -f $outFile
    if ($LASTEXITCODE -ne 0) { throw "pg_dump basarisiz (cikis kodu $LASTEXITCODE)" }
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

$size = [math]::Round((Get-Item $outFile).Length / 1KB, 1)
Write-Output "Yedek alindi: $outFile ($size KB)"

# En yeni $KeepLast dosya dışındakileri sil
Get-ChildItem $BackupDir -Filter "isbasi_*.dump" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip $KeepLast |
    ForEach-Object {
        Remove-Item $_.FullName -Force -Confirm:$false
        Write-Output "Eski yedek silindi: $($_.Name)"
    }
