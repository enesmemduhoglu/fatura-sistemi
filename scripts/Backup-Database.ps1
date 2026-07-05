# PostgreSQL yedekleme betiği.
# Bağlantı bilgilerini repo kökündeki .env dosyasından okur (DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD),
# pg_dump custom formatında tarih damgalı yedek alır ve kademeli saklama uygular:
#   - Son $KeepDailyDays gündeki TÜM yedekler tutulur
#   - Son $KeepWeeklyWeeks haftada her haftanın en yeni yedeği tutulur
#   - Son $KeepMonthlyMonths ayda her ayın en yeni yedeği tutulur
#   - Kalanlar silinir
#
# Elle çalıştırma:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Backup-Database.ps1
#
# Geri yükleme için: scripts\Restore-Database.ps1 (bkz. README).

param(
    [string]$BackupDir = (Join-Path $env:USERPROFILE "isbasi-yedekler"),
    [int]$KeepDailyDays = 14,
    [int]$KeepWeeklyWeeks = 8,
    [int]$KeepMonthlyMonths = 12,
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

# --- Kademeli saklama (gunluk / haftalik / aylik) ---
$now = Get-Date
$files = @(Get-ChildItem $BackupDir -Filter "isbasi_*.dump")
$keep = New-Object "System.Collections.Generic.HashSet[string]"
$calendar = [System.Globalization.CultureInfo]::InvariantCulture.Calendar

# Son N gundeki tum yedekler
foreach ($f in $files) {
    if (($now - $f.LastWriteTime).TotalDays -le $KeepDailyDays) { [void]$keep.Add($f.FullName) }
}

# Son N haftada her ISO haftasinin en yeni yedegi
$weekLimit = $now.AddDays(-7 * $KeepWeeklyWeeks)
$files | Where-Object { $_.LastWriteTime -ge $weekLimit } |
    Group-Object {
        $t = $_.LastWriteTime
        $week = $calendar.GetWeekOfYear($t, [System.Globalization.CalendarWeekRule]::FirstFourDayWeek, [DayOfWeek]::Monday)
        "{0}-W{1:D2}" -f $t.Year, $week
    } |
    ForEach-Object {
        $newest = $_.Group | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        [void]$keep.Add($newest.FullName)
    }

# Son N ayda her ayin en yeni yedegi
$monthLimit = $now.AddMonths(-$KeepMonthlyMonths)
$files | Where-Object { $_.LastWriteTime -ge $monthLimit } |
    Group-Object { $_.LastWriteTime.ToString("yyyy-MM") } |
    ForEach-Object {
        $newest = $_.Group | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        [void]$keep.Add($newest.FullName)
    }

foreach ($f in $files) {
    if (-not $keep.Contains($f.FullName)) {
        Remove-Item $f.FullName -Force -Confirm:$false
        Write-Output "Eski yedek silindi: $($f.Name)"
    }
}
