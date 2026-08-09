\set ON_ERROR_STOP on

-- Run this file as the configured Microsoft Entra PostgreSQL administrator.
-- Required psql variables: database_name, web_principal_name, jobs_principal_name.
-- Microsoft requires Entra principals to be created while connected to the postgres database.

DO $roles$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'freizeit_app') THEN
        CREATE ROLE freizeit_app
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'freizeit_jobs') THEN
        CREATE ROLE freizeit_jobs
            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
    END IF;
END
$roles$;

SELECT *
FROM pg_catalog.pgaadauth_create_principal(:'web_principal_name', false, false)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'web_principal_name');

SELECT *
FROM pg_catalog.pgaadauth_create_principal(:'jobs_principal_name', false, false)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'jobs_principal_name');

\connect :database_name

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT CONNECT ON DATABASE :"database_name" TO :"web_principal_name", :"jobs_principal_name";
REVOKE CREATE, TEMPORARY ON DATABASE :"database_name" FROM :"web_principal_name";
GRANT CREATE ON DATABASE :"database_name" TO :"jobs_principal_name";
GRANT freizeit_app TO :"web_principal_name";
GRANT freizeit_jobs TO :"jobs_principal_name";
