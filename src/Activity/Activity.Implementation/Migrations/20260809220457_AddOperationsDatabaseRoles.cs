using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Activity.Implementation.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationsDatabaseRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $operations_role$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'freizeit_jobs') THEN
                        CREATE ROLE freizeit_jobs
                            NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOBYPASSRLS;
                    END IF;
                END
                $operations_role$;

                GRANT USAGE ON SCHEMA identity, camps, catering, spiritual, knowledge, logistics, files, activity
                    TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA identity TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA camps TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA catering TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA spiritual TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA knowledge TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA logistics TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA files TO freizeit_jobs;
                GRANT SELECT, UPDATE, DELETE ON ALL TABLES IN SCHEMA activity TO freizeit_jobs;

                ALTER DEFAULT PRIVILEGES IN SCHEMA identity
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA camps
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA catering
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA spiritual
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA knowledge
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA logistics
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA files
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA activity
                    GRANT SELECT, UPDATE, DELETE ON TABLES TO freizeit_jobs;

                DO $operations_policies$
                DECLARE
                    item record;
                BEGIN
                    FOR item IN
                        SELECT schemaname, tablename
                        FROM pg_tables
                        WHERE schemaname IN
                            ('identity', 'camps', 'catering', 'spiritual',
                             'knowledge', 'logistics', 'files', 'activity')
                    LOOP
                        EXECUTE format(
                            'CREATE POLICY operations_cleanup_select ON %I.%I '
                            'FOR SELECT TO freizeit_jobs USING (true)',
                            item.schemaname,
                            item.tablename);
                        EXECUTE format(
                            'CREATE POLICY operations_cleanup_update ON %I.%I '
                            'FOR UPDATE TO freizeit_jobs USING (true) WITH CHECK (true)',
                            item.schemaname,
                            item.tablename);
                        EXECUTE format(
                            'CREATE POLICY operations_cleanup_delete ON %I.%I '
                            'FOR DELETE TO freizeit_jobs USING (true)',
                            item.schemaname,
                            item.tablename);
                    END LOOP;
                END
                $operations_policies$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $operations_policies$
                DECLARE
                    item record;
                BEGIN
                    FOR item IN
                        SELECT schemaname, tablename
                        FROM pg_tables
                        WHERE schemaname IN
                            ('identity', 'camps', 'catering', 'spiritual',
                             'knowledge', 'logistics', 'files', 'activity')
                    LOOP
                        EXECUTE format(
                            'DROP POLICY IF EXISTS operations_cleanup_select ON %I.%I',
                            item.schemaname,
                            item.tablename);
                        EXECUTE format(
                            'DROP POLICY IF EXISTS operations_cleanup_update ON %I.%I',
                            item.schemaname,
                            item.tablename);
                        EXECUTE format(
                            'DROP POLICY IF EXISTS operations_cleanup_delete ON %I.%I',
                            item.schemaname,
                            item.tablename);
                    END LOOP;
                END
                $operations_policies$;

                ALTER DEFAULT PRIVILEGES IN SCHEMA identity REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA camps REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA catering REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA spiritual REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA knowledge REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA logistics REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA files REVOKE ALL ON TABLES FROM freizeit_jobs;
                ALTER DEFAULT PRIVILEGES IN SCHEMA activity REVOKE ALL ON TABLES FROM freizeit_jobs;
                REVOKE ALL ON ALL TABLES IN SCHEMA identity, camps, catering, spiritual, knowledge, logistics, files, activity
                    FROM freizeit_jobs;
                REVOKE ALL ON SCHEMA identity, camps, catering, spiritual, knowledge, logistics, files, activity
                    FROM freizeit_jobs;
                """);
        }
    }
}
