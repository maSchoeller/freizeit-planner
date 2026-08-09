[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$containerName = "freizeit-cleanup-$PID-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$databasePassword = 'cleanup-test-only-password'
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$databasePort = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

function Invoke-Psql {
    param([Parameter(Mandatory)][string]$Sql)

    $output = & docker exec -e "PGPASSWORD=$databasePassword" $containerName `
        psql --username postgres --dbname freizeit --no-psqlrc --tuples-only --no-align `
        --set ON_ERROR_STOP=1 --command $Sql
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL cleanup assertion failed.' }
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
    $previousBlobs = $env:ConnectionStrings__blobs
    $previousEnvironment = $env:DOTNET_ENVIRONMENT
    $previousMicrosoftLogLevel = $env:Logging__LogLevel__Microsoft
    try {
        $env:ConnectionStrings__freizeit =
            "Host=127.0.0.1;Port=$databasePort;Database=freizeit;Username=postgres;Password=$databasePassword"
        $env:ConnectionStrings__blobs = 'UseDevelopmentStorage=true'
        $env:DOTNET_ENVIRONMENT = 'Development'
        $env:Logging__LogLevel__Microsoft = 'Warning'
        dotnet run --project src/FreizeitCockpit.Migrator/FreizeitCockpit.Migrator.csproj `
            --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Cleanup test migration failed.' }

        Invoke-Psql -Sql @'
UPDATE identity.organizations
SET "DeletionScheduledAt" = now() - interval '31 days'
WHERE "Id" = '20000000-0000-0000-0000-000000000001';

INSERT INTO identity.organizations ("Id", "Name", "Slug", "Status", "Version")
VALUES ('20000000-0000-0000-0000-000000000002', 'Bleibender Veranstalter', 'bleibend', 0, 1);

UPDATE identity.users
SET "DeletionScheduledAt" = now() - interval '31 days'
WHERE "Id" = '10000000-0000-0000-0000-000000000004';

INSERT INTO knowledge.notes
    ("Id", organization_id, camp_id, "Title", "Markdown", "IsPinned", "State",
     "CreatedAt", created_by, "UpdatedAt", updated_by, "Version")
VALUES
    ('71000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001',
     '30000000-0000-0000-0000-000000000001', 'Zu löschen', 'Inhalt', false, 'Active',
     now(), '10000000-0000-0000-0000-000000000001', now(),
     '10000000-0000-0000-0000-000000000001', 1),
    ('71000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002',
     '30000000-0000-0000-0000-000000000002', 'Bleibt anonymisiert', 'Inhalt', false, 'Active',
     now(), '10000000-0000-0000-0000-000000000004', now(),
     '10000000-0000-0000-0000-000000000004', 1);

INSERT INTO activity.activity_events
    ("Id", actor_id, organization_id, camp_id, "Kind", "ObjectType", object_id,
     "Title", "Timestamp", "Version")
VALUES
    ('72000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001',
     '20000000-0000-0000-0000-000000000001', '30000000-0000-0000-0000-000000000001',
     'Created', 'Note', '71000000-0000-0000-0000-000000000001', 'Zu löschen', now(), 1),
    ('72000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000004',
     '20000000-0000-0000-0000-000000000002', '30000000-0000-0000-0000-000000000002',
     'Created', 'Note', '71000000-0000-0000-0000-000000000002', 'Bleibt anonymisiert', now(), 1);

INSERT INTO spiritual.bible_snapshots
    ("Id", organization_id, camp_id, devotion_id, "Reference", "TextExcerpt",
     "TechnicalTranslationId", "TranslationDisplayName", "License", "Attribution", "RetrievedAt", "Origin")
VALUES
    ('73000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002',
     '30000000-0000-0000-0000-000000000002', '74000000-0000-0000-0000-000000000001',
     'Johannes 3,16', 'Text', 'deu1951', 'Schlachter 1951', 'CC BY 4.0', 'Test', now(), 'Manual');

INSERT INTO spiritual.devotions
    ("Id", organization_id, camp_id, "Topic", "BibleReference", "Translation", "CoreMessage",
     "MarkdownContent", "ResponsibleUserIds", "MaterialNotes", current_bible_snapshot_id,
     "CreatedAt", "UpdatedAt", "DeletedAt", "Version")
VALUES
    ('74000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002',
     '30000000-0000-0000-0000-000000000002', 'Abgelaufene Andacht', 'Johannes 3,16',
     'Schlachter1951', 'Kernaussage', '# Inhalt', ARRAY[]::uuid[], '',
     '73000000-0000-0000-0000-000000000001', now() - interval '40 days', now() - interval '31 days',
     now() - interval '31 days', 2);

INSERT INTO logistics.material_requirements
    ("Id", organization_id, camp_id, "Name", "QuantityValue", "QuantityUnit", "Status",
     "Version", "DeletedAt", "PurgeAt")
VALUES
    ('75000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002',
     '30000000-0000-0000-0000-000000000002', 'Abgelaufenes Material', 1, 4, 0, 2,
     now() - interval '31 days', now() - interval '1 day');
'@ | Out-Null

        dotnet run --project src/FreizeitCockpit.Cleanup/FreizeitCockpit.Cleanup.csproj `
            --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'Cleanup process failed.' }
    }
    finally {
        $env:ConnectionStrings__freizeit = $previousConnection
        $env:ConnectionStrings__blobs = $previousBlobs
        $env:DOTNET_ENVIRONMENT = $previousEnvironment
        $env:Logging__LogLevel__Microsoft = $previousMicrosoftLogLevel
    }

    Invoke-Psql -Sql @'
DO $assert$
BEGIN
    IF EXISTS (SELECT FROM identity.organizations
               WHERE "Id" = '20000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'due organization identity remained';
    END IF;
    IF EXISTS (SELECT FROM knowledge.notes
               WHERE organization_id = '20000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'due organization domain data remained';
    END IF;
    IF EXISTS (SELECT FROM activity.activity_events
               WHERE organization_id = '20000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'due organization activity data remained';
    END IF;
    IF EXISTS (SELECT FROM identity.users
               WHERE "Id" = '10000000-0000-0000-0000-000000000004') THEN
        RAISE EXCEPTION 'due account remained';
    END IF;
    IF EXISTS (SELECT FROM knowledge.notes
               WHERE created_by = '10000000-0000-0000-0000-000000000004'
                  OR updated_by = '10000000-0000-0000-0000-000000000004') THEN
        RAISE EXCEPTION 'note audit identity was not pseudonymized';
    END IF;
    IF NOT EXISTS (SELECT FROM knowledge.notes
                   WHERE organization_id = '20000000-0000-0000-0000-000000000002'
                     AND created_by = '00000000-0000-0000-0000-000000000000'
                     AND updated_by = '00000000-0000-0000-0000-000000000000') THEN
        RAISE EXCEPTION 'remaining note was not preserved with pseudonymous audit fields';
    END IF;
    IF NOT EXISTS (SELECT FROM activity.activity_events
                   WHERE organization_id = '20000000-0000-0000-0000-000000000002'
                     AND actor_id = '00000000-0000-0000-0000-000000000000') THEN
        RAISE EXCEPTION 'remaining activity was not pseudonymized';
    END IF;
    IF EXISTS (SELECT FROM spiritual.devotions
               WHERE "Id" = '74000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'expired devotion remained';
    END IF;
    IF EXISTS (SELECT FROM spiritual.bible_snapshots
               WHERE "Id" = '73000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'expired devotion snapshot remained';
    END IF;
    IF EXISTS (SELECT FROM logistics.material_requirements
               WHERE "Id" = '75000000-0000-0000-0000-000000000001') THEN
        RAISE EXCEPTION 'expired material requirement remained';
    END IF;
END
$assert$;
'@ | Out-Null

    Write-Host 'PostgreSQL cleanup and privacy erasure passed.'
}
finally {
    Pop-Location
    & docker rm --force $containerName *> $null
}
