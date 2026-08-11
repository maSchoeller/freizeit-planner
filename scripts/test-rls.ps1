[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$containerName = "freizeit-rls-$PID-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$databasePassword = 'rls-test-only-password'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$databasePort = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

function Invoke-Psql {
    param([Parameter(Mandatory)][string]$Sql)

    $output = & docker exec -e "PGPASSWORD=$databasePassword" $containerName `
        psql --username postgres --dbname freizeit --no-psqlrc --tuples-only --no-align `
        --set ON_ERROR_STOP=1 --command $Sql
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL RLS assertion failed.' }
    return ($output | Out-String).Trim()
}

Push-Location $repositoryRoot
try {
    & docker run --detach --rm --name $containerName `
        --env "POSTGRES_PASSWORD=$databasePassword" `
        --env POSTGRES_USER=postgres `
        --env POSTGRES_DB=freizeit `
        --publish "127.0.0.1:${databasePort}:5432" `
        postgres:17 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Temporary PostgreSQL 17 container could not be started.' }

    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $containerName pg_isready --username postgres --dbname freizeit *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Temporary PostgreSQL 17 did not become ready.' }

    $previousConnection = $env:ConnectionStrings__freizeit
    $previousEnvironment = $env:DOTNET_ENVIRONMENT
    try {
        $env:ConnectionStrings__freizeit =
            "Host=127.0.0.1;Port=$databasePort;Database=freizeit;Username=postgres;Password=$databasePassword"
        $env:DOTNET_ENVIRONMENT = 'Development'
        dotnet run --project src/FreizeitCockpit.Migrator/FreizeitCockpit.Migrator.csproj `
            --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'RLS test migration failed.' }
        $env:FREIZEIT_ATOMIC_TEST_CONNECTION = $env:ConnectionStrings__freizeit
        dotnet test tests/Api.Tests/Api.Tests.csproj --no-restore --configuration Release `
            --filter 'FullyQualifiedName~AtomicPlanningTransactionTests'
        if ($LASTEXITCODE -ne 0) { throw 'Atomic planning rollback test failed.' }
    }
    finally {
        Remove-Item Env:FREIZEIT_ATOMIC_TEST_CONNECTION -ErrorAction SilentlyContinue
        $env:ConnectionStrings__freizeit = $previousConnection
        $env:DOTNET_ENVIRONMENT = $previousEnvironment
    }

    $roleAttributes = Invoke-Psql -Sql `
        "SELECT rolsuper::text || '|' || rolbypassrls::text FROM pg_roles WHERE rolname = 'freizeit_app';"
    if ($roleAttributes -ne 'false|false' -and $roleAttributes -ne 'f|f') {
        throw "Runtime role has unsafe attributes: $roleAttributes"
    }

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000006', true);
SELECT set_config('app.operation', 'invitation_acceptance', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 1 THEN
        RAISE EXCEPTION 'invitation acceptance cannot resolve its target organization';
    END IF;
END
$assert$;
INSERT INTO identity.organizations ("Id", "Name", "Slug", "Status", "Version")
VALUES ('20000000-0000-0000-0000-000000000098', 'Einladungs-Organization', 'einladungs-organization', 0, 1);
INSERT INTO identity.memberships
    (organization_id, user_id, "Role", "IsActive", "Version", "Status", "OrganizationRole")
VALUES
    ('20000000-0000-0000-0000-000000000098', '10000000-0000-0000-0000-000000000006', 4, true, 1, 0, 0);
INSERT INTO identity.camp_assignments
    (camp_id, user_id, "CampRole", "IsActive", organization_id, "Role", "Version")
VALUES
    ('30000000-0000-0000-0000-000000000098', '10000000-0000-0000-0000-000000000006', 1, true,
     '20000000-0000-0000-0000-000000000098', 2, 1);
ROLLBACK;
'@ | Out-Null

    Invoke-Psql -Sql @'
INSERT INTO identity.organizations ("Id", "Name", "Slug", "Status", "Version")
VALUES ('20000000-0000-0000-0000-000000000002', 'Fremder Veranstalter', 'fremd', 0, 1);
INSERT INTO identity.memberships (organization_id, user_id, "Role", "IsActive", "Version")
VALUES ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000006', 4, true, 1)
ON CONFLICT DO NOTHING;

INSERT INTO activity.activity_events
    ("Id", actor_id, organization_id, camp_id, "Kind", "ObjectType", object_id, "Title", "Timestamp", "Version")
VALUES
    ('71000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000005',
     '20000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001',
     'Created', 'Note', '72000000-0000-0000-0000-000000000001', 'Eigene Aktivität', now(), 1),
    ('71000000-0000-0000-0000-000000000099', '10000000-0000-0000-0000-000000000005',
     '20000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000099',
     'Created', 'Note', '72000000-0000-0000-0000-000000000099', 'Fremde Aktivität', now(), 1);

INSERT INTO activity.search_documents
    ("Id", organization_id, camp_id, "ObjectType", object_id, "Title", "SearchText", "MetadataJson",
     "SourceVersion", "IsRemoved", "UpdatedAt", "Version")
VALUES
    ('73000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001',
     '30000000-0000-0000-0000-000000000001', 'Note',
     '72000000-0000-0000-0000-000000000001', 'Eigener Suchtreffer', 'eigener text', '{}', 1, false, now(), 1),
    ('73000000-0000-0000-0000-000000000099', '20000000-0000-0000-0000-000000000001',
     '30000000-0000-0000-0000-000000000099', 'Note',
     '72000000-0000-0000-0000-000000000099', 'Fremder Suchtreffer', 'fremder text', '{}', 1, false, now(), 1);
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000005', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.camp_id', '30000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
DECLARE changed integer;
BEGIN
    IF (SELECT count(*) FROM activity.activity_events) <> 1 THEN
        RAISE EXCEPTION 'activity feed leaked another camp';
    END IF;
    IF (SELECT count(*) FROM activity.search_documents) <> 1 THEN
        RAISE EXCEPTION 'search index leaked another camp';
    END IF;
    UPDATE activity.search_documents SET "Title" = 'Unzulässig'
    WHERE "Id" = '73000000-0000-0000-0000-000000000099';
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed <> 0 THEN
        RAISE EXCEPTION 'foreign search document was writable';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000005', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.camp_id', '30000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 1 THEN
        RAISE EXCEPTION 'own organization not visible';
    END IF;
    IF (SELECT count(*) FROM identity.camp_assignments) <> 1 THEN
        RAISE EXCEPTION 'own camp assignment not visible';
    END IF;
    IF (SELECT count(*) FROM identity.memberships) <> 1 THEN
        RAISE EXCEPTION 'other organization memberships leaked';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000005', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.camp_id', '30000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.list_camp_members(
        '20000000-0000-0000-0000-000000000001',
        '30000000-0000-0000-0000-000000000001')) <> 5 THEN
        RAISE EXCEPTION 'camp member directory omitted allowed members or leaked an unassigned member';
    END IF;
    IF EXISTS (
        SELECT 1
        FROM identity.list_camp_members(
            '20000000-0000-0000-0000-000000000001',
            '30000000-0000-0000-0000-000000000001')
        WHERE "UserId" = '10000000-0000-0000-0000-000000000006') THEN
        RAISE EXCEPTION 'camp member directory leaked an unassigned organization member';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000006', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.camp_id', '30000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.list_camp_members(
        '20000000-0000-0000-0000-000000000001',
        '30000000-0000-0000-0000-000000000001')) <> 0 THEN
        RAISE EXCEPTION 'unassigned actor gained camp member directory access';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000005', true);
