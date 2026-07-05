# PostgreSQL geri yükleme betiği.
#
# Yedekleri listele (.env gerekmez):
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Restore-Database.ps1 -List
#
# Canlı veritabanını bir yedeğe döndür (üzerine yazmadan önce güvenlik yedeği alınır;
# canlıya yazdığı için -Force ister, uygulama kapalıyken çalıştırın):
#   ... Restore-Database.ps1 -File isbasi_2026-07-05_2000.dump -Force
#
# Yedeği AYRI bir veritabanına aç (canlıya dokunmaz; silinen tek bir kaydı bulup
# elle geri girmek için idealdir — pgAdmin/psql ile "isbasi_inceleme" içine bakılır):
#   ... Restore-Database.ps1 -File isbasi_2026-07-05_2000.dump -TargetDatabase isbasi_inceleme

param(
    [string]$File,
    [switch]$List,
    [string]$TargetDatabase,
    [switch]$Force,
    [string]$BackupDir = (Join-Path $env:USERPROFILE "isbasi-yedekler"),
    [string]$PgBin = "C:\Program Files\PostgreSQL\17\bin"
)

$ErrorActionPreference = "Stop"

# --- Listeleme: .env ve PostgreSQL araçları gerekmez ---
if ($List) {
    if (-not (Test-Path $BackupDir)) { Write-Output "Yedek klasoru yok: $BackupDir"; exit 0 }
    $backups = @(Get-ChildItem $BackupDir -Filter "isbasi_*.dump" | Sort-Object LastWriteTime -Descending)
    if ($backups.Count -eq 0) { Write-Output "Yedek bulunamadi: $BackupDir"; exit 0 }
    foreach ($b in $backups) {
        $size = [math]::Round($b.Length / 1KB, 1)
        Write-Output ("{0}  {1,10} KB  {2}" -f $b.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), $size, $b.Name)
    }
    exit 0
}

if (-not $File) { throw "Kullanim: -List ile yedekleri gorun ya da -File <yedek> verin (bkz. dosya basindaki ornekler)." }

# Dosya adi verildiyse yedek klasorunde aranir
$dumpFile = $File
if (-not (Test-Path $dumpFile)) { $dumpFile = Join-Path $BackupDir $File }
if (-not (Test-Path $dumpFile)) { throw "Yedek dosyasi bulunamadi: $File" }

$pgRestore = Join-Path $PgBin "pg_restore.exe"
$pgDump = Join-Path $PgBin "pg_dump.exe"
$psql = Join-Path $PgBin "psql.exe"
foreach ($tool in @($pgRestore, $psql)) {
    if (-not (Test-Path $tool)) { throw "PostgreSQL araci bulunamadi: $tool" }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot ".env"
if (-not (Test-Path $envFile)) { throw ".env dosyasi bulunamadi: $envFile" }

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

$liveDb = $vars["DB_NAME"]
if ($TargetDatabase) { $target = $TargetDatabase } else { $target = $liveDb }
$restoringLive = ($target -eq $liveDb)

if ($restoringLive -and -not $Force) {
    throw "Bu islem '$liveDb' canli veritabaninin UZERINE yazar. Emindeyseniz -Force ekleyin; canliya dokunmadan incelemek icin -TargetDatabase kullanin."
}

$env:PGPASSWORD = $vars["DB_PASSWORD"]
try {
    $connArgs = @("-h", $vars["DB_HOST"], "-p", $vars["DB_PORT"], "-U", $vars["DB_USER"])

    if ($restoringLive) {
        # Guvenlik agi: mevcut durum da yedeklenir ki geri yukleme pisman etmesin
        if (-not (Test-Path $pgDump)) { throw "PostgreSQL araci bulunamadi: $pgDump" }
        if (-not (Test-Path $BackupDir)) { New-Item -ItemType Directory -Path $BackupDir | Out-Null }
        $stamp = Get-Date -Format "yyyy-MM-dd_HHmm"
        $safety = Join-Path $BackupDir "isbasi_restore-oncesi_$stamp.dump"
        & $pgDump @connArgs -d $liveDb -F c -f $safety
        if ($LASTEXITCODE -ne 0) { throw "Guvenlik yedegi alinamadi (cikis kodu $LASTEXITCODE)" }
        Write-Output "Guvenlik yedegi alindi: $safety"
    }
    else {
        # Hedef veritabani yoksa olusturulur
        $exists = & $psql @connArgs -d "postgres" -t -A -c "SELECT 1 FROM pg_database WHERE datname = '$target'"
        if ($LASTEXITCODE -ne 0) { throw "Veritabani sorgusu basarisiz (cikis kodu $LASTEXITCODE)" }
        if ($exists -ne "1") {
            & $psql @connArgs -d "postgres" -c "CREATE DATABASE ""$target""" | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Veritabani olusturulamadi: $target" }
            Write-Output "Veritabani olusturuldu: $target"
        }
    }

    # --clean --if-exists: hedefteki nesneler dusurulup yedekten yeniden kurulur
    & $pgRestore @connArgs -d $target --clean --if-exists $dumpFile
    if ($LASTEXITCODE -ne 0) { throw "pg_restore basarisiz (cikis kodu $LASTEXITCODE)" }
    Write-Output "Geri yukleme tamamlandi: $dumpFile -> $target"
} finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}
