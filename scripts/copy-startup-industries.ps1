<#
.SYNOPSIS
    Copies startup industries from an old PostgreSQL database into the new E-HUB database.

.DESCRIPTION
    Reads public.startup_industries from the old database and inserts rows that do not already exist
    in the new database (matched by normalized_name or id). Existing rows in the new database are never
    modified. Soft-deleted rows in the old database are skipped.

    Default is a dry run (the transaction is rolled back). Pass -Apply to commit.

    Connection strings are read from environment variables so they never appear in the command line,
    shell history or chat:
        $env:OLD_DB_URL = 'postgresql://user:password@host:5432/old_db'
        $env:NEW_DB_URL = 'postgresql://user:password@host:5432/new_db'

    psql runs inside a postgres Docker container. If a database is on this machine, use
    host.docker.internal instead of localhost in the URL.

.EXAMPLE
    ./scripts/copy-startup-industries.ps1          # dry run
    ./scripts/copy-startup-industries.ps1 -Apply   # commit
#>
param(
    [switch]$Apply,
    [string]$PostgresImage = 'postgres:18',
    [string]$DockerNetwork
)

$ErrorActionPreference = 'Stop'

foreach ($name in 'OLD_DB_URL', 'NEW_DB_URL') {
    if (-not [Environment]::GetEnvironmentVariable($name)) {
        throw "Environment variable $name is not set."
    }
}

if ($env:OLD_DB_URL -eq $env:NEW_DB_URL) {
    throw 'OLD_DB_URL and NEW_DB_URL point to the same database.'
}

$finalStatement = if ($Apply) { 'COMMIT;' } else { 'ROLLBACK;' }

$containerScript = @"
set -eu

has_description=`$(psql "`$OLD_DB_URL" -v ON_ERROR_STOP=1 -At -c "select count(*) from information_schema.columns where table_schema = 'public' and table_name = 'startup_industries' and column_name = 'description'")
if [ "`$has_description" = "1" ]; then description_expr="description"; else description_expr="null"; fi

psql "`$OLD_DB_URL" -v ON_ERROR_STOP=1 -c "\copy (select id, name, normalized_name, `$description_expr, status from public.startup_industries where not coalesce(is_deleted, false)) to '/tmp/old_industries.csv' with (format csv)"

{
cat <<'SQL'
begin;
create temp table old_industries (id uuid, name text, normalized_name text, description text, status text) on commit drop;
SQL
printf '%s\n' "\\copy old_industries from '/tmp/old_industries.csv' with (format csv)"
cat <<'SQL'
select count(*) as old_rows from old_industries;

select o.name as skipped_already_in_new
from old_industries o
where exists (
    select 1 from public.startup_industries n
    where n.normalized_name = coalesce(nullif(btrim(o.normalized_name), ''), upper(btrim(o.name)))
       or n.id = o.id);

with inserted as (
    insert into public.startup_industries (id, name, normalized_name, description, status, created_at, is_deleted)
    select o.id,
           left(btrim(o.name), 100),
           left(coalesce(nullif(btrim(o.normalized_name), ''), upper(btrim(o.name))), 100),
           nullif(left(btrim(coalesce(o.description, '')), 240), ''),
           case when lower(o.status) = 'inactive' then 'Inactive' else 'Active' end,
           now(),
           false
    from old_industries o
    where btrim(o.name) <> ''
    on conflict do nothing
    returning name
)
select name as inserted from inserted order by name;
SQL
printf '%s\n' "$finalStatement"
} | psql "`$NEW_DB_URL" -v ON_ERROR_STOP=1
: end of script
"@

$containerScript = $containerScript -replace "`r`n", "`n"

if ($Apply) { Write-Host 'APPLY mode: changes will be committed.' }
else { Write-Host 'DRY RUN: changes will be rolled back. Re-run with -Apply to commit.' }

$dockerArgs = @('run', '--rm', '-i', '-e', 'OLD_DB_URL', '-e', 'NEW_DB_URL')
if ($DockerNetwork) { $dockerArgs += @('--network', $DockerNetwork) }
$containerScript | docker @dockerArgs $PostgresImage sh -s
if ($LASTEXITCODE -ne 0) { throw "Copy failed with exit code $LASTEXITCODE." }