SELECT set_config('app.operation', 'platform_admin', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 0 THEN
        RAISE EXCEPTION 'tenant user spoofed platform metadata access';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000006', true);
SELECT set_config('app.operation', 'platform_admin', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 2 THEN
        RAISE EXCEPTION 'platform organization metadata list unavailable';
    END IF;
    IF (SELECT count(*) FROM identity.memberships) <> 6 THEN
        RAISE EXCEPTION 'superadmin membership administration view is incomplete';
    END IF;
    IF (SELECT count(*) FROM identity.camp_assignments) <> 3 THEN
        RAISE EXCEPTION 'superadmin camp-assignment administration view is incomplete';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000005', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000002', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
DECLARE changed integer;
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 0 THEN
        RAISE EXCEPTION 'foreign organization leaked';
    END IF;
    UPDATE identity.organizations SET "Name" = 'Unzulässig'
    WHERE "Id" = '20000000-0000-0000-0000-000000000002';
    GET DIAGNOSTICS changed = ROW_COUNT;
    IF changed <> 0 THEN
        RAISE EXCEPTION 'foreign organization was writable';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000006', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.camp_id', '30000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 1 THEN
        RAISE EXCEPTION 'superadmin explicit organization membership was ignored';
    END IF;
    IF (SELECT count(*) FROM activity.activity_events) <> 0 THEN
        RAISE EXCEPTION 'superadmin gained camp content access without a camp assignment';
    END IF;
END
$assert$;
COMMIT;
'@ | Out-Null

    Invoke-Psql -Sql @'
UPDATE identity.organizations SET "Status" = 1
WHERE "Id" = '20000000-0000-0000-0000-000000000001';
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.organization_id', '20000000-0000-0000-0000-000000000001', true);
SELECT set_config('app.operation', 'tenant', true);
DO $assert$
BEGIN
    IF (SELECT count(*) FROM identity.organizations) <> 0 THEN
        RAISE EXCEPTION 'suspended organization remained accessible';
    END IF;
END
$assert$;
COMMIT;
UPDATE identity.organizations SET "Status" = 0
WHERE "Id" = '20000000-0000-0000-0000-000000000001';
'@ | Out-Null

    $cleared = Invoke-Psql -Sql @'
BEGIN;
SET LOCAL ROLE freizeit_app;
SELECT set_config('app.user_id', '10000000-0000-0000-0000-000000000001', true);
COMMIT;
SELECT COALESCE(NULLIF(current_setting('app.user_id', true), ''), 'cleared');
'@
    if (($cleared -split "`r?`n")[-1] -ne 'cleared') {
        throw 'Tenant context escaped its transaction.'
    }

    Write-Host 'PostgreSQL RLS isolation passed.'
}
finally {
    Pop-Location
    & docker rm --force $containerName *> $null
}
