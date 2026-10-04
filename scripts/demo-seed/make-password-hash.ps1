# Tao bcrypt hash cho mat khau demo, chay tren may Windows (can Docker Desktop dang bat).
# Mat khau chi duoc nhap tai may ban; chi hash (khong the dao nguoc) duoc in ra.
# Dung postgres:18-alpine tam thoi + pgcrypto de ra hash $2a$11$..., cung dinh dang BCrypt.Net cua backend.

param([string]$Password)

$ErrorActionPreference = 'Stop'

if ($Password) {
    $plain = $Password
}
else {
    $secure = Read-Host 'Nhap mat khau demo (it nhat 12 ky tu)' -AsSecureString
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}
if ($plain.Length -lt 12) { throw 'Mat khau qua ngan (can it nhat 12 ky tu).' }

$name = "ehub-hash-$(Get-Random)"
docker run -d --rm --name $name -e POSTGRES_PASSWORD=throwaway postgres:18-alpine | Out-Null
try {
    $sql = @'
\set pw `printenv DEMO_PW`
CREATE EXTENSION IF NOT EXISTS pgcrypto;
SELECT crypt(:'pw', gen_salt('bf', 11));
'@
    $hash = $null
    for ($i = 0; $i -lt 30 -and -not $hash; $i++) {
        Start-Sleep -Seconds 2
        $out = $sql | docker exec -i -e "DEMO_PW=$plain" $name psql -U postgres -At 2>$null
        $line = $out | Where-Object { $_ -like '$2a$11$*' } | Select-Object -First 1
        if ($line) { $hash = $line }
    }
    if (-not $hash) { throw 'Khong tao duoc hash (Postgres tam chua san sang?).' }
    Write-Host ''
    Write-Host 'Hash (an toan de dan vao VPS, khong the dao nguoc thanh mat khau):'
    Write-Host $hash
}
finally {
    docker rm -f $name | Out-Null
    $plain = $null
}
